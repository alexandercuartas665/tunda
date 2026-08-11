using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DokTrino.Application.Admin;
using DokTrino.Application.Common;
using DokTrino.Domain.Entities;
using DokTrino.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DokTrino.Application.Tenancy;

public sealed class AiInferenceService : IAiInferenceService
{
    private const int MaxToolRounds = 6;

    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IAiProviderClient _client;
    private readonly IAiUsageService _usage;
    private readonly IEnumerable<IAgentToolset> _toolsets;

    public AiInferenceService(IApplicationDbContext db, ISecretProtector secretProtector, IAiProviderClient client,
        IAiUsageService usage, IEnumerable<IAgentToolset> toolsets)
    {
        _db = db;
        _secretProtector = secretProtector;
        _client = client;
        _usage = usage;
        _toolsets = toolsets;
    }

    public async Task<AiChatResult> ConsultarConHerramientasAsync(string systemPrompt, string pregunta, string source = "clasificador", CancellationToken cancellationToken = default)
    {
        // Primer proveedor habilitado con clave. Sin proveedor, el modulo cae a su heuristica.
        var providerCfg = await _db.AiProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsEnabled && c.ApiKeyEncrypted != null, cancellationToken);
        if (providerCfg is null)
        {
            return new AiChatResult(false, null, "No hay un proveedor de IA habilitado. Configuralo en Servidores de IA.");
        }

        string apiKey;
        try { apiKey = _secretProtector.Unprotect(providerCfg.ApiKeyEncrypted!); }
        catch { return new AiChatResult(false, null, "La API key esta cifrada con una version anterior. Vuelve a guardarla en Servidores de IA."); }

        var meta = AiProviderCatalog.For(providerCfg.Provider);
        var model = !string.IsNullOrWhiteSpace(providerCfg.Model) ? providerCfg.Model! : meta.DefaultModel;

        var quota = await _usage.GetQuotaAsync(cancellationToken);
        if (quota.Exceeded && quota.Hard)
        {
            return new AiChatResult(false, null, $"Alcanzaste el limite de tokens de IA de tu plan este mes ({quota.MonthlyLimitTokens:N0}).");
        }

        var specs = _toolsets.SelectMany(t => t.GetSpecs()).ToList();
        var ownerByTool = _toolsets
            .SelectMany(ts => ts.GetSpecs().Select(s => (s.Name, ts)))
            .ToDictionary(x => x.Name, x => x.ts);

        var messages = new List<AiToolMessage> { new("user", pregunta) };
        int totalIn = 0, totalOut = 0;
        string? lastText = null;

        for (var round = 1; round <= MaxToolRounds; round++)
        {
            var completion = await _client.CompleteWithToolsAsync(
                providerCfg.Provider, apiKey, providerCfg.BaseUrl, model, systemPrompt, messages, specs, cancellationToken);
            totalIn += completion.InputTokens;
            totalOut += completion.OutputTokens;
            if (!completion.Ok) { return new AiChatResult(false, null, completion.Error); }
            lastText = completion.Text ?? lastText;

            // Sin herramientas pedidas: respuesta final.
            if (completion.ToolCalls.Count == 0)
            {
                await _usage.RecordAsync(null, providerCfg.Provider, model, totalIn, totalOut, source, true, cancellationToken);
                return new AiChatResult(true, completion.Text, null, totalIn, totalOut);
            }

            messages.Add(new AiToolMessage("assistant", completion.Text, completion.ToolCalls));
            foreach (var call in completion.ToolCalls)
            {
                var owner = ownerByTool.GetValueOrDefault(call.Name);
                var exec = owner is null
                    ? new AgentToolResult($"{{\"ok\":false,\"error\":\"Herramienta {call.Name} no disponible\"}}", false)
                    : await owner.ExecuteAsync(call.Name, call.ArgumentsJson, cancellationToken);
                messages.Add(new AiToolMessage("tool", exec.Json, null, call.Id, call.Name));
            }
        }

        // Agoto las rondas de herramientas sin cerrar.
        await _usage.RecordAsync(null, providerCfg.Provider, model, totalIn, totalOut, source, true, cancellationToken);
        return new AiChatResult(true, lastText ?? "El analisis no pudo completarse; intenta reformular la pregunta.", null, totalIn, totalOut);
    }

    public async Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null, CancellationToken cancellationToken = default)
    {
        var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);
        if (agent is null) { return new AiChatResult(false, null, "El agente no existe."); }

        // La cuenta del proveedor (API key, modelo, base url) la define el Super Admin (config global).
        var providerCfg = await _db.AiProviderConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == agent.Provider, cancellationToken);
        if (providerCfg is null || !providerCfg.IsEnabled || string.IsNullOrWhiteSpace(providerCfg.ApiKeyEncrypted))
        {
            return new AiChatResult(false, null, $"El proveedor {agent.Provider} no esta habilitado en la plataforma.");
        }

        string apiKey;
        try { apiKey = _secretProtector.Unprotect(providerCfg.ApiKeyEncrypted); }
        catch { return new AiChatResult(false, null, "La API key del proveedor esta cifrada con una version anterior. Vuelve a guardarla en Servidores de IA."); }

        var meta = AiProviderCatalog.For(agent.Provider);
        var model = !string.IsNullOrWhiteSpace(agent.Model) ? agent.Model!
            : !string.IsNullOrWhiteSpace(providerCfg.Model) ? providerCfg.Model!
            : meta.DefaultModel;

        if (turns.Count == 0) { return new AiChatResult(false, null, "Escribe un mensaje para probar el agente."); }

        // Control de cupo: si el plan tiene limite duro y ya se agoto el mes, no se ejecuta.
        // (Las consultas a BD se hacen en serie sobre el DbContext scoped: cupo -> prompt -> proveedor.)
        var quota = await _usage.GetQuotaAsync(cancellationToken);
        if (quota.Exceeded && quota.Hard)
        {
            return new AiChatResult(false, null, $"Alcanzaste el limite de tokens de IA de tu plan este mes ({quota.MonthlyLimitTokens:N0}). Actualiza tu plan para seguir usando los agentes.");
        }

        // Recursos del agente (todos los tipos): se usan para componer el prompt y para resolver adjuntos.
        var resources = await _db.AiAgentResources.AsNoTracking()
            .Where(r => r.AgentId == agentId)
            .OrderBy(r => r.SortOrder)
            .Select(r => new AiChatAttachment(r.Name, r.ResourceType, r.FileUrl, r.FileName, r.Detail))
            .ToListAsync(cancellationToken);

        var systemPrompt = await BuildSystemPrompt(agentId, systemPromptOverride ?? agent.SystemPrompt, resources, cancellationToken);

        var result = await _client.CompleteAsync(agent.Provider, apiKey, providerCfg.BaseUrl, model, systemPrompt, turns, cancellationToken);

        // Todo consumo de IA del tenant pasa por el modulo de tokens (incluido el chat de prueba).
        if (result.Ok)
        {
            await _usage.RecordAsync(agent.Id, agent.Provider, model, result.InputTokens, result.OutputTokens, "test", true, cancellationToken);
        }

        // Entrega de recursos: el modelo marca [[enviar: Nombre]] y aqui adjuntamos el recurso (archivo o texto).
        if (result.Ok && !string.IsNullOrEmpty(result.Text))
        {
            var (cleanText, attachments) = ExtractAttachments(result.Text!, resources);
            return result with { Text = cleanText, Attachments = attachments };
        }

        return result;
    }

    public async Task<ProcedimientoGeneradoDto> GenerarProcedimientoAsync(Guid respuestaId, CancellationToken ct = default)
    {
        var r = await _db.RespuestasTablaDocumental.FirstOrDefaultAsync(x => x.Id == respuestaId, ct);
        if (r is null) { return ProcedimientoGeneradoDto.Fail("El documento ya no existe; recarga la tabla."); }

        var providerCfg = await _db.AiProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsEnabled && c.ApiKeyEncrypted != null, ct);
        if (providerCfg is null) { return ProcedimientoGeneradoDto.Fail("No hay un proveedor de IA habilitado. Configuralo en Servidores de IA."); }

        string apiKey;
        try { apiKey = _secretProtector.Unprotect(providerCfg.ApiKeyEncrypted!); }
        catch { return ProcedimientoGeneradoDto.Fail("La API key esta cifrada con una version anterior. Vuelve a guardarla en Servidores de IA."); }

        var meta = AiProviderCatalog.For(providerCfg.Provider);
        var model = !string.IsNullOrWhiteSpace(providerCfg.Model) ? providerCfg.Model! : meta.DefaultModel;

        var quota = await _usage.GetQuotaAsync(ct);
        if (quota.Exceeded && quota.Hard)
        { return ProcedimientoGeneradoDto.Fail($"Alcanzaste el limite de tokens de IA de tu plan este mes ({quota.MonthlyLimitTokens:N0})."); }

        var prompts = await AsegurarPromptsProcedimientoAsync(r.TenantId, providerCfg.Provider, ct);
        var plantilla = await LlenarPlantillaAsync(prompts[ProcedimientoPrompts.NPlantilla], r, ct);

        // 1) QUE ES
        var quees = await PasoAsync(providerCfg.Provider, apiKey, providerCfg.BaseUrl, model,
            prompts[ProcedimientoPrompts.NQueEs].Replace("@@PLANTILLA@@", plantilla), ct);
        if (!quees.Ok) { return ProcedimientoGeneradoDto.Fail(quees.Error); }

        var textos = new List<string> { quees.Texto };
        string? elimina = null, conserva = null;

        // 2) POR QUE ELIMINA (solo si la disposicion es Eliminacion)
        if (r.DispE)
        {
            var e = await PasoAsync(providerCfg.Provider, apiKey, providerCfg.BaseUrl, model,
                prompts[ProcedimientoPrompts.NElimina].Replace("@@PLANTILLA@@", plantilla), ct);
            if (e.Ok) { elimina = e.Texto; textos.Add(e.Texto); }
        }

        // 3) POR QUE CONSERVA (solo si es Conservacion Total)
        if (r.DispCt)
        {
            var c = await PasoAsync(providerCfg.Provider, apiKey, providerCfg.BaseUrl, model,
                prompts[ProcedimientoPrompts.NConserva].Replace("@@PLANTILLA@@", plantilla), ct);
            if (c.Ok) { conserva = c.Texto; textos.Add(c.Texto); }
        }

        // 4) UNIFICADOR
        var uniPrompt = prompts[ProcedimientoPrompts.NUnificador]
            .Replace("@@PLANTILLA@@", plantilla)
            .Replace("@@TEXTOS@@", string.Join("\n\n", textos));
        var uni = await PasoAsync(providerCfg.Provider, apiKey, providerCfg.BaseUrl, model, uniPrompt, ct);
        var unificado = uni.Ok ? uni.Texto : string.Join("\n\n", textos);

        r.Procedimiento = unificado;
        await _db.SaveChangesAsync(ct);

        return new ProcedimientoGeneradoDto(true, null, quees.Texto, elimina, conserva, unificado);
    }

    // Siembra (una vez por tenant) el agente "Procedimientos TRD" con sus 5 prompts y los devuelve por nombre.
    private async Task<IReadOnlyDictionary<string, string>> AsegurarPromptsProcedimientoAsync(Guid tenantId, AiProvider provider, CancellationToken ct)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Role == ProcedimientoPrompts.AgenteRol, ct);
        if (agent is null)
        {
            agent = new AiAgent
            {
                TenantId = tenantId, Name = ProcedimientoPrompts.AgenteNombre, Role = ProcedimientoPrompts.AgenteRol,
                Provider = provider, SystemPrompt = ProcedimientoPrompts.AgenteDescripcion, IsActive = true
            };
            _db.AiAgents.Add(agent);
            short orden = 1;
            foreach (var (nombre, cuerpo) in ProcedimientoPrompts.Todos())
            {
                _db.AiAgentPrompts.Add(new AiAgentPrompt { TenantId = tenantId, AgentId = agent.Id, Name = nombre, Rule = "", Body = cuerpo, SortOrder = orden++ });
            }
            await _db.SaveChangesAsync(ct);
        }

        var prompts = await _db.AiAgentPrompts.AsNoTracking()
            .Where(p => p.AgentId == agent.Id)
            .ToDictionaryAsync(p => p.Name, p => p.Body, ct);
        // Si el admin borro algun prompt, se completa con el de base para no romper la cadena.
        foreach (var (nombre, cuerpo) in ProcedimientoPrompts.Todos())
        { if (!prompts.ContainsKey(nombre)) { prompts[nombre] = cuerpo; } }
        return prompts;
    }

    private async Task<string> LlenarPlantillaAsync(string plantilla, RespuestaTablaDocumental r, CancellationToken ct)
    {
        var serie = await _db.Series.AsNoTracking().Where(s => s.Id == r.SerieId).Select(s => s.Codigo + " - " + s.Nombre).FirstOrDefaultAsync(ct) ?? "";
        var subserie = r.SubserieId == null ? "SIN SUBSERIE"
            : (await _db.Subseries.AsNoTracking().Where(s => s.Id == r.SubserieId).Select(s => s.Codigo + " - " + s.Nombre).FirstOrDefaultAsync(ct) ?? "SIN SUBSERIE");
        var gerencia = await _db.Dependencias.AsNoTracking().Where(d => d.Id == r.DependenciaId).Select(d => d.Codigo + " - " + d.NombreCargo).FirstOrDefaultAsync(ct) ?? "";

        static string SiNo(bool b) => b ? "Si" : "No";
        static string Anios(decimal? n) => n is decimal v ? v.ToString("0.##", CultureInfo.InvariantCulture) + " anios" : "(no definido)";

        return plantilla
            .Replace("@@SERIE@@", serie)
            .Replace("@@SUBSERIE@@", subserie)
            .Replace("@@GERENCIA@@", gerencia)
            .Replace("@@Archivo gestion@@", Anios(r.TiempoAg))
            .Replace("@@Archivo central@@", Anios(r.TiempoAc))
            .Replace("@@TIEMPOBSE@@", string.IsNullOrWhiteSpace(r.TiempoObserv) ? "(sin observacion)" : r.TiempoObserv!)
            .Replace("@@Conservacion Total@@", SiNo(r.DispCt))
            .Replace("@@Seleccion@@", SiNo(r.DispS))
            .Replace("@@Eliminacion@@", SiNo(r.DispE))
            .Replace("@@DISPOBS@@", string.IsNullOrWhiteSpace(r.DispObserv) ? "(sin observacion)" : r.DispObserv!)
            .Replace("@@REPPAL@@", SiNo(!string.IsNullOrWhiteSpace(r.Representativo)))
            .Replace("@@DDHH@@", SiNo(r.SerieDdhh))
            .Replace("@@Administrativo@@", SiNo(r.Val1Admin))
            .Replace("@@Tecnico@@", SiNo(r.Val1Tecnica))
            .Replace("@@Legal@@", SiNo(r.Val1Legal))
            .Replace("@@Contable@@", SiNo(r.Val1Contable))
            .Replace("@@Fiscal@@", SiNo(r.Val1Fiscal))
            .Replace("@@Historico@@", SiNo(r.Val2Historica))
            .Replace("@@Cientifico@@", SiNo(r.Val2Cientifica))
            .Replace("@@Cultural@@", SiNo(r.Val2Cultural));
    }

    private async Task<(bool Ok, string Texto, string Error)> PasoAsync(AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt, CancellationToken ct)
    {
        var res = await _client.CompleteAsync(provider, apiKey, baseUrl, model, systemPrompt,
            new[] { new AiChatTurn("user", "Genera unicamente el XML solicitado.") }, ct);
        if (!res.Ok) { return (false, "", res.Error ?? "Fallo la generacion con el modelo."); }
        await _usage.RecordAsync(null, provider, model, res.InputTokens, res.OutputTokens, "procedimiento", true, ct);
        return (true, ExtraerProcedimiento(res.Text ?? ""), "");
    }

    // Saca el contenido de <PROCEDIMIENTO>...</PROCEDIMIENTO> (tolera espacios en el cierre); si no hay XML, usa el texto tal cual.
    private static string ExtraerProcedimiento(string xml)
    {
        var m = Regex.Match(xml, @"<\s*PROCEDIMIENTO\s*>(.*?)<\s*/\s*PROCEDIMIENTO\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var txt = m.Success ? m.Groups[1].Value : xml;
        return System.Net.WebUtility.HtmlDecode(txt).Trim();
    }

    // Arma el prompt del sistema: prompt base + enrutador (con {{recurso}} expandido) + catalogo de recursos.
    private async Task<string> BuildSystemPrompt(Guid agentId, string basePrompt, IReadOnlyList<AiChatAttachment> resources, CancellationToken ct)
    {
        var sb = new StringBuilder(ExpandResourceRefs(basePrompt, resources));

        var prompts = await _db.AiAgentPrompts.AsNoTracking()
            .Where(p => p.AgentId == agentId)
            .OrderBy(p => p.SortOrder)
            .Select(p => new { p.Name, p.Rule, p.Body })
            .ToListAsync(ct);
        if (prompts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Enrutador de prompts: evalua el mensaje del cliente y, si coincide alguna de estas reglas, sigue PRIMERO las instrucciones del prompt correspondiente (ademas del comportamiento base). Si ninguna aplica, responde con el comportamiento base.");
            foreach (var p in prompts)
            {
                sb.AppendLine();
                sb.AppendLine($"### Prompt \"{p.Name}\"");
                sb.AppendLine($"Regla (cuando usarlo): {(string.IsNullOrWhiteSpace(p.Rule) ? "(sin regla; usar a criterio)" : p.Rule)}");
                sb.AppendLine($"Instrucciones: {ExpandResourceRefs(p.Body, resources)}");
            }
        }

        if (resources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Recursos disponibles. REGLA IMPORTANTE: cuando vayas a comunicar el contenido de un recurso (precios, politicas, textos, imagenes, videos, PDF, ubicacion), NO lo reescribas ni lo resumas: entregalo EXACTO incluyendo en tu respuesta el marcador [[enviar: Nombre exacto del recurso]]. El sistema agregara el contenido o el archivo tal cual. Puedes acompanarlo con una frase breve, pero el contenido del recurso lo entrega el marcador.");
            foreach (var r in resources)
            {
                var kind = r.ResourceType == AgentResourceType.Text ? "Texto" : r.ResourceType.ToString();
                var desc = string.IsNullOrWhiteSpace(r.Detail) ? "archivo" : r.Detail;
                sb.AppendLine($"- ({kind}) {r.Name}: {desc}  -> entregar con [[enviar: {r.Name}]]");
            }
        }

        return sb.ToString();
    }

    // Reemplaza {{nombre}} por la instruccion de entregar ese recurso de forma EXACTA (sin degradarlo).
    private static string ExpandResourceRefs(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{")) { return text; }
        return Regex.Replace(text, @"\{\{\s*([^}]+?)\s*\}\}", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is null) { return m.Value; }
            return $"el recurso \"{res.Name}\" (entregalo EXACTO incluyendo el marcador [[enviar: {res.Name}]]; el sistema agrega su contenido, no lo reescribas)";
        });
    }

    // Extrae los marcadores [[enviar: Nombre]], los quita del texto y devuelve los recursos a adjuntar.
    private static (string, IReadOnlyList<AiChatAttachment>) ExtractAttachments(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        var attachments = new List<AiChatAttachment>();
        var clean = Regex.Replace(text, @"\[\[\s*enviar\s*:\s*([^\]]+?)\s*\]\]", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is not null && attachments.All(a => a.Name != res.Name)) { attachments.Add(res); }
            return string.Empty;
        }, RegexOptions.IgnoreCase);

        // Limpia espacios/lineas sobrantes que deja el marcador.
        clean = Regex.Replace(clean, @"[ \t]+\n", "\n").Trim();
        return (clean, attachments);
    }

    private static AiChatAttachment? FindResource(IReadOnlyList<AiChatAttachment> resources, string name)
    {
        var key = Normalize(name);
        return resources.FirstOrDefault(r => Normalize(r.Name) == key);
    }

    // Normaliza para comparar nombres: minusculas y sin acentos (asi "politica" == "{{politica}}").
    private static string Normalize(string s)
    {
        var n = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) { sb.Append(c); }
        }
        return sb.ToString();
    }
}
