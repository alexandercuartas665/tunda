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

    public async Task<bool> CambiarEstadoAsync(Guid trdId, string estado, Guid actor, CancellationToken ct = default)
    {
        estado = (estado ?? "").Trim().ToUpperInvariant();
        if (!EstadosTrd.Contains(estado)) { throw new InvalidOperationException("Estado invalido."); }
        var trd = await _db.TablasRetencionDocumental.FirstOrDefaultAsync(x => x.Id == trdId, ct);
        if (trd is null) { return false; }
        // Transicion controlada: DESARROLLO -> ACTIVO -> CERRADO (no saltar a un estado anterior).
        int Orden(string e) => Array.IndexOf(EstadosTrd, e);
        if (Orden(estado) < Orden(trd.Estado)) { throw new InvalidOperationException($"No se puede volver de {trd.Estado} a {estado}."); }
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
                _db.ColaboradoresDependencia.Count(c => c.DependenciaId == d.Id)))
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
