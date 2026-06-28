using System.Security.Cryptography;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class TrdAdminService : ITrdAdminService
{
    private static readonly string[] EstadosTrd = ["DESARROLLO", "ACTIVO", "CERRADO"];

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public TrdAdminService(IApplicationDbContext db, ITenantContext tenant, TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TrdDto>> ListarTrdAsync(CancellationToken ct = default) =>
        await _db.TablasRetencionDocumental.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Select(x => new TrdDto(x.Id, x.Consecutivo, x.Titulo, x.Estado,
                x.SegmentoId == null ? null : _db.Segmentos.Where(s => s.Id == x.SegmentoId).Select(s => s.Nombre).FirstOrDefault(),
                x.FechaInicio, x.FechaFin,
                _db.Dependencias.Count(d => d.TrdId == x.Id)))
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
            .Select(d => new DependenciaDto(d.Id, d.PadreId, d.Nivel, d.Orden, d.NombreCargo, d.Codigo, d.Estado))
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

    public async Task<bool> EliminarDependenciaAsync(Guid id, Guid actor, CancellationToken ct = default)
    {
        var dep = await _db.Dependencias.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dep is null) { return false; }
        _db.Dependencias.Remove(dep);
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
