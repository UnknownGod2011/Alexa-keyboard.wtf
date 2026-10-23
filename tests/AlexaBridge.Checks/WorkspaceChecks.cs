using AlexaBridge;
using ModelContextProtocol.Protocol;
using System.Text.Json;

internal static class WorkspaceChecks
{
    public static async Task RunAsync(string temporaryRoot, Action<bool, string> check)
    {
        var projects = Path.Combine(temporaryRoot, "projects");
        var project = Path.Combine(projects, "Example");
        Directory.CreateDirectory(project);
        var data = Path.Combine(temporaryRoot, "workspaces");
        var root = new WorkspaceRoot(projects);
        var store = new WorkspaceStore(data, root);
        var folder = new Folder();
        var notepad = new Notepad();
        var approval = new Approval();
        WorkspaceTools Tools(bool enabled = true) => new(store, root, approval, folder, notepad, new(enabled));
        check(new WorkspaceTools(store, new WorkspaceRoot(null), approval, folder, notepad, new(true)).GetWorkspacePolicy().IsError == true,
            "workspace root is explicit host configuration");
        foreach (var path in new[] { "../Example", "Example/..", "/Example", "C:\\Example", "Example//child", "Example.", "Example ", ".hidden/../Example" })
            check(Tools().SaveWorkspace("Invalid", path, "summary", true, false, default).IsError == true,
                "workspace rejects unsafe path: " + path);
        check(Tools().SaveWorkspace("None", "Example", "summary", false, false, default).IsError == true, "workspace requires an action");
        var saved = Tools().SaveWorkspace("Development", "Example", "Continue coding", true, true, default);
        check(saved.IsError != true && folder.Calls == 0 && notepad.Calls == 0, "saving workspace has no desktop side effect");
        check(Tools().LoadWorkspace("Development").IsError != true, "workspace record round trip validates path");
        check((await Tools(false).RestoreWorkspace("Development", default)).IsError == true && approval.Calls == 0, "restore requires operator opt-in before asking approval");
        approval.Allow = false;
        check((await Tools().RestoreWorkspace("Development", default)).IsError == true && folder.Calls == 0 && notepad.Calls == 0,
            "denied restore launches no steps");
        approval.Allow = true;
        var prior = approval.Calls;
        var restored = await Tools().RestoreWorkspace("Development", default);
        check(restored.IsError != true && folder.Calls == 1 && notepad.Calls == 1 && folder.LastPath == project && approval.Calls == prior + 1 &&
            approval.LastAction.Contains(project, StringComparison.Ordinal) && approval.LastAction.Contains("Notepad", StringComparison.Ordinal),
            "one fresh approval describes and launches exactly the saved bundle");
        folder.Fail = true;
        var partial = await Tools().RestoreWorkspace("Development", default);
        check(partial.IsError == true && Text(partial).Contains("failed", StringComparison.Ordinal) && Text(partial).Contains("process_reported", StringComparison.Ordinal),
            "partial restore reports both failed and successful steps");
        folder.Fail = false;
        folder.NoHandle = true;
        check((await Tools().RestoreWorkspace("Development", default)).IsError == true, "shell request without process handle remains unverified");
        folder.NoHandle = false;
        using (var cancel = new CancellationTokenSource())
        {
            var before = notepad.Calls;
            folder.AfterLaunch = cancel.Cancel;
            var cancelled = await Tools().RestoreWorkspace("Development", cancel.Token);
            check(cancelled.IsError == true && notepad.Calls == before && Text(cancelled).Contains("cancelled", StringComparison.Ordinal),
                "cancellation after first step prevents subsequent launch and reports partial result");
            folder.AfterLaunch = null;
        }
        using (var cancel = new CancellationTokenSource())
        {
            var before = folder.Calls;
            approval.AfterApproval = cancel.Cancel;
            try { await Tools().RestoreWorkspace("Development", cancel.Token); throw new Exception("Cancellation was ignored."); }
            catch (OperationCanceledException) { check(folder.Calls == before, "cancellation during approval prevents all launch steps"); }
            approval.AfterApproval = null;
        }
        var secondRoot = Path.Combine(temporaryRoot, "different-projects");
        Directory.CreateDirectory(Path.Combine(secondRoot, "Example"));
        var changedRoot = new WorkspaceRoot(secondRoot);
        var changed = new WorkspaceTools(new WorkspaceStore(data, changedRoot), changedRoot, approval, folder, notepad, new(true));
        check(changed.LoadWorkspace("Development").IsError == true && (await changed.RestoreWorkspace("Development", default)).IsError == true,
            "changed approved root invalidates saved workspace");
        File.WriteAllText(Path.Combine(data, "Broken.json"), "{broken");
        check(Tools().LoadWorkspace("Broken").IsError == true, "corrupt workspace fails closed");
        var record = store.Load("Development");
        File.WriteAllText(Path.Combine(data, "Future.json"), JsonSerializer.Serialize(record with { Version = 99, Name = "Future" }));
        check(Tools().LoadWorkspace("Future").IsError == true, "future workspace format rejected");
        File.WriteAllText(Path.Combine(data, "Mismatch.json"), JsonSerializer.Serialize(record));
        check(Tools().LoadWorkspace("Mismatch").IsError == true, "workspace identity mismatch rejected");
        File.WriteAllText(Path.Combine(data, "Oversized.json"), new string('x', 524289));
        check(Tools().LoadWorkspace("Oversized").IsError == true, "workspace decoding is size bounded");
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            try { store.Save("Development", "Example", "overwritten", true, false, cancel.Token); throw new Exception("Cancelled save was ignored."); }
            catch (OperationCanceledException) { check(store.Load("Development").Summary == "Continue coding", "cancelled workspace write preserves previous record"); }
        }
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() => store.Save("Concurrent", "Example", "writer " + i, true, false, default))));
        check(store.Load("Concurrent").Summary.StartsWith("writer ", StringComparison.Ordinal) && !Directory.EnumerateFiles(data, "*.tmp").Any(), "workspace writes remain atomic under concurrency");
        // A runner without symlink privileges still exercises the same reparse check with a Windows junction.
        var alias = Path.Combine(projects, "Alias");
        if (OperatingSystem.IsWindows())
        {
            using var junction = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Arguments = $"/c mklink /J \"{alias}\" \"{project}\"", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            })!;
            await junction.WaitForExitAsync();
            if (junction.ExitCode != 0) throw new Exception("Could not create junction fixture: " + await junction.StandardError.ReadToEndAsync());
        }
        else Directory.CreateSymbolicLink(alias, project);
        try
        {
            check(Tools().SaveWorkspace("Alias", "Alias", "must reject", true, false, default).IsError == true, "workspace rejects directory junction or symbolic link");
            check(new WorkspaceTools(store, new WorkspaceRoot(alias), approval, folder, notepad, new(true)).GetWorkspacePolicy().IsError == true,
                "approved root cannot itself be a junction or symbolic link");
        }
        finally { Directory.Delete(alias); }
        Directory.Delete(project);
        check((await Tools().RestoreWorkspace("Development", default)).IsError == true, "removed project directory is rejected before approval");
    }
    private static string Text(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
    private sealed class Approval : IActionApproval
    {
        public bool Allow = true;
        public int Calls;
        public string LastAction = "";
        public Action? AfterApproval;
        public Task<bool> ApproveAsync(string action, CancellationToken cancellationToken) { Calls++; LastAction = action; AfterApproval?.Invoke(); return Task.FromResult(Allow); }
    }
    private sealed class Folder : IWorkspaceFolderLauncher
    {
        public bool Supported => true;
        public bool Fail, NoHandle;
        public int Calls;
        public string? LastPath;
        public Action? AfterLaunch;
        public int? Launch(string directory) { Calls++; LastPath = directory; if (Fail) throw new InvalidOperationException(); AfterLaunch?.Invoke(); return NoHandle ? null : 101; }
    }
    private sealed class Notepad : INotepadLauncher
    {
        public bool Supported => true;
        public int Calls;
        public int? Launch() { Calls++; return 102; }
    }
}
