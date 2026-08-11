using System.Security.Cryptography;
using DokTrino.Application.Common;
using DokTrino.Application.Common.Auth;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class TrdAdminService : ITrdAdminService
{
    private static readonly string[] EstadosTrd = ["DESARROLLO", "ACTIVO", "CERRADO"];

    /// <summary>
    /// Clave con la que nace la cuenta del colaborador. Es una credencial conocida
    /// y decidida por el cliente: quien reciba el correo de una persona puede
    /// entrar a su cuenta hasta que la cambie. El enlace por token no la necesita.
    /// </summary>
    private const string ClavePorDefecto = "12345";

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;
    private readonly IPasswordHasher _hasher;

    public TrdAdminService(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock, IPasswordHasher hasher)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
        _hasher = hasher;
    }

    public async Task<IReadOnlyList<TrdDto>> ListarTrdAsync(CancellationToken ct = default) =>
        await _db.TablasRetencionDocumental.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Select(x => new TrdDto(x.Id, x.Consecutivo, x.Titulo, x.Estado,
                x.SegmentoId == null ? null : _db.Segmentos.Where(s => s.Id == x.SegmentoId).Select(s => s.Nombre).FirstOrDefault(),
                x.FechaInicio, x.FechaFin,
                _db.Dependencias.Count(d => d.TrdId == x.Id),
                x.Observaciones,
                _db.RespuestasTablaDocumental.Count(r => r.TrdId == x.Id),
                x.CreatedAt))
            .ToListAsync(ct);

    public async Task<TrdDto?> CrearTrdAsync(CrearTrdRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var titulo = (req.Titulo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(titulo)) { throw new InvalidOperationException("El titulo de la TRD es obligatorio."); }
        if (req.SegmentoId is Guid sid && !await _db.Segmentos.AnyAsync(s => s.Id == sid, ct)) { throw new InvalidOperationException("El segmento no existe."); }

        var seq = await _db.TablasRetencionDocumental.CountAsync(ct) + 1;
        string consecutivo;
        do { consecutivo = $"TRD-{seq:D4}"; seq++; }
        while (await _db.TablasRetencionDocumental.AnyAsync(x => x.Consecutivo == consecutivo, ct));

        var trd = new TablaRetencionDocumental
        {
            TenantId = tenantId, Consecutivo = consecutivo, Titulo = titulo, Estado = "DESARROLLO",
            SegmentoId = req.SegmentoId, FechaInicio = req.FechaInicio, FechaFin = req.FechaFin,
            Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones!.Trim(),
            CreadoPor = actor
        };
        _db.TablasRetencionDocumental.Add(trd);
        await _db.SaveChangesAsync(ct);
        return new TrdDto(trd.Id, trd.Consecutivo, trd.Titulo, trd.Estado, null, trd.FechaInicio, trd.FechaFin, 0);
    }

    public async Task<TrdDto?> DuplicarTrdAsync(Guid trdId, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var origen = await _db.TablasRetencionDocumental.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trdId, ct);
        if (origen is null) { return null; }

        // Consecutivo nuevo (misma logica de secuencia que CrearTrdAsync).
        var seq = await _db.TablasRetencionDocumental.CountAsync(ct) + 1;
        string consecutivo;
        do { consecutivo = $"TRD-{seq:D4}"; seq++; }
        while (await _db.TablasRetencionDocumental.AnyAsync(x => x.Consecutivo == consecutivo, ct));

        // La copia nace en DESARROLLO y con titulo marcado. El cap protege el max(200).
        var baseTitulo = origen.Titulo.Length > 192 ? origen.Titulo[..192] : origen.Titulo;
        var nueva = new TablaRetencionDocumental
        {
            TenantId = tenantId, Consecutivo = consecutivo, Titulo = baseTitulo + " (copia)", Estado = "DESARROLLO",
            SegmentoId = origen.SegmentoId, FechaInicio = origen.FechaInicio, FechaFin = origen.FechaFin,
            Observaciones = origen.Observaciones, CreadoPor = actor
        };
        _db.TablasRetencionDocumental.Add(nueva);

        // Dependencias: se copia todo el organigrama MENOS la agenda (FechaInicioEstimada/FechaFinEstimada),
        // que es propia de cada encuesta. Se remapea Id -> Id nuevo para reconstruir el arbol (PadreId).
        var deps = await _db.Dependencias.AsNoTracking().Where(d => d.TrdId == trdId)
            .OrderBy(d => d.Nivel).ThenBy(d => d.Orden).ToListAsync(ct);
        var mapDep = new Dictionary<Guid, Guid>();
        var nuevasDeps = new List<Dependencia>();
        foreach (var d in deps)
        {
            var nd = new Dependencia
            {
                TenantId = tenantId, TrdId = nueva.Id, Nivel = d.Nivel, Orden = d.Orden,
                NombreCargo = d.NombreCargo, Codigo = d.Codigo, Estado = d.Estado,
                CodigoRaizDocumental = d.CodigoRaizDocumental, GerenteNombre = d.GerenteNombre,
                GerenteEmail = d.GerenteEmail, Observaciones = d.Observaciones
                // FechaInicioEstimada / FechaFinEstimada: NO se copian (la agenda es propia de cada encuesta).
            };
            mapDep[d.Id] = nd.Id;
            nuevasDeps.Add(nd);
        }
        // Reasignar padres con el mapa (el orden por Nivel garantiza que el padre ya esta mapeado).
        for (var i = 0; i < deps.Count; i++)
        {
            if (deps[i].PadreId is Guid pid && mapDep.TryGetValue(pid, out var np)) { nuevasDeps[i].PadreId = np; }
        }
        _db.Dependencias.AddRange(nuevasDeps);

        // Personas (colaboradores) por dependencia.
        var depIds = mapDep.Keys.ToList();
        var colabs = await _db.ColaboradoresDependencia.AsNoTracking()
            .Where(c => depIds.Contains(c.DependenciaId)).ToListAsync(ct);
        foreach (var c in colabs)
        {
            _db.ColaboradoresDependencia.Add(new ColaboradorDependencia
            {
                TenantId = tenantId, DependenciaId = mapDep[c.DependenciaId], UsuarioId = c.UsuarioId,
                Email = c.Email, Nombre = c.Nombre, Telefono = c.Telefono, Rol = c.Rol
            });
        }

        // Matriz TRD (respuestas archivisticas) + sus formatos. Se remapea RespuestaId.
        var resps = await _db.RespuestasTablaDocumental.AsNoTracking().Where(r => r.TrdId == trdId).ToListAsync(ct);
        var mapResp = new Dictionary<Guid, Guid>();
        foreach (var r in resps)
        {
            var nr = new RespuestaTablaDocumental
            {
                TenantId = tenantId, TrdId = nueva.Id,
                DependenciaId = mapDep.TryGetValue(r.DependenciaId, out var nid) ? nid : r.DependenciaId,
                SerieId = r.SerieId, SubserieId = r.SubserieId, TipologiaId = r.TipologiaId, SinSubserie = r.SinSubserie,
                TiempoAg = r.TiempoAg, TiempoAc = r.TiempoAc, TiempoObserv = r.TiempoObserv,
                DispCt = r.DispCt, DispS = r.DispS, DispE = r.DispE, DispD = r.DispD, DispObserv = r.DispObserv,
                Val1Admin = r.Val1Admin, Val1Tecnica = r.Val1Tecnica, Val1Legal = r.Val1Legal,
                Val1Contable = r.Val1Contable, Val1Fiscal = r.Val1Fiscal,
                Val2Historica = r.Val2Historica, Val2Cientifica = r.Val2Cientifica, Val2Cultural = r.Val2Cultural,
                Representativo = r.Representativo, SerieDdhh = r.SerieDdhh, RelacionSig = r.RelacionSig,
                Extension = r.Extension, FechaReg = r.FechaReg, CreadoPor = actor
            };
            mapResp[r.Id] = nr.Id;
            _db.RespuestasTablaDocumental.Add(nr);
        }
        var respIds = mapResp.Keys.ToList();
        var formatos = await _db.FormatosSerie.AsNoTracking().Where(f => respIds.Contains(f.RespuestaId)).ToListAsync(ct);
        foreach (var f in formatos)
        {
            _db.FormatosSerie.Add(new FormatoSerie
            {
                TenantId = tenantId, RespuestaId = mapResp[f.RespuestaId],
                Soporte = f.Soporte, Formato = f.Formato, Descripcion = f.Descripcion
            });
        }

        await _db.SaveChangesAsync(ct);
        return new TrdDto(nueva.Id, nueva.Consecutivo, nueva.Titulo, nueva.Estado, null,
            nueva.FechaInicio, nueva.FechaFin, deps.Count);
    }

    public async Task<bool> CambiarEstadoAsync(Guid trdId, string estado, Guid actor, CancellationToken ct = default)
    {
        estado = (estado ?? "").Trim().ToUpperInvariant();
        if (!EstadosTrd.Contains(estado)) { throw new InvalidOperationException("Estado invalido."); }
        var trd = await _db.TablasRetencionDocumental.FirstOrDefaultAsync(x => x.Id == trdId, ct);
        if (trd is null) { return false; }
        // Transicion controlada: DESARROLLO -> ACTIVO -> CERRADO (no saltar a un estado anterior).
        int Orden(string e) => Array.IndexOf(EstadosTrd, e);
        if (Orden(estado) < Orden(trd.Estado)) { throw new InvalidOperationException($"No se puede volver de {trd.Estado} a {estado}."); }

        // Solo una encuesta activa a la vez. Se bloquea en vez de cerrar la
        // vigente sola: cerrar es irreversible y tumbaria el levantamiento en
        // curso de las dependencias sin que nadie lo pida.
        if (estado == "ACTIVO")
        {
            var vigente = await _db.TablasRetencionDocumental.AsNoTracking()
                .Where(x => x.Estado == "ACTIVO" && x.Id != trdId)
                .Select(x => x.Consecutivo)
                .FirstOrDefaultAsync(ct);
            if (vigente is not null)
            {
                throw new InvalidOperationException(
                    $"Ya hay una encuesta activa ({vigente}). Cierrala antes de activar esta.");
            }
        }

        trd.Estado = estado;
        trd.FechaNovedad = _clock.GetUtcNow();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarTrdAsync(Guid trdId, Guid actor, CancellationToken ct = default)
    {
        var trd = await _db.TablasRetencionDocumental.FirstOrDefaultAsync(x => x.Id == trdId, ct);
        if (trd is null) { return false; }
        _db.TablasRetencionDocumental.Remove(trd);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DependenciaDto>> ArbolDependenciasAsync(Guid trdId, CancellationToken ct = default) =>
        await _db.Dependencias.AsNoTracking().Where(d => d.TrdId == trdId)
            .OrderBy(d => d.Nivel).ThenBy(d => d.Orden)
            .Select(d => new DependenciaDto(d.Id, d.PadreId, d.Nivel, d.Orden, d.NombreCargo, d.Codigo, d.Estado,
                _db.ColaboradoresDependencia.Count(c => c.DependenciaId == d.Id),
                d.CodigoRaizDocumental, d.GerenteNombre, d.GerenteEmail, d.Observaciones,
                d.FechaInicioEstimada, d.FechaFinEstimada))
            .ToListAsync(ct);

    public async Task<DependenciaDto?> AgregarDependenciaAsync(CrearDependenciaRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var nombre = (req.NombreCargo ?? "").Trim();
        var codigo = (req.Codigo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(codigo)) { throw new InvalidOperationException("Nombre y codigo de la dependencia son obligatorios."); }
        if (!await _db.TablasRetencionDocumental.AnyAsync(t => t.Id == req.TrdId, ct)) { throw new InvalidOperationException("La TRD no existe."); }

        short nivel = 0;
        if (req.PadreId is Guid pid)
        {
            var padre = await _db.Dependencias.FirstOrDefaultAsync(d => d.Id == pid, ct);
            if (padre is null) { throw new InvalidOperationException("La dependencia padre no existe."); }
            nivel = (short)(padre.Nivel + 1);
        }
        // Orden = max(peer)+1 (el unique (trd,padre,orden) protege ante carrera).
        var maxOrden = await _db.Dependencias.Where(d => d.TrdId == req.TrdId && d.PadreId == req.PadreId).MaxAsync(d => (int?)d.Orden, ct) ?? 0;
        var dep = new Dependencia
        {
            TenantId = tenantId, TrdId = req.TrdId, PadreId = req.PadreId, Nivel = nivel, Orden = maxOrden + 1,
            NombreCargo = nombre, Codigo = codigo, Estado = "ACTIVO"
        };
        _db.Dependencias.Add(dep);
        await _db.SaveChangesAsync(ct);
        return new DependenciaDto(dep.Id, dep.PadreId, dep.Nivel, dep.Orden, dep.NombreCargo, dep.Codigo, dep.Estado);
    }

    public async Task<bool> ActualizarDependenciaAsync(Guid id, string codigo, string nombreCargo, Guid actor, CancellationToken ct = default)
    {
        var dep = await _db.Dependencias.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dep is null) { return false; }

        var cod = (codigo ?? "").Trim();
        var nom = (nombreCargo ?? "").Trim();
        if (cod.Length == 0 || nom.Length == 0)
        {
            throw new InvalidOperationException("Codigo y nombre de la dependencia son obligatorios.");
        }
        if (cod.Length > 30) { throw new InvalidOperationException("El codigo admite hasta 30 caracteres."); }

        dep.Codigo = cod;
        dep.NombreCargo = nom;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> GuardarPropiedadesDependenciaAsync(GuardarPropiedadesDependenciaRequest req, Guid actor, CancellationToken ct = default)
    {
        var dep = await _db.Dependencias.FirstOrDefaultAsync(d => d.Id == req.Id, ct);
        if (dep is null) { return false; }

        static string? Limpio(string? s, int max)
        {
            s = s?.Trim();
            if (string.IsNullOrEmpty(s)) { return null; }
            return s.Length > max ? s[..max] : s;
        }

        var email = Limpio(req.GerenteEmail, 200);
        if (email is not null && !email.Contains('@')) { throw new InvalidOperationException("El correo del gerente no es valido."); }

        if (req.FechaInicioEstimada is DateOnly ini && req.FechaFinEstimada is DateOnly fin && fin < ini)
        {
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la de inicio.");
        }

        dep.CodigoRaizDocumental = Limpio(req.CodigoRaizDocumental, 60);
        dep.GerenteNombre = Limpio(req.GerenteNombre, 200);
        dep.GerenteEmail = email;
        dep.Observaciones = Limpio(req.Observaciones, 2000);
        dep.FechaInicioEstimada = req.FechaInicioEstimada;
        dep.FechaFinEstimada = req.FechaFinEstimada;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<AgendaDependenciaDto>> AgendaTrdAsync(Guid trdId, CancellationToken ct = default)
    {
        // El curso vigente sirve para marcar quien ya aprobo la capacitacion.
        return await _db.Dependencias.AsNoTracking()
            .Where(d => d.TrdId == trdId)
            .OrderBy(d => d.Nivel).ThenBy(d => d.Orden)
            .Select(d => new AgendaDependenciaDto(
                d.Id, d.Codigo, d.NombreCargo, d.Nivel,
                d.FechaInicioEstimada, d.FechaFinEstimada,
                _db.ColaboradoresDependencia.Count(c => c.DependenciaId == d.Id),
                _db.RespuestasTablaDocumental.Count(r => r.DependenciaId == d.Id),
                _db.CuestionarioIntentos.Any(i => i.DependenciaId == d.Id && i.Aprobado)))
            .ToListAsync(ct);
    }

    public async Task<bool> ActualizarFechasDependenciaAsync(Guid id, DateOnly? inicio, DateOnly? fin, Guid actor, CancellationToken ct = default)
    {
        var dep = await _db.Dependencias.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dep is null) { return false; }
        if (inicio is DateOnly i && fin is DateOnly f && f < i)
        {
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la de inicio.");
        }
        dep.FechaInicioEstimada = inicio;
        dep.FechaFinEstimada = fin;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IndicadoresDependenciaDto> IndicadoresDependenciaAsync(Guid depId, CancellationToken ct = default)
    {
        // Documentos que esta dependencia declaro en la TRD (matriz).
        var docsQ = _db.RespuestasTablaDocumental.AsNoTracking().Where(r => r.DependenciaId == depId);
        var docsCargados = await docsQ.CountAsync(ct);
        var seriesDistintas = await docsQ.Select(r => r.SerieId).Distinct().CountAsync(ct);

        // Documentos por mes (ultimos 6 meses con actividad), a partir de fecha_reg.
        var porMesRaw = await docsQ
            .GroupBy(r => new { r.FechaReg.Year, r.FechaReg.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() })
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .Take(6)
            .ToListAsync(ct);
        var porMes = porMesRaw
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new PuntoMensualDto($"{x.Month:00}/{x.Year % 100:00}", x.Total))
            .ToList();

        // Evaluacion de formacion: intentos y aprobados de esta dependencia.
        var intentosQ = _db.CuestionarioIntentos.AsNoTracking().Where(i => i.DependenciaId == depId);
        var evalIntentos = await intentosQ.CountAsync(ct);
        var evalAprobados = await intentosQ.CountAsync(i => i.Aprobado, ct);

        // Ingresos: tokens de la dependencia efectivamente consumidos (cada colaborador
        // entra por su enlace; consumido_en marca cuando ingreso).
        var tokensQ = _db.TokensDependencia.AsNoTracking().Where(t => t.DependenciaId == depId && t.ConsumidoEn != null);
        var ingresos = await tokensQ.CountAsync(ct);
        var colabQueIngresaron = await tokensQ.Select(t => t.ColaboradorId).Distinct().CountAsync(ct);

        var personas = await _db.ColaboradoresDependencia.AsNoTracking().CountAsync(c => c.DependenciaId == depId, ct);

        return new IndicadoresDependenciaDto(
            personas, docsCargados, seriesDistintas, porMes,
            evalIntentos, evalAprobados, ingresos, colabQueIngresaron);
    }

    public async Task<bool> EliminarDependenciaAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var dep = await _db.Dependencias.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dep is null) { return false; }
        _db.Dependencias.Remove(dep);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ColaboradorDto>> ColaboradoresAsync(Guid dependenciaId, string baseUrl, CancellationToken ct = default)
    {
        var cols = await _db.ColaboradoresDependencia.AsNoTracking()
            .Where(c => c.DependenciaId == dependenciaId)
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.DependenciaId, c.Nombre, c.Email, c.Rol, c.Telefono })
            .ToListAsync(ct);

        // Ultimo enlace vigente de cada persona: el enlace es por persona, no por
        // dependencia, asi que cada quien ve solo el suyo.
        var ids = cols.Select(c => c.Id).ToList();
        var tokens = await _db.TokensDependencia.AsNoTracking()
            .Where(t => t.ColaboradorId != null && ids.Contains(t.ColaboradorId!.Value) && t.ConsumidoEn == null)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.ColaboradorId, t.Token })
            .ToListAsync(ct);
        var porColaborador = tokens
            .GroupBy(t => t.ColaboradorId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Token);

        return cols.Select(c => new ColaboradorDto(
                c.Id, c.DependenciaId, c.Nombre, c.Email, c.Rol, c.Telefono,
                porColaborador.TryGetValue(c.Id, out var tk) ? $"{baseUrl.TrimEnd('/')}/trd-cliente?token={tk}" : null))
            .ToList();
    }

    public async Task<ColaboradorDto?> AgregarColaboradorAsync(CrearColaboradorRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var nombre = (req.Nombre ?? "").Trim();
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
        var rol = string.IsNullOrWhiteSpace(req.Rol) ? "RESPONSABLE" : req.Rol.Trim();
        if (nombre.Length == 0 || email.Length == 0)
        {
            throw new InvalidOperationException("Nombre y correo de la persona son obligatorios.");
        }
        if (!await _db.Dependencias.AnyAsync(d => d.Id == req.DependenciaId, ct))
        {
            throw new InvalidOperationException("La dependencia no existe.");
        }
        // El indice unico (dependencia, email) ya lo impide; avisamos antes para
        // no devolver un error de base de datos a la pantalla.
        if (await _db.ColaboradoresDependencia.AnyAsync(c => c.DependenciaId == req.DependenciaId && c.Email == email, ct))
        {
            throw new InvalidOperationException("Esa persona ya esta asignada a la dependencia.");
        }

        var col = new ColaboradorDependencia
        {
            TenantId = tenantId, DependenciaId = req.DependenciaId,
            Nombre = nombre, Email = email, Telefono = telefono, Rol = rol
        };
        _db.ColaboradoresDependencia.Add(col);
        await _db.SaveChangesAsync(ct);

        await AsegurarCuentaAsync(col, tenantId, ct);

        return new ColaboradorDto(col.Id, col.DependenciaId, col.Nombre, col.Email, col.Rol, col.Telefono);
    }

    /// <summary>
    /// Provisiona la cuenta de acceso del colaborador con la clave por defecto.
    /// Si el correo ya existe en la plataforma se reutiliza la cuenta y NO se toca
    /// su clave: sobrescribirla dejaria fuera a alguien que ya usa el sistema.
    /// </summary>
    private async Task AsegurarCuentaAsync(ColaboradorDependencia col, Guid tenantId, CancellationToken ct)
    {
        var pu = await _db.PlatformUsers.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Email == col.Email, ct);
        if (pu is null)
        {
            pu = new PlatformUser
            {
                Email = col.Email,
                DisplayName = col.Nombre,
                EmailVerified = true,
                AuthProvider = "local",
                PasswordHash = _hasher.Hash(ClavePorDefecto),
                Status = PlatformUserStatus.Active,
                EsGlobal = false
            };
            _db.PlatformUsers.Add(pu);
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.TenantUsers.IgnoreQueryFilters()
                .AnyAsync(u => u.PlatformUserId == pu.Id && u.TenantId == tenantId, ct))
        {
            // El telefono y el nombre viven en el colaborador: TenantUser no los tiene
            // y no vale la pena ampliarlo solo para esta pantalla.
            _db.TenantUsers.Add(new TenantUser
            {
                TenantId = tenantId,
                PlatformUserId = pu.Id,
                Email = col.Email,
                Status = PlatformUserStatus.Active
            });
            await _db.SaveChangesAsync(ct);
        }

        col.UsuarioId = pu.Id;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Personas de la TRD que aun no tienen cuenta de acceso.</summary>
    private IQueryable<ColaboradorDependencia> SinCuenta(Guid trdId) =>
        from c in _db.ColaboradoresDependencia
        join d in _db.Dependencias on c.DependenciaId equals d.Id
        where d.TrdId == trdId && c.UsuarioId == null
        select c;

    public async Task<int> ColaboradoresSinCuentaAsync(Guid trdId, CancellationToken ct = default) =>
        await SinCuenta(trdId).CountAsync(ct);

    public async Task<int> CrearCuentasPendientesAsync(Guid trdId, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return 0; }

        var pendientes = await SinCuenta(trdId).ToListAsync(ct);
        foreach (var col in pendientes)
        {
            await AsegurarCuentaAsync(col, tenantId, ct);
        }
        return pendientes.Count;
    }

    public async Task<bool> ActualizarColaboradorAsync(EditarColaboradorRequest req, Guid actor, CancellationToken ct = default)
    {
        var col = await _db.ColaboradoresDependencia.FirstOrDefaultAsync(c => c.Id == req.Id, ct);
        if (col is null) { return false; }

        var nombre = (req.Nombre ?? "").Trim();
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (nombre.Length == 0 || email.Length == 0)
        {
            throw new InvalidOperationException("Nombre y correo de la persona son obligatorios.");
        }
        if (await _db.ColaboradoresDependencia
                .AnyAsync(c => c.DependenciaId == col.DependenciaId && c.Email == email && c.Id != col.Id, ct))
        {
            throw new InvalidOperationException("Ya hay otra persona con ese correo en la dependencia.");
        }

        col.Nombre = nombre;
        col.Email = email;
        col.Telefono = string.IsNullOrWhiteSpace(req.Telefono) ? null : req.Telefono.Trim();
        col.Rol = string.IsNullOrWhiteSpace(req.Rol) ? col.Rol : req.Rol.Trim();
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TokenGeneradoDto?> GenerarTokenColaboradorAsync(Guid colaboradorId, string baseUrl, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var col = await _db.ColaboradoresDependencia.AsNoTracking().FirstOrDefaultAsync(c => c.Id == colaboradorId, ct);
        if (col is null) { throw new InvalidOperationException("La persona ya no esta asignada."); }
        var dep = await _db.Dependencias.AsNoTracking().FirstOrDefaultAsync(d => d.Id == col.DependenciaId, ct);
        if (dep is null) { throw new InvalidOperationException("La dependencia no existe."); }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _db.TokensDependencia.Add(new TokenDependencia
        {
            TenantId = tenantId,
            TrdId = dep.TrdId,
            DependenciaId = dep.Id,
            ColaboradorId = col.Id,
            Token = token,
            EmailColaborador = col.Email,
            ExpiraEn = DateTimeOffset.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync(ct);
        return new TokenGeneradoDto(token, $"{baseUrl.TrimEnd('/')}/trd-cliente?token={token}");
    }

    public async Task<bool> EliminarColaboradorAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var col = await _db.ColaboradoresDependencia.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (col is null) { return false; }
        _db.ColaboradoresDependencia.Remove(col);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DocumentoTrdDto>> DocumentosTrdAsync(
        Guid trdId, Guid? dependenciaId = null, string? texto = null, CancellationToken ct = default)
    {
        var q = _db.RespuestasTablaDocumental.AsNoTracking().Where(r => r.TrdId == trdId);
        if (dependenciaId is Guid dep) { q = q.Where(r => r.DependenciaId == dep); }

        var filas = await q
            .OrderBy(r => r.DependenciaId).ThenByDescending(r => r.FechaReg)
            .Select(r => new
            {
                r.Id, r.DependenciaId,
                DepCodigo = _db.Dependencias.Where(d => d.Id == r.DependenciaId).Select(d => d.Codigo).FirstOrDefault(),
                DepNombre = _db.Dependencias.Where(d => d.Id == r.DependenciaId).Select(d => d.NombreCargo).FirstOrDefault(),
                RaizDoc = _db.Dependencias.Where(d => d.Id == r.DependenciaId).Select(d => d.CodigoRaizDocumental).FirstOrDefault(),
                r.SerieId,
                SerieCod = _db.Series.Where(s => s.Id == r.SerieId).Select(s => s.Codigo).FirstOrDefault(),
                SerieNombre = _db.Series.Where(s => s.Id == r.SerieId).Select(s => s.Codigo + " - " + s.Nombre).FirstOrDefault(),
                r.SubserieId,
                SubCod = r.SubserieId == null ? null : _db.Subseries.Where(s => s.Id == r.SubserieId).Select(s => s.Codigo).FirstOrDefault(),
                SubserieNombre = r.SubserieId == null ? null
                    : _db.Subseries.Where(s => s.Id == r.SubserieId).Select(s => s.Codigo + " - " + s.Nombre).FirstOrDefault(),
                r.TipologiaId,
                TipologiaNombre = r.TipologiaId == null ? null
                    : _db.TipologiasDocumentales.Where(t => t.Id == r.TipologiaId).Select(t => t.Nombre).FirstOrDefault(),
                r.TiempoAg, r.TiempoAc, r.TiempoObserv,
                r.DispCt, r.DispS, r.DispE, r.DispD, r.DispObserv,
                r.Val1Admin, r.Val1Tecnica, r.Val1Legal, r.Val1Contable, r.Val1Fiscal,
                r.Val2Historica, r.Val2Cientifica, r.Val2Cultural,
                r.SinSubserie, r.Representativo, r.SerieDdhh, r.RelacionSig, r.Procedimiento,
                r.ProcQueEs, r.ProcElimina, r.ProcConserva,
                Formatos = _db.FormatosSerie.Where(f => f.RespuestaId == r.Id).Select(f => f.Formato).ToList(),
                r.FechaReg
            })
            .ToListAsync(ct);

        var busca = (texto ?? "").Trim();
        var resultado = filas.Select(r => new DocumentoTrdDto(
            r.Id, ComponerCodigo(r.RaizDoc, r.DepCodigo ?? "", r.SerieCod ?? "", r.SubCod),
            r.DependenciaId, r.DepCodigo ?? "", r.DepNombre ?? "",
            r.SerieId, r.SerieNombre ?? "", r.SubserieId, r.SubserieNombre,
            r.TipologiaId, r.TipologiaNombre,
            r.TiempoAg, r.TiempoAc, r.TiempoObserv,
            r.DispCt, r.DispS, r.DispE, r.DispD, r.DispObserv,
            r.Val1Admin, r.Val1Tecnica, r.Val1Legal, r.Val1Contable, r.Val1Fiscal,
            r.Val2Historica, r.Val2Cientifica, r.Val2Cultural,
            r.SinSubserie,
            !string.IsNullOrWhiteSpace(r.Representativo), r.SerieDdhh, !string.IsNullOrWhiteSpace(r.RelacionSig),
            r.Procedimiento,
            r.ProcQueEs, r.ProcElimina, r.ProcConserva,
            string.Join(", ", r.Formatos), r.FechaReg,
            r.Formatos.Any(f => f.Equals("Papel", StringComparison.OrdinalIgnoreCase)),
            string.Join(", ", r.Formatos.Where(f => !f.Equals("Papel", StringComparison.OrdinalIgnoreCase)))));

        if (busca.Length > 0)
        {
            resultado = resultado.Where(d =>
                Contiene(d.SerieNombre, busca) || Contiene(d.SubserieNombre, busca)
                || Contiene(d.TipologiaNombre, busca) || Contiene(d.DependenciaNombre, busca));
        }
        return resultado.ToList();
    }

    private static bool Contiene(string? campo, string busca) =>
        campo is not null && campo.Contains(busca, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Codigo de clasificacion documental (derivado): RAIZ.SERIE.COLA_SUBSERIE con separador punto.
    /// La raiz es el codigo raiz documental de la dependencia; si esta vacio, cae al codigo del
    /// organigrama. La cola de subserie es su codigo sin el prefijo de la serie (049-07 -> 07).
    /// </summary>
    private static string ComponerCodigo(string? raizDoc, string depCodigo, string serieCodigo, string? subserieCodigo)
    {
        var partes = new List<string>();
        var raiz = string.IsNullOrWhiteSpace(raizDoc) ? depCodigo : raizDoc.Trim();
        if (!string.IsNullOrWhiteSpace(raiz)) { partes.Add(raiz); }
        if (!string.IsNullOrWhiteSpace(serieCodigo)) { partes.Add(serieCodigo.Trim()); }
        var cola = ColaSubserie(subserieCodigo, serieCodigo);
        if (cola.Length > 0) { partes.Add(cola); }
        return string.Join(".", partes);
    }

    private static string ColaSubserie(string? subserieCodigo, string? serieCodigo)
    {
        if (string.IsNullOrWhiteSpace(subserieCodigo)) { return ""; }
        var s = subserieCodigo.Trim();
        if (!string.IsNullOrWhiteSpace(serieCodigo) && s.StartsWith(serieCodigo, StringComparison.OrdinalIgnoreCase))
        {
            s = s[serieCodigo!.Length..].TrimStart('-', '.', ' ');
        }
        return s;
    }

    public async Task<Guid?> GuardarDocumentoTrdAsync(GuardarDocumentoTrdRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }

        var trd = await _db.TablasRetencionDocumental.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TrdId, ct);
        if (trd is null) { throw new InvalidOperationException("La TRD no existe."); }
        // Misma regla que el lado cliente: una TRD cerrada no se toca.
        if (trd.Estado == "CERRADO") { throw new InvalidOperationException("La encuesta esta cerrada; no admite cambios."); }

        if (!await _db.Dependencias.AnyAsync(d => d.Id == req.DependenciaId && d.TrdId == req.TrdId, ct))
        { throw new InvalidOperationException("Elige una dependencia del organigrama."); }
        if (!await _db.Series.AnyAsync(s => s.Id == req.SerieId, ct))
        { throw new InvalidOperationException("Elige una serie."); }

        // El unique (trd, dependencia, serie, subserie, tipologia) impide repetir.
        var duplicado = await _db.RespuestasTablaDocumental.AnyAsync(
            r => r.TrdId == req.TrdId && r.DependenciaId == req.DependenciaId
                 && r.SerieId == req.SerieId && r.SubserieId == req.SubserieId
                 && r.TipologiaId == req.TipologiaId && r.Id != req.Id, ct);
        if (duplicado) { throw new InvalidOperationException("Esa dependencia ya declaro ese documento."); }

        RespuestaTablaDocumental fila;
        if (req.Id is Guid id)
        {
            fila = await _db.RespuestasTablaDocumental.FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw new InvalidOperationException("El documento ya no existe; recarga la tabla.");
        }
        else
        {
            fila = new RespuestaTablaDocumental
            {
                TenantId = tenantId, TrdId = req.TrdId,
                Extension = "{}", FechaReg = _clock.GetUtcNow(), CreadoPor = actor
            };
            _db.RespuestasTablaDocumental.Add(fila);
        }

        fila.DependenciaId = req.DependenciaId;
        fila.SerieId = req.SerieId;
        fila.SubserieId = req.SubserieId;
        fila.TipologiaId = req.TipologiaId;
        fila.SinSubserie = req.SubserieId is null;
        fila.TiempoAg = req.TiempoAg;
        fila.TiempoAc = req.TiempoAc;
        fila.TiempoObserv = req.TiempoObserv;
        fila.DispCt = req.DispCt; fila.DispS = req.DispS; fila.DispE = req.DispE; fila.DispD = req.DispD;
        fila.DispObserv = req.DispObserv;
        fila.Val1Admin = req.Val1Admin; fila.Val1Tecnica = req.Val1Tecnica; fila.Val1Legal = req.Val1Legal;
        fila.Val1Contable = req.Val1Contable; fila.Val1Fiscal = req.Val1Fiscal;
        fila.Val2Historica = req.Val2Historica; fila.Val2Cientifica = req.Val2Cientifica; fila.Val2Cultural = req.Val2Cultural;
        // REP y SIG se guardan como marca (SI/null); la entidad los modela como texto libre.
        fila.Representativo = req.Representativo ? "SI" : null;
        fila.SerieDdhh = req.SerieDdhh;
        fila.RelacionSig = req.RelacionSig ? "SI" : null;
        fila.Procedimiento = string.IsNullOrWhiteSpace(req.Procedimiento) ? null : req.Procedimiento!.Trim();

        await _db.SaveChangesAsync(ct);
        return fila.Id;
    }

    public async Task<bool> AlternarCampoDocumentoAsync(Guid respuestaId, string campo, Guid actor, CancellationToken ct = default)
    {
        var r = await _db.RespuestasTablaDocumental.FirstOrDefaultAsync(x => x.Id == respuestaId, ct);
        if (r is null) { throw new InvalidOperationException("El documento ya no existe; recarga la tabla."); }
        var estado = await _db.TablasRetencionDocumental.Where(t => t.Id == r.TrdId).Select(t => t.Estado).FirstOrDefaultAsync(ct);
        if (estado == "CERRADO") { throw new InvalidOperationException("La encuesta esta cerrada; no admite cambios."); }

        bool nuevo;
        switch (campo)
        {
            case nameof(RespuestaTablaDocumental.SinSubserie): nuevo = r.SinSubserie = !r.SinSubserie; break;
            case nameof(RespuestaTablaDocumental.DispCt): nuevo = r.DispCt = !r.DispCt; break;
            case nameof(RespuestaTablaDocumental.DispS): nuevo = r.DispS = !r.DispS; break;
            case nameof(RespuestaTablaDocumental.DispE): nuevo = r.DispE = !r.DispE; break;
            case nameof(RespuestaTablaDocumental.DispD): nuevo = r.DispD = !r.DispD; break;
            case nameof(RespuestaTablaDocumental.Val1Admin): nuevo = r.Val1Admin = !r.Val1Admin; break;
            case nameof(RespuestaTablaDocumental.Val1Tecnica): nuevo = r.Val1Tecnica = !r.Val1Tecnica; break;
            case nameof(RespuestaTablaDocumental.Val1Legal): nuevo = r.Val1Legal = !r.Val1Legal; break;
            case nameof(RespuestaTablaDocumental.Val1Contable): nuevo = r.Val1Contable = !r.Val1Contable; break;
            case nameof(RespuestaTablaDocumental.Val1Fiscal): nuevo = r.Val1Fiscal = !r.Val1Fiscal; break;
            case nameof(RespuestaTablaDocumental.Val2Historica): nuevo = r.Val2Historica = !r.Val2Historica; break;
            case nameof(RespuestaTablaDocumental.Val2Cientifica): nuevo = r.Val2Cientifica = !r.Val2Cientifica; break;
            case nameof(RespuestaTablaDocumental.Val2Cultural): nuevo = r.Val2Cultural = !r.Val2Cultural; break;
            case nameof(RespuestaTablaDocumental.SerieDdhh): nuevo = r.SerieDdhh = !r.SerieDdhh; break;
            // REP y SIG se modelan como texto libre; se alternan como marca SI/null.
            case nameof(RespuestaTablaDocumental.Representativo):
                r.Representativo = string.IsNullOrWhiteSpace(r.Representativo) ? "SI" : null; nuevo = r.Representativo is not null; break;
            case nameof(RespuestaTablaDocumental.RelacionSig):
                r.RelacionSig = string.IsNullOrWhiteSpace(r.RelacionSig) ? "SI" : null; nuevo = r.RelacionSig is not null; break;
            default: throw new InvalidOperationException($"Campo no alternable: {campo}.");
        }
        await _db.SaveChangesAsync(ct);
        return nuevo;
    }

    public async Task ActualizarNumeroDocumentoAsync(Guid respuestaId, string campo, decimal? valor, Guid actor, CancellationToken ct = default)
    {
        var r = await _db.RespuestasTablaDocumental.FirstOrDefaultAsync(x => x.Id == respuestaId, ct);
        if (r is null) { throw new InvalidOperationException("El documento ya no existe; recarga la tabla."); }
        var estado = await _db.TablasRetencionDocumental.Where(t => t.Id == r.TrdId).Select(t => t.Estado).FirstOrDefaultAsync(ct);
        if (estado == "CERRADO") { throw new InvalidOperationException("La encuesta esta cerrada; no admite cambios."); }
        if (valor is < 0) { throw new InvalidOperationException("El tiempo no puede ser negativo."); }
        switch (campo)
        {
            case nameof(RespuestaTablaDocumental.TiempoAg): r.TiempoAg = valor; break;
            case nameof(RespuestaTablaDocumental.TiempoAc): r.TiempoAc = valor; break;
            default: throw new InvalidOperationException($"Campo numerico no editable: {campo}.");
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ActualizarTextoDocumentoAsync(Guid respuestaId, string campo, string? valor, Guid actor, CancellationToken ct = default)
    {
        var r = await _db.RespuestasTablaDocumental.FirstOrDefaultAsync(x => x.Id == respuestaId, ct);
        if (r is null) { throw new InvalidOperationException("El documento ya no existe; recarga la tabla."); }
        var estado = await _db.TablasRetencionDocumental.Where(t => t.Id == r.TrdId).Select(t => t.Estado).FirstOrDefaultAsync(ct);
        if (estado == "CERRADO") { throw new InvalidOperationException("La encuesta esta cerrada; no admite cambios."); }
        var v = string.IsNullOrWhiteSpace(valor) ? null : valor!.Trim();
        switch (campo)
        {
            case nameof(RespuestaTablaDocumental.TiempoObserv): r.TiempoObserv = v; break;
            case nameof(RespuestaTablaDocumental.DispObserv): r.DispObserv = v; break;
            default: throw new InvalidOperationException($"Campo de texto no editable: {campo}.");
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ActualizarFormatosDocumentoAsync(Guid respuestaId, string formatosCsv, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return; }
        var r = await _db.RespuestasTablaDocumental.FirstOrDefaultAsync(x => x.Id == respuestaId, ct);
        if (r is null) { throw new InvalidOperationException("El documento ya no existe; recarga la tabla."); }
        var estado = await _db.TablasRetencionDocumental.Where(t => t.Id == r.TrdId).Select(t => t.Estado).FirstOrDefaultAsync(ct);
        if (estado == "CERRADO") { throw new InvalidOperationException("La encuesta esta cerrada; no admite cambios."); }

        // Se reemplaza la lista completa por lo que quedo escrito en la celda.
        var existentes = await _db.FormatosSerie.Where(f => f.RespuestaId == respuestaId).ToListAsync(ct);
        _db.FormatosSerie.RemoveRange(existentes);

        var nombres = (formatosCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var nombre in nombres)
        {
            _db.FormatosSerie.Add(new FormatoSerie { TenantId = tenantId, RespuestaId = respuestaId, Soporte = SoporteDe(nombre), Formato = nombre });
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>El unico soporte fisico es el papel; el resto son digitales (mismo criterio que el cliente).</summary>
    private static string SoporteDe(string formato) =>
        formato.Trim().Equals("Papel", StringComparison.OrdinalIgnoreCase) ? "PAPEL" : "DIGITAL";

    public async Task<int> GuardarEstructuraTrdAsync(GuardarEstructuraTrdRequest req, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return 0; }

        var trd = await _db.TablasRetencionDocumental.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TrdId, ct);
        if (trd is null) { throw new InvalidOperationException("La TRD no existe."); }
        // Misma regla que el resto: una TRD cerrada no se toca.
        if (trd.Estado == "CERRADO") { throw new InvalidOperationException("La encuesta esta cerrada; no admite cambios."); }

        if (!await _db.Dependencias.AnyAsync(d => d.Id == req.DependenciaId && d.TrdId == req.TrdId, ct))
        { throw new InvalidOperationException("Elige una dependencia del organigrama."); }
        if (!await _db.Series.AnyAsync(s => s.Id == req.SerieId, ct))
        { throw new InvalidOperationException("Elige una serie."); }

        var tipologias = req.TipologiaIds.Distinct().ToList();
        if (tipologias.Count == 0) { throw new InvalidOperationException("Marca al menos una tipologia."); }

        var creadas = 0;
        foreach (var tipologiaId in tipologias)
        {
            // El unique (trd, dependencia, serie, subserie, tipologia) impide repetir:
            // si ya esta declarada, se salta (igual que el lado cliente).
            var yaEsta = await _db.RespuestasTablaDocumental.AnyAsync(
                r => r.TrdId == req.TrdId && r.DependenciaId == req.DependenciaId
                     && r.SerieId == req.SerieId && r.SubserieId == req.SubserieId
                     && r.TipologiaId == tipologiaId, ct);
            if (yaEsta) { continue; }

            var fila = new RespuestaTablaDocumental
            {
                TenantId = tenantId, TrdId = req.TrdId, DependenciaId = req.DependenciaId,
                SerieId = req.SerieId, SubserieId = req.SubserieId, TipologiaId = tipologiaId,
                SinSubserie = req.SubserieId is null,
                TiempoAg = req.TiempoAg, TiempoAc = req.TiempoAc, TiempoObserv = req.TiempoObserv,
                DispCt = req.DispCt, DispS = req.DispS, DispE = req.DispE, DispD = req.DispD,
                DispObserv = req.DispObserv,
                Val1Admin = req.Val1Admin, Val1Tecnica = req.Val1Tecnica, Val1Legal = req.Val1Legal,
                Val1Contable = req.Val1Contable, Val1Fiscal = req.Val1Fiscal,
                Val2Historica = req.Val2Historica, Val2Cientifica = req.Val2Cientifica, Val2Cultural = req.Val2Cultural,
                Extension = "{}", FechaReg = _clock.GetUtcNow(), CreadoPor = actor
            };

            // Formatos de esta tipologia: se agregan por la navegacion, EF fija el
            // RespuestaId al guardar (no hace falta un Id previo).
            if (req.FormatosPorTipologia.TryGetValue(tipologiaId, out var formatos) && formatos is not null)
            {
                foreach (var f in formatos.Where(x => !string.IsNullOrWhiteSpace(x))
                                          .Select(x => x.Trim())
                                          .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    fila.Formatos.Add(new FormatoSerie { TenantId = tenantId, Soporte = SoporteDe(f), Formato = f });
                }
            }

            _db.RespuestasTablaDocumental.Add(fila);
            creadas++;
        }

        await _db.SaveChangesAsync(ct);
        return creadas;
    }

    public async Task<bool> EliminarDocumentoTrdAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var fila = await _db.RespuestasTablaDocumental.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (fila is null) { return false; }
        _db.RespuestasTablaDocumental.Remove(fila);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TokenGeneradoDto?> GenerarTokenAsync(Guid dependenciaId, string? email, string baseUrl, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        var dep = await _db.Dependencias.FirstOrDefaultAsync(d => d.Id == dependenciaId, ct);
        if (dep is null) { throw new InvalidOperationException("La dependencia no existe."); }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        _db.TokensDependencia.Add(new TokenDependencia
        {
            TenantId = tenantId, TrdId = dep.TrdId, DependenciaId = dependenciaId,
            Token = token, EmailColaborador = string.IsNullOrWhiteSpace(email) ? null : email!.Trim(),
            ExpiraEn = _clock.GetUtcNow().AddDays(7)
        });
        await _db.SaveChangesAsync(ct);
        var url = $"{baseUrl.TrimEnd('/')}/trd-cliente?token={token}";
        return new TokenGeneradoDto(token, url);
    }

    // ----- Catalogo -----
    public async Task<IReadOnlyList<SegmentoDto>> ListSegmentosAsync(CancellationToken ct = default) =>
        await _db.Segmentos.AsNoTracking().OrderBy(x => x.Codigo).Select(x => new SegmentoDto(x.Id, x.Codigo, x.Nombre)).ToListAsync(ct);

    public async Task<SegmentoDto?> CrearSegmentoAsync(string codigo, string nombre, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        codigo = (codigo ?? "").Trim(); nombre = (nombre ?? "").Trim();
        if (codigo.Length == 0 || nombre.Length == 0) { throw new InvalidOperationException("Codigo y nombre del segmento son obligatorios."); }
        if (await _db.Segmentos.AnyAsync(x => x.Codigo == codigo, ct)) { throw new InvalidOperationException($"Ya existe el segmento '{codigo}'."); }
        var e = new Segmento { TenantId = tenantId, Codigo = codigo, Nombre = nombre };
        _db.Segmentos.Add(e);
        await _db.SaveChangesAsync(ct);
        return new SegmentoDto(e.Id, e.Codigo, e.Nombre);
    }

    public async Task<IReadOnlyList<SerieDto>> ListSeriesAsync(CancellationToken ct = default) =>
        await _db.Series.AsNoTracking().OrderBy(x => x.Codigo)
            .Select(x => new SerieDto(x.Id, x.Codigo, x.Nombre, x.Activo, _db.Subseries.Count(s => s.SerieId == x.Id))).ToListAsync(ct);

    public async Task<SerieDto?> CrearSerieAsync(string codigo, string nombre, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        codigo = (codigo ?? "").Trim(); nombre = (nombre ?? "").Trim();
        if (codigo.Length == 0 || nombre.Length == 0) { throw new InvalidOperationException("Codigo y nombre de la serie son obligatorios."); }
        if (await _db.Series.AnyAsync(x => x.Codigo == codigo, ct)) { throw new InvalidOperationException($"Ya existe la serie '{codigo}'."); }
        var e = new Serie { TenantId = tenantId, Codigo = codigo, Nombre = nombre, Activo = true };
        _db.Series.Add(e);
        await _db.SaveChangesAsync(ct);
        return new SerieDto(e.Id, e.Codigo, e.Nombre, e.Activo, 0);
    }

    public async Task<IReadOnlyList<SubserieDto>> ListSubseriesAsync(Guid serieId, CancellationToken ct = default) =>
        await _db.Subseries.AsNoTracking().Where(x => x.SerieId == serieId).OrderBy(x => x.Codigo)
            .Select(x => new SubserieDto(x.Id, x.SerieId, x.Codigo, x.Nombre)).ToListAsync(ct);

    public async Task<SubserieDto?> CrearSubserieAsync(Guid serieId, string codigo, string nombre, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        codigo = (codigo ?? "").Trim(); nombre = (nombre ?? "").Trim();
        if (codigo.Length == 0 || nombre.Length == 0) { throw new InvalidOperationException("Codigo y nombre de la subserie son obligatorios."); }
        if (!await _db.Series.AnyAsync(s => s.Id == serieId, ct)) { throw new InvalidOperationException("La serie no existe."); }
        if (await _db.Subseries.AnyAsync(x => x.SerieId == serieId && x.Codigo == codigo, ct)) { throw new InvalidOperationException($"Ya existe la subserie '{codigo}' en esa serie."); }
        var e = new Subserie { TenantId = tenantId, SerieId = serieId, Codigo = codigo, Nombre = nombre };
        _db.Subseries.Add(e);
        await _db.SaveChangesAsync(ct);
        return new SubserieDto(e.Id, e.SerieId, e.Codigo, e.Nombre);
    }

    public async Task<IReadOnlyList<TipologiaDocDto>> ListTipologiasAsync(CancellationToken ct = default) =>
        await _db.TipologiasDocumentales.AsNoTracking().OrderBy(x => x.Codigo)
            .Select(x => new TipologiaDocDto(x.Id, x.SerieId, x.SubserieId, x.Codigo, x.Nombre, x.Tipo, x.Activo)).ToListAsync(ct);

    public async Task<TipologiaDocDto?> CrearTipologiaAsync(Guid? serieId, Guid? subserieId, string codigo, string nombre, string tipo, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return null; }
        codigo = (codigo ?? "").Trim(); nombre = (nombre ?? "").Trim();
        tipo = string.IsNullOrWhiteSpace(tipo) ? "GENERAL" : tipo.Trim().ToUpperInvariant();
        if (codigo.Length == 0 || nombre.Length == 0) { throw new InvalidOperationException("Codigo y nombre de la tipologia son obligatorios."); }
        if (subserieId is Guid ss && !await _db.Subseries.AnyAsync(x => x.Id == ss, ct)) { throw new InvalidOperationException("La subserie no existe."); }
        if (serieId is Guid se && !await _db.Series.AnyAsync(x => x.Id == se, ct)) { throw new InvalidOperationException("La serie no existe."); }
        if (await _db.TipologiasDocumentales.AnyAsync(x => x.Codigo == codigo, ct)) { throw new InvalidOperationException($"Ya existe la tipologia '{codigo}'."); }
        var e = new TipologiaDocumental { TenantId = tenantId, SerieId = serieId, SubserieId = subserieId, Codigo = codigo, Nombre = nombre, Tipo = tipo, Activo = true };
        _db.TipologiasDocumentales.Add(e);
        await _db.SaveChangesAsync(ct);
        return new TipologiaDocDto(e.Id, e.SerieId, e.SubserieId, e.Codigo, e.Nombre, e.Tipo, e.Activo);
    }

    public async Task<int> SeedDemoAsync(string baseUrl, Guid actor, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId) { return 0; }
        if (await _db.TablasRetencionDocumental.AnyAsync(ct)) { return 0; }

        // Segmento + serie/subserie/tipologia + TRD con organigrama + token de ejemplo.
        var seg = new Segmento { TenantId = tenantId, Codigo = "GEN", Nombre = "General" };
        _db.Segmentos.Add(seg);
        var serie = new Serie { TenantId = tenantId, Codigo = "100", Nombre = "Actas", Activo = true };
        _db.Series.Add(serie);
        var sub = new Subserie { TenantId = tenantId, Serie = serie, Codigo = "100-10", Nombre = "Actas de comite" };
        _db.Subseries.Add(sub);
        _db.TipologiasDocumentales.Add(new TipologiaDocumental { TenantId = tenantId, Subserie = sub, Codigo = "100-10-01", Nombre = "Acta de comite directivo", Tipo = "GENERAL", Activo = true });

        var trd = new TablaRetencionDocumental { TenantId = tenantId, Consecutivo = "TRD-0001", Titulo = "TRD Institucional 2026", Estado = "DESARROLLO", Segmento = seg, CreadoPor = actor };
        _db.TablasRetencionDocumental.Add(trd);
        var raiz = new Dependencia { TenantId = tenantId, Trd = trd, Nivel = 0, Orden = 1, NombreCargo = "Direccion General", Codigo = "100", Estado = "ACTIVO" };
        _db.Dependencias.Add(raiz);
        _db.Dependencias.Add(new Dependencia { TenantId = tenantId, Trd = trd, Padre = raiz, Nivel = 1, Orden = 1, NombreCargo = "Secretaria General", Codigo = "110", Estado = "ACTIVO" });
        await _db.SaveChangesAsync(ct);
        return 1;
    }
}
