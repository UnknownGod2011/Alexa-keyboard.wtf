using AlexaClient;
using System.Net;
using System.Text;
using System.Text.Json;

internal static class ChatChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        check(ManualCommands.Parse("ASK\tPreserve My Case") == ManualCommand.Ask && ManualCommands.AskPrompt("ASK\tPreserve My Case") == "Preserve My Case",
            "explicit AI routing preserves prompt text and accepts whitespace");
        check(ManualCommands.Parse("asking open notepad") == ManualCommand.Help, "unrecognized text cannot implicitly start cloud AI or a desktop action");
        const string key = "unit-test-not-a-real-key";
        const string success = """{"status":"completed","output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"First"},{"type":"output_text","text":"Second"}]}]}""";
        var handler = new RecordingHttp(success);
        using var chat = new OptionalChat(true, key, "configured-model", new HttpClient(handler));
        var reply = await chat.AskAsync("A question", default);
        check(reply.Text == "First\nSecond" && !reply.Incomplete && !reply.Refused, "AI extracts all text blocks after non-message output");
        using (var wire = JsonDocument.Parse(handler.Bodies[0]))
        {
            var data = wire.RootElement;
            check(handler.LastUri == "https://api.openai.com/v1/responses" && handler.LastAuthorization == "Bearer " + key &&
                data.GetProperty("model").GetString() == "configured-model" && data.GetProperty("store").GetBoolean() == false &&
                data.GetProperty("max_output_tokens").GetInt32() == 1024 && !data.TryGetProperty("tools", out _),
                "AI request uses explicit model, auth and no storage/tools");
            check(!handler.Bodies[0].Contains(key, StringComparison.Ordinal) && data.GetProperty("input")[0].GetProperty("content").GetString() == "A question",
                "AI body carries only explicit prompt and instructions, not credential");
        }
        await chat.AskAsync("Follow up", default);
        using (var wire = JsonDocument.Parse(handler.Bodies[1]))
            check(wire.RootElement.GetProperty("input").GetArrayLength() == 3, "AI conversation includes bounded prior user/assistant turn");
        await chat.ResetAsync(default);
        await chat.AskAsync("Fresh", default);
        using (var wire = JsonDocument.Parse(handler.Bodies[2]))
            check(wire.RootElement.GetProperty("input").GetArrayLength() == 1, "AI reset removes previous conversation");

        handler.Status = HttpStatusCode.Unauthorized;
        handler.Body = key + " private diagnostic";
        await ExpectUnavailable(() => chat.AskAsync("failed prompt", default), message =>
            check(message.Contains("401", StringComparison.Ordinal) && !message.Contains(key, StringComparison.Ordinal) && !message.Contains("private", StringComparison.Ordinal),
                "AI errors report status without leaking provider error bodies"));
        handler.Status = HttpStatusCode.OK; handler.Body = success;
        await chat.AskAsync("After failure", default);
        using (var wire = JsonDocument.Parse(handler.Bodies.Last()))
            check(wire.RootElement.GetProperty("input").GetArrayLength() == 3 && !handler.Bodies.Last().Contains("failed prompt", StringComparison.Ordinal),
                "failed AI calls do not contaminate conversation");
        foreach (var status in new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.TemporaryRedirect })
        {
            handler.Status = status;
            var before = handler.Bodies.Count;
            await ExpectUnavailable(() => chat.AskAsync("do not retry", default), _ => check(handler.Bodies.Count == before + 1, "AI does not retry failed HTTP " + (int)status));
        }
        handler.Status = HttpStatusCode.OK;
        foreach (var body in new[] { "{broken", "{}", "{\"status\":\"failed\",\"output\":[]}", "{\"status\":\"completed\",\"output\":[]}" })
        {
            handler.Body = body;
            await ExpectUnavailable(() => chat.AskAsync("format", default), _ => check(true, "AI unsupported/empty response is an explicit failure"));
        }
        handler.Body = """{"status":"incomplete","output":[{"type":"message","content":[{"type":"output_text","text":"partial answer"}]}]}""";
        check((await chat.AskAsync("partial", default)).Incomplete, "AI partial output remains explicitly incomplete");
        handler.Body = """{"status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"Cannot comply"}]}]}""";
        check((await chat.AskAsync("refusal", default)).Refused, "AI refusal is distinguished from an answer");
        handler.Body = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"unit-test-not-a-real-key"}]}]}""";
        check((await chat.AskAsync("redact", default)).Text == "[redacted]", "AI response cannot echo configured credential into UI");
        handler.Body = new string('x', 65537);
        await ExpectUnavailable(() => chat.AskAsync("oversized", default), _ => check(true, "AI response body size is bounded"));
        handler.UnknownLength = true;
        await ExpectUnavailable(() => chat.AskAsync("oversized without length", default), _ => check(true, "AI streaming read stays bounded without Content-Length"));
        handler.UnknownLength = false;
        var priorCalls = handler.Bodies.Count;
        await ExpectUnavailable(() => chat.AskAsync(new string('x', 4001), default), _ => check(handler.Bodies.Count == priorCalls, "oversized AI prompt is rejected before network"));
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            try { await chat.AskAsync("cancelled", cancel.Token); throw new Exception("AI cancellation ignored."); }
            catch (OperationCanceledException) { check(handler.Bodies.Count == priorCalls, "cancelled AI request never reaches HTTP adapter"); }
        }
        using (var disabled = new OptionalChat(false, key, "model", new HttpClient(new RecordingHttp(success))))
            await ExpectUnavailable(() => disabled.AskAsync("question", default), _ => check(disabled.Status.Contains("disabled", StringComparison.Ordinal), "AI is opt-in even with credentials present"));
        using (var incomplete = new OptionalChat(true, null, null, new HttpClient(new RecordingHttp(success))))
            await ExpectUnavailable(() => incomplete.AskAsync("question", default), _ => check(incomplete.Status.Contains("not configured", StringComparison.Ordinal), "partial AI configuration does not fabricate a response"));
        handler.Body = success;
        await chat.ResetAsync(default);
        handler.Block = true;
        using (var cancel = new CancellationTokenSource())
        {
            var pending = chat.AskAsync("cancel while sending", cancel.Token);
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancel.Cancel();
            try { await pending; throw new Exception("In-flight cancellation ignored."); }
            catch (OperationCanceledException) { check(true, "in-flight AI request observes cancellation"); }
        }
        handler.Block = false;
        await chat.AskAsync("after cancellation", default);
        using (var wire = JsonDocument.Parse(handler.Bodies.Last()))
            check(wire.RootElement.GetProperty("input").GetArrayLength() == 1, "cancelled in-flight AI call does not enter history");
        await chat.ResetAsync(default);
        for (var i = 0; i < 10; i++) await chat.AskAsync(new string('q', 3000), default);
        using (var wire = JsonDocument.Parse(handler.Bodies.Last()))
            check(wire.RootElement.GetProperty("input").EnumerateArray().Sum(m => m.GetProperty("content").GetString()!.Length) <= 12000,
                "AI conversation request stays within configured context character budget");
    }

    private static async Task ExpectUnavailable(Func<Task<ChatReply>> action, Action<string> assertion)
    {
        try { await action(); }
        catch (ChatUnavailableException ex) { assertion(ex.Message); return; }
        throw new InvalidOperationException("Expected explicit AI failure.");
    }

    private sealed class UnseekableMemory(byte[] bytes) : MemoryStream(bytes) { public override bool CanSeek => false; }

    private sealed class RecordingHttp(string body) : HttpMessageHandler
    {
        public string Body = body;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public bool UnknownLength, Block;
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<string> Bodies = [];
        public string? LastAuthorization, LastUri;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAuthorization = request.Headers.Authorization?.ToString(); LastUri = request.RequestUri?.ToString();
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (Block) { Entered.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); }
            return new(Status) { Content = UnknownLength ? new StreamContent(new UnseekableMemory(Encoding.UTF8.GetBytes(Body))) : new StringContent(Body, Encoding.UTF8, "application/json") };
        }
    }
}
