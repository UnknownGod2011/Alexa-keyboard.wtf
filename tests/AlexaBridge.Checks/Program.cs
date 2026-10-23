using AlexaBridge;
using ModelContextProtocol.Protocol;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "alexa-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var checks = 0;
try
{
    var data = Path.Combine(root, "data");
    var store = new HandoffStore(data);
    var launcher = new RecordingLauncher();
    var tools = new DesktopTools(store, new FixedApproval(false), launcher);

    var denied = await tools.OpenNotepad(CancellationToken.None);
    Check(denied.IsError == true && launcher.Calls == 0, "denied launch has no side effect");

    var headless = new ConsoleActionApproval(denyActions: true);
    Check(!await headless.ApproveAsync("launch", CancellationToken.None), "headless approval denies");

    var approved = new DesktopTools(store, new FixedApproval(true), launcher);
    Check((await approved.OpenNotepad(CancellationToken.None)).IsError != true && launcher.Calls == 1,
        "approved launch calls only the allowlisted adapter once");

    using var cancel = new CancellationTokenSource();
    var cancellingTools = new DesktopTools(store, new CancellingApproval(cancel), launcher);
    await ExpectAsync<OperationCanceledException>(() => cancellingTools.OpenNotepad(cancel.Token));
    Check(launcher.Calls == 1, "cancellation after approval prevents launch");

    var unsupported = new DesktopTools(store, new FixedApproval(true), new RecordingLauncher { Supported = false });
    Check((await unsupported.OpenNotepad(CancellationToken.None)).IsError == true, "unsupported platform is a tool error");

    var failed = new DesktopTools(store, new FixedApproval(true), new RecordingLauncher { Fail = true });
    Check((await failed.OpenNotepad(CancellationToken.None)).IsError == true, "launch failure is a tool error");

    foreach (var name in new[] { "../escape", "..", "", "CON", "LPT1", " trailing ", "path/name", "x:y" })
    {
        Check(tools.SaveTaskHandoff(name, "summary", CancellationToken.None).IsError == true,
            "reject unsafe or ambiguous name: " + name);
    }
    Check(tools.SaveTaskHandoff("valid", new string('x', 1501), CancellationToken.None).IsError == true,
        "reject oversized summary");
    Check(tools.SaveTaskHandoff("valid", "   ", CancellationToken.None).IsError == true, "reject blank summary");
    Check(tools.LoadTaskHandoff("missing").IsError == true, "missing handoff is a tool error");

    const string summary = "Continue development — round trip with Unicode ✓";
    Check(tools.SaveTaskHandoff("Development", summary, CancellationToken.None).IsError != true, "save succeeds");
    var loaded = tools.LoadTaskHandoff("Development");
    var item = JsonSerializer.Deserialize<TaskHandoff>(Text(loaded));
    Check(loaded.IsError != true && item?.Summary == summary && item.Version == 1, "load preserves summary and schema");

    using var cancelledSave = new CancellationTokenSource();
    cancelledSave.Cancel();
    Expect<OperationCanceledException>(() => store.Save("Development", "must not overwrite", cancelledSave.Token));
    Check(store.Load("Development").Summary == summary, "cancelled save preserves existing handoff");

    File.WriteAllText(Path.Combine(data, "Corrupt.json"), "{broken");
    Check(tools.LoadTaskHandoff("Corrupt").IsError == true, "corrupt JSON is a tool error");
    File.WriteAllText(Path.Combine(data, "Future.json"), JsonSerializer.Serialize(new TaskHandoff(99, "Future", "future", DateTimeOffset.UtcNow)));
    Check(tools.LoadTaskHandoff("Future").IsError == true, "unsupported version is a tool error");
    File.WriteAllText(Path.Combine(data, "Mismatch.json"), JsonSerializer.Serialize(new TaskHandoff(1, "Other", "summary", DateTimeOffset.UtcNow)));
    Check(tools.LoadTaskHandoff("Mismatch").IsError == true, "mismatched stored identity is rejected");
    File.WriteAllText(Path.Combine(data, "Oversized.json"), new string('x', 17000));
    Check(tools.LoadTaskHandoff("Oversized").IsError == true, "oversized stored data is rejected before decoding");

    await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => store.Save("Concurrent", "writer " + i))));
    Check(store.Load("Concurrent").Summary.StartsWith("writer ", StringComparison.Ordinal), "concurrent writes leave a complete record");
    Check(!Directory.EnumerateFiles(data, "*.tmp").Any(), "atomic writes clean up temporary files");
    Check(!File.Exists(Path.Combine(root, "escape.json")),
        "path traversal never creates a file within an escaped location");

    Console.WriteLine($"PASS: {checks} permission, cancellation, tool-result and persistence checks.");
    Console.WriteLine("NOTE: adapter tests use a recording launcher; no Windows UI action was attempted.");
    return 0;
}
finally { Directory.Delete(root, recursive: true); }

void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}
void Expect<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
async Task ExpectAsync<T>(Func<Task<CallToolResult>> action) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
static string Text(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

sealed class FixedApproval(bool result) : IActionApproval
{
    public Task<bool> ApproveAsync(string action, CancellationToken cancellationToken) => Task.FromResult(result);
}
sealed class CancellingApproval(CancellationTokenSource source) : IActionApproval
{
    public Task<bool> ApproveAsync(string action, CancellationToken cancellationToken)
    {
        source.Cancel();
        return Task.FromResult(true);
    }
}
sealed class RecordingLauncher : INotepadLauncher
{
    public bool Supported { get; init; } = true;
    public bool Fail { get; init; }
    public int Calls { get; private set; }
    public int? Launch()
    {
        Calls++;
        if (Fail) throw new InvalidOperationException("test launch failure");
        return 123;
    }
}
