using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DokTrino.Application.Tenancy;
using DokTrino.Domain.Enums;

namespace DokTrino.Infrastructure.Ai;

/// <summary>
/// Cliente HTTP de inferencia para los proveedores de IA. La API key llega descifrada; no se persiste
/// ni se loggea. Soporta Gemini (REST), OpenAI/ChatGPT y DeepSeek (chat/completions) y Claude (messages).
/// </summary>
public sealed class AiProviderClient : IAiProviderClient
{
    private readonly HttpClient _http;

    public AiProviderClient(HttpClient http) => _http = http;

    public async Task<AiChatResult> CompleteAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiChatTurn> turns, CancellationToken cancellationToken = default)
    {
        try
        {
            return provider switch
            {
                AiProvider.Gemini => await Gemini(apiKey, baseUrl, model, systemPrompt, turns, cancellationToken),
                AiProvider.Claude => await Claude(apiKey, baseUrl, model, systemPrompt, turns, cancellationToken),
                _ => await OpenAiCompatible(provider, apiKey, baseUrl, model, systemPrompt, turns, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            return new AiChatResult(false, null, $"No se pudo contactar al proveedor: {ex.Message}");
        }
    }

    private static string Base(string? baseUrl, string fallback) =>
        (string.IsNullOrWhiteSpace(baseUrl) ? fallback : baseUrl).TrimEnd('/');

    // ===== Gemini =====
    private async Task<AiChatResult> Gemini(string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://generativelanguage.googleapis.com")}/v1beta/models/{model}:generateContent?key={apiKey}";
        var body = new
        {
            systemInstruction = string.IsNullOrWhiteSpace(systemPrompt) ? null : new { parts = new[] { new { text = systemPrompt } } },
            contents = turns.Select(t => new { role = t.Role == "model" ? "model" : "user", parts = new[] { new { text = t.Text } } }).ToArray()
        };
        using var resp = await _http.PostAsync(url, JsonContent(body), ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return Fail(resp.StatusCode is var s ? (int)s : 0, raw); }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usageMetadata", out var um))
        {
            inTok = um.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            outTok = um.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    // ===== OpenAI / ChatGPT / DeepSeek (formato chat/completions) =====
    private async Task<AiChatResult> OpenAiCompatible(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var fallback = provider == AiProvider.DeepSeek ? "https://api.deepseek.com" : "https://api.openai.com/v1";
        var url = $"{Base(baseUrl, fallback)}/chat/completions";

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt)) { messages.Add(new { role = "system", content = systemPrompt }); }
        foreach (var t in turns) { messages.Add(new { role = t.Role == "model" ? "assistant" : "user", content = t.Text }); }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent(new { model, messages }) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return Fail((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usage", out var u))
        {
            inTok = u.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            outTok = u.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    // ===== Claude (messages) =====
    private async Task<AiChatResult> Claude(string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, CancellationToken ct)
    {
        var url = $"{Base(baseUrl, "https://api.anthropic.com")}/v1/messages";
        var body = new
        {
            model,
            max_tokens = 1024,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            messages = turns.Select(t => new { role = t.Role == "model" ? "assistant" : "user", content = t.Text }).ToArray()
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent(body) };
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return Fail((int)resp.StatusCode, raw); }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        var (inTok, outTok) = (0, 0);
        if (doc.RootElement.TryGetProperty("usage", out var u))
        {
            inTok = u.TryGetProperty("input_tokens", out var p) ? p.GetInt32() : 0;
            outTok = u.TryGetProperty("output_tokens", out var c) ? c.GetInt32() : 0;
        }
        return new AiChatResult(true, text, null, inTok, outTok);
    }

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }),
            Encoding.UTF8, "application/json");

    private static AiChatResult Fail(int status, string raw)
    {
        var snippet = raw.Length > 300 ? raw[..300] : raw;
        return new AiChatResult(false, null, $"El proveedor respondio HTTP {status}. {snippet}");
    }

    // ==================== Tool-use (function calling) ====================

    public async Task<AiCompletion> CompleteWithToolsAsync(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiToolMessage> messages, IReadOnlyList<AiToolSpec> tools, CancellationToken ct = default)
    {
        try
        {
            return provider == AiProvider.Claude
                ? await ClaudeWithTools(apiKey, baseUrl, model, systemPrompt, messages, tools, ct)
                : await OpenAiWithTools(provider, apiKey, baseUrl, model, systemPrompt, messages, tools, ct);
        }
        catch (Exception ex) { return AiCompletion.Failed($"No se pudo contactar al proveedor: {ex.Message}"); }
    }

    private static JsonElement Schema(string json)
    {
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch { return JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone(); }
    }

    private async Task<AiCompletion> ClaudeWithTools(string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiToolMessage> history, IReadOnlyList<AiToolSpec> tools, CancellationToken ct)
    {
        var msgs = new List<object>();
        foreach (var m in history)
        {
            if (m.Role == "tool")
            {
                msgs.Add(new { role = "user", content = new object[] {
                    new { type = "tool_result", tool_use_id = m.ToolCallId, content = m.Text ?? "" } } });
            }
            else if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 })
            {
                var content = new List<object>();
                if (!string.IsNullOrWhiteSpace(m.Text)) { content.Add(new { type = "text", text = m.Text }); }
                foreach (var c in m.ToolCalls)
                {
                    content.Add(new { type = "tool_use", id = c.Id, name = c.Name,
                        input = Schema(string.IsNullOrWhiteSpace(c.ArgumentsJson) ? "{}" : c.ArgumentsJson) });
                }
                msgs.Add(new { role = "assistant", content });
            }
            else
            {
                msgs.Add(new { role = m.Role == "model" ? "assistant" : m.Role, content = m.Text ?? "" });
            }
        }

        var toolDefs = tools.Select(t => new { name = t.Name, description = t.Description,
            input_schema = Schema(t.ParametersJsonSchema) }).ToArray();

        var body = new
        {
            model,
            max_tokens = 1500,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            messages = msgs,
            tools = toolDefs
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base(baseUrl, "https://api.anthropic.com")}/v1/messages")
        { Content = JsonContent(body) };
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return AiCompletion.Failed($"El proveedor respondio HTTP {(int)resp.StatusCode}."); }

        using var doc = JsonDocument.Parse(raw);
        var text = new StringBuilder();
        var calls = new List<AiToolCall>();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (type == "text") { text.Append(block.GetProperty("text").GetString()); }
            else if (type == "tool_use")
            {
                calls.Add(new AiToolCall(block.GetProperty("id").GetString() ?? "",
                    block.GetProperty("name").GetString() ?? "",
                    block.GetProperty("input").GetRawText()));
            }
        }
        var (inTok, outTok) = Tokens(doc.RootElement, "usage", "input_tokens", "output_tokens");
        return new AiCompletion(true, text.Length == 0 ? null : text.ToString(), calls, null, inTok, outTok);
    }

    private async Task<AiCompletion> OpenAiWithTools(AiProvider provider, string apiKey, string? baseUrl, string model,
        string systemPrompt, IReadOnlyList<AiToolMessage> history, IReadOnlyList<AiToolSpec> tools, CancellationToken ct)
    {
        var fallback = provider == AiProvider.DeepSeek ? "https://api.deepseek.com" : "https://api.openai.com/v1";
        var msgs = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt)) { msgs.Add(new { role = "system", content = systemPrompt }); }
        foreach (var m in history)
        {
            if (m.Role == "tool")
            {
                msgs.Add(new { role = "tool", tool_call_id = m.ToolCallId, content = m.Text ?? "" });
            }
            else if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 })
            {
                msgs.Add(new { role = "assistant", content = m.Text,
                    tool_calls = m.ToolCalls.Select(c => new { id = c.Id, type = "function",
                        function = new { name = c.Name, arguments = string.IsNullOrWhiteSpace(c.ArgumentsJson) ? "{}" : c.ArgumentsJson } }).ToArray() });
            }
            else
            {
                msgs.Add(new { role = m.Role == "model" ? "assistant" : m.Role, content = m.Text ?? "" });
            }
        }
        var toolDefs = tools.Select(t => new { type = "function",
            function = new { name = t.Name, description = t.Description, parameters = Schema(t.ParametersJsonSchema) } }).ToArray();

        var body = new { model, messages = msgs, tools = toolDefs, tool_choice = "auto" };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base(baseUrl, fallback)}/chat/completions")
        { Content = JsonContent(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) { return AiCompletion.Failed($"El proveedor respondio HTTP {(int)resp.StatusCode}."); }

        using var doc = JsonDocument.Parse(raw);
        var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        var text = msg.TryGetProperty("content", out var c2) && c2.ValueKind == JsonValueKind.String ? c2.GetString() : null;
        var calls = new List<AiToolCall>();
        if (msg.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
        {
            foreach (var tc in tcs.EnumerateArray())
            {
                var fn = tc.GetProperty("function");
                calls.Add(new AiToolCall(tc.GetProperty("id").GetString() ?? "",
                    fn.GetProperty("name").GetString() ?? "",
                    fn.GetProperty("arguments").GetString() ?? "{}"));
            }
        }
        var (inTok, outTok) = Tokens(doc.RootElement, "usage", "prompt_tokens", "completion_tokens");
        return new AiCompletion(true, text, calls, null, inTok, outTok);
    }

    private static (int, int) Tokens(JsonElement root, string usageProp, string inProp, string outProp)
    {
        if (!root.TryGetProperty(usageProp, out var u)) { return (0, 0); }
        var i = u.TryGetProperty(inProp, out var p) ? p.GetInt32() : 0;
        var o = u.TryGetProperty(outProp, out var c) ? c.GetInt32() : 0;
        return (i, o);
    }
}
