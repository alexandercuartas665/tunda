using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class TrdClienteService : ITrdClienteService
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public TrdClienteService(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<TokenSesionDto?> ResolverTokenAsync(string token, CancellationToken ct = default)
    {
        token = (token ?? "").Trim();
        if (token.Length == 0) { return null; }
        var tok = await _db.TokensDependencia.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(t => t.Token == token, ct);
        if (tok is null) { return null; }
        var trd = await _db.TablasRetencionDocumental.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(t => t.Id == tok.TrdId, ct);
        var dep = await _db.Dependencias.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(d => d.Id == tok.DependenciaId, ct);
        if (trd is null || dep is null) { return null; }
        var expirado = tok.ExpiraEn is DateTimeOffset exp && exp < _clock.GetUtcNow();
        var soloLectura = expirado || trd.Estado == "CERRADO" || dep.Estado == "CERRADO";
        return new TokenSesionDto(tok.TenantId, trd.Id, trd.Consecutivo, trd.Titulo, trd.Estado,
            dep.Id, dep.NombreCargo, dep.Estado, soloLectura, expirado);
    }

    public async Task<IReadOnlyList<RespuestaTrdDto>> ListarRespuestasAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<RespuestaTrdDto>(); }
        var rows = await _db.RespuestasTablaDocumental.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.TenantId == s.TenantId && r.TrdId == s.TrdId && r.DependenciaId == s.DependenciaId)
            .OrderByDescending(r => r.FechaReg)
            .Select(r => new
            {
                r.Id,
                Serie = _db.Series.IgnoreQueryFilters().Where(x => x.Id == r.SerieId).Select(x => x.Codigo + " - " + x.Nombre).FirstOrDefault(),
                Subserie = r.SubserieId == null ? null : _db.Subseries.IgnoreQueryFilters().Where(x => x.Id == r.SubserieId).Select(x => x.Codigo + " - " + x.Nombre).FirstOrDefault(),
                Tipologia = r.TipologiaId == null ? null : _db.TipologiasDocumentales.IgnoreQueryFilters().Where(x => x.Id == r.TipologiaId).Select(x => x.Codigo + " - " + x.Nombre).FirstOrDefault(),
                r.TiempoAg, r.TiempoAc, r.DispCt, r.DispS, r.DispE, r.DispD,
                r.Val1Admin, r.Val1Legal, r.Val2Historica
            })
            .ToListAsync(ct);

        return rows.Select(r => new RespuestaTrdDto(
            r.Id, r.Serie ?? "", r.Subserie, r.Tipologia, r.TiempoAg, r.TiempoAc,
            string.Join("/", new[] { r.DispCt ? "CT" : null, r.DispS ? "S" : null, r.DispE ? "E" : null, r.DispD ? "D" : null }.Where(x => x != null)),
            string.Join(", ", new[] { r.Val1Admin ? "Adm" : null, r.Val1Legal ? "Legal" : null, r.Val2Historica ? "Historica" : null }.Where(x => x != null))))
            .ToList();
    }

    public async Task<Guid?> GuardarRespuestaAsync(string token, GuardarRespuestaCommand cmd, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { throw new InvalidOperationException("Token invalido."); }
        if (s.Expirado) { throw new InvalidOperationException("El token expiro."); }
        if (s.SoloLectura) { throw new InvalidOperationException("La TRD o la dependencia estan CERRADAS; no se puede diligenciar."); }
        if (!await _db.Series.IgnoreQueryFilters().AnyAsync(x => x.Id == cmd.SerieId && x.TenantId == s.TenantId, ct))
        { throw new InvalidOperationException("La serie no existe."); }

        var entity = new RespuestaTablaDocumental
        {
            TenantId = s.TenantId, TrdId = s.TrdId, DependenciaId = s.DependenciaId,
            SerieId = cmd.SerieId, SubserieId = cmd.SubserieId, TipologiaId = cmd.TipologiaId,
            SinSubserie = cmd.SinSubserie || cmd.SubserieId == null,
            TiempoAg = cmd.TiempoAg, TiempoAc = cmd.TiempoAc,
            DispCt = cmd.DispCt, DispS = cmd.DispS, DispE = cmd.DispE, DispD = cmd.DispD,
            Val1Admin = cmd.Val1Admin, Val1Legal = cmd.Val1Legal, Val2Historica = cmd.Val2Historica,
            Extension = "{}", FechaReg = _clock.GetUtcNow(), CreadoPor = Guid.Empty
        };
        _db.RespuestasTablaDocumental.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> EliminarRespuestaAsync(string token, Guid respuestaId, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null || s.SoloLectura) { return false; }
        var r = await _db.RespuestasTablaDocumental.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == respuestaId && x.TenantId == s.TenantId, ct);
        if (r is null) { return false; }
        _db.RespuestasTablaDocumental.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OpcionDto>> SeriesAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<OpcionDto>(); }
        return await _db.Series.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == s.TenantId && x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OpcionDto>> TipologiasAsync(string token, CancellationToken ct = default)
    {
        var s = await ResolverTokenAsync(token, ct);
        if (s is null) { return Array.Empty<OpcionDto>(); }
        return await _db.TipologiasDocumentales.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == s.TenantId && x.Activo).OrderBy(x => x.Codigo)
            .Select(x => new OpcionDto(x.Id, x.Codigo + " - " + x.Nombre)).ToListAsync(ct);
    }
}
