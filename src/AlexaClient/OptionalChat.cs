using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AlexaClient;

public sealed record ChatReply(string Text, bool Incomplete, bool Refused);
public sealed class ChatUnavailableException(string message) : Exception(message);

/// <summary>Explicit opt-in text conversation. It has no MCP connection or desktop-action capability.</summary>
public sealed class OptionalChat : IDisposable
{
    private const int MaxPrompt = 4000, MaxHistory = 12000, MaxBody = 65536;
    private readonly HttpClient _http;
    private readonly string? _key, _model;
    private readonly bool _enabled;
    private readonly string? _configurationError;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<(string Role, string Text)> _history = [];

    public static OptionalChat FromEnvironment() => new(
        Environment.GetEnvironmentVariable("ALEXA_AI_ENABLED") == "1",
        Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        Environment.GetEnvironmentVariable("ALEXA_AI_MODEL"),
        new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan });

    internal OptionalChat(bool enabled, string? key, string? model, HttpClient http)
    {
        _enabled = enabled; _key = key; _model = model; _http = http;
        if (enabled && (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace) || key.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(model) || model.Length > 200 || model.Any(char.IsControl)))
            _configurationError = "AI is enabled but not configured. Set OPENAI_API_KEY and ALEXA_AI_MODEL before starting the client.";
    }

    public string Status => !_enabled ? "AI is disabled. Manual MCP commands need no AI key." :
        _configurationError ?? $"AI is configured for model {_model}. Only explicit 'ask' prompts and recent ask/reply history are sent to OpenAI. No desktop actions are executed by AI.";

    public async Task ResetAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { _history.Clear(); }
        finally { _gate.Release(); }
    }

    public async Task<ChatReply> AskAsync(string prompt, CancellationToken ct)
    {
        if (!_enabled) throw new ChatUnavailableException("AI is disabled. Set ALEXA_AI_ENABLED=1, OPENAI_API_KEY and ALEXA_AI_MODEL to opt in. Manual MCP tools remain available without a key.");
        if (_configurationError is not null) throw new ChatUnavailableException(_configurationError);
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > MaxPrompt)
            throw new ChatUnavailableException("Ask a non-empty question of at most 4000 characters.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(35));
        await _gate.WaitAsync(deadline.Token);
        try
        {
            var history = _history.ToList();
            while (history.Count > 0 && (history.Count >= 12 || history.Sum(m => m.Text.Length) + prompt.Length > MaxHistory))
                history.RemoveRange(0, Math.Min(2, history.Count));
            var input = history.Select(m => new { role = m.Role, content = m.Text }).ToList();
            input.Add(new { role = "user", content = prompt });
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
            request.Content = JsonContent.Create(new {
                model = _model, input, store = false, max_output_tokens = 1024,
                instructions = "You are a text assistant inside a local desktop companion. You cannot inspect this computer or execute desktop actions. Never claim that you opened, saved or restored anything. Suggest the explicit manual commands when needed."
            });
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
                throw new ChatUnavailableException($"AI request failed (HTTP {(int)response.StatusCode}). Check account access, selected model and quota. No automatic retry was made.");
            if (response.Content.Headers.ContentLength > MaxBody)
                throw new ChatUnavailableException("AI response exceeded the size limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var block = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(block, deadline.Token)) > 0)
            {
                if (buffer.Length + read > MaxBody) throw new ChatUnavailableException("AI response exceeded the size limit.");
                buffer.Write(block, 0, read);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            var data = document.RootElement;
            var status = data.GetProperty("status").GetString();
            if (status is not ("completed" or "incomplete")) throw new ChatUnavailableException("AI did not return a completed or partial text response.");
            var texts = new List<string>();
            var refused = false;
            foreach (var item in data.GetProperty("output").EnumerateArray())
            {
                if (item.GetProperty("type").GetString() != "message") continue;
                foreach (var content in item.GetProperty("content").EnumerateArray())
                {
                    var type = content.GetProperty("type").GetString();
                    if (type == "output_text") texts.Add(content.GetProperty("text").GetString() ?? "");
                    else if (type == "refusal") { refused = true; texts.Add(content.GetProperty("refusal").GetString() ?? ""); }
                }
            }
            var text = string.Join("\n", texts).Replace(_key!, "[redacted]", StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(text)) throw new ChatUnavailableException("AI returned no displayable text. Try a shorter question or review the selected model's output budget.");
            deadline.Token.ThrowIfCancellationRequested();
            // Commit conversation only after a valid response. Failed/cancelled calls do not alter history.
            _history.Clear(); _history.AddRange(history);
            _history.Add(("user", prompt)); _history.Add(("assistant", text));
            while (_history.Count > 12 || _history.Sum(m => m.Text.Length) > MaxHistory) _history.RemoveRange(0, 2);
            return new(text, status == "incomplete", refused);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new ChatUnavailableException("AI returned an unsupported response format. No desktop action was executed."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        { throw new ChatUnavailableException("AI connection failed. Check network access. No automatic retry was made."); }
        finally { _gate.Release(); }
    }

    public void Dispose() { _http.Dispose(); _gate.Dispose(); }
}
