using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AlexaBridge;

public interface INotepadLauncher
{
    bool Supported { get; }
    int? Launch();
}

public sealed class NotepadLauncher : INotepadLauncher
{
    public bool Supported => OperatingSystem.IsWindows();

    public int? Launch()
    {
        if (!Supported) throw new PlatformNotSupportedException();
        // Never search the caller's current directory or PATH for an executable.
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        using var process = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = false });
        return process?.Id;
    }
}

[McpServerToolType]
public sealed class DesktopTools(HandoffStore store, IActionApproval approval, INotepadLauncher launcher, DesktopActionPolicy policy)
{
    [McpServerTool(Name = "get_desktop_status", ReadOnly = true, OpenWorld = false), Description("Return basic non-sensitive information about this MCP desktop companion.")]
    public CallToolResult GetDesktopStatus() => Result(JsonSerializer.Serialize(new {
        platform = RuntimeInformation.OSDescription,
        windows = OperatingSystem.IsWindows(),
        processId = Environment.ProcessId,
        mode = "offline-manual",
        supportsApplicationLaunch = launcher.Supported,
        desktopActionsEnabled = policy.Enabled,
        localAuthentication = "per-launch-bearer",
        launchApproval = "server-console-per-request"
    }));

    [McpServerTool(Name = "open_notepad", OpenWorld = false), Description("Request Windows Notepad launch. A fresh approval in the server's local console is required; MCP arguments cannot grant approval.")]
    public async Task<CallToolResult> OpenNotepad(CancellationToken cancellationToken)
    {
        if (!policy.Enabled)
            return Result("Denied: desktop application launches are disabled by the server operator. Set ALEXA_ALLOW_DESKTOP_ACTIONS=1 before starting the bridge to opt in; each launch will still require local approval.", true);
        if (!launcher.Supported) return Result("Not supported: this action requires Windows.", true);
        if (!await approval.ApproveAsync("Open Windows Notepad on this PC", cancellationToken))
            return Result("Denied: Notepad was not launched. Approve the individual request in the server console; headless hosts deny launches.", true);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var pid = launcher.Launch();
            return pid is null
                ? Result("Windows did not return a process handle; launch is unverified.", true)
                : Result($"Windows reported a process launch (PID {pid}). Verify the Notepad window is visible.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return Result($"Notepad launch failed: {ex.GetType().Name}.", true);
        }
    }

    [McpServerTool(Name = "save_task_handoff", OpenWorld = false), Description("Save a small non-sensitive task summary in this companion's local data directory; never arbitrary files or secrets.")]
    public CallToolResult SaveTaskHandoff(
        [Description("Session name: 1-64 letters/digits/internal spaces/dash/underscore; no Windows device names.")] string name,
        [Description("Non-sensitive task summary, 1-1500 characters.")] string summary,
        CancellationToken cancellationToken)
    {
        try
        {
            store.Save(name, summary, cancellationToken);
            return Result($"Saved task handoff '{name}' to local application data.");
        }
        catch (ArgumentException ex) { return Result(ex.Message, true); }
        catch (IOException) { return Result("Task handoff could not be saved (I/O error).", true); }
        catch (UnauthorizedAccessException) { return Result("Task handoff could not be saved (access denied).", true); }
    }

    [McpServerTool(Name = "load_task_handoff", ReadOnly = true, OpenWorld = false), Description("Retrieve a previously saved task summary by its validated name.")]
    public CallToolResult LoadTaskHandoff([Description("Previously saved task handoff name.")] string name)
    {
        try { return Result(JsonSerializer.Serialize(store.Load(name))); }
        catch (ArgumentException ex) { return Result(ex.Message, true); }
        catch (FileNotFoundException) { return Result("No task handoff with that name exists.", true); }
        catch (DirectoryNotFoundException) { return Result("No task handoff with that name exists.", true); }
        catch (InvalidDataException ex) { return Result(ex.Message, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return Result("Unable to read the saved task handoff.", true); }
    }

    private static CallToolResult Result(string text, bool error = false) => new()
    {
        IsError = error,
        Content = [new TextContentBlock { Text = text }]
    };
}

public sealed record DesktopActionPolicy(bool Enabled);

