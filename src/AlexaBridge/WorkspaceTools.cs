using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AlexaBridge;

public interface IWorkspaceFolderLauncher
{
    bool Supported { get; }
    int? Launch(string approvedDirectory);
}

public sealed class WorkspaceFolderLauncher : IWorkspaceFolderLauncher
{
    public bool Supported => OperatingSystem.IsWindows();
    public int? Launch(string approvedDirectory)
    {
        if (!Supported) throw new PlatformNotSupportedException();
        // Only a previously validated existing directory is passed to the Windows shell.
        using var process = Process.Start(new ProcessStartInfo { FileName = approvedDirectory, UseShellExecute = true, Verb = "open" });
        return process?.Id;
    }
}

public sealed record WorkspaceStep(string Action, string State, int? ProcessId = null, string? Detail = null);

[McpServerToolType]
public sealed class WorkspaceTools(WorkspaceStore store, WorkspaceRoot root, IActionApproval approval,
    IWorkspaceFolderLauncher folderLauncher, INotepadLauncher notepadLauncher, DesktopActionPolicy policy)
{
    [McpServerTool(Name = "get_workspace_policy", ReadOnly = true, OpenWorld = false), Description("Show the host-configured workspace root and restore policy. MCP callers cannot change this root.")]
    public CallToolResult GetWorkspacePolicy() => Guard(() => Result(new {
        root = root.Resolve("."), desktopActionsEnabled = policy.Enabled,
        approval = "fresh-server-console-per-restore", actions = new[] { "open approved folder", "open Windows Notepad" }
    }));

    [McpServerTool(Name = "save_workspace", OpenWorld = false), Description("Save a versioned local workspace: one existing relative folder inside the host-approved root, a summary and explicit folder/Notepad actions. Does not launch anything.")]
    public CallToolResult SaveWorkspace(string name, string relativeFolder, string summary, bool openFolder, bool openNotepad, CancellationToken cancellationToken) =>
        Guard(() => Result(store.Save(name, relativeFolder, summary, openFolder, openNotepad, cancellationToken)));

    [McpServerTool(Name = "load_workspace", ReadOnly = true, OpenWorld = false), Description("Read a saved workspace and revalidate its folder and approved-root binding. Does not launch anything.")]
    public CallToolResult LoadWorkspace(string name) => Guard(() => Result(store.Load(name)));

    [McpServerTool(Name = "restore_workspace", OpenWorld = false), Description("Restore a saved workspace's explicit folder/Notepad actions. Requires host opt-in and one fresh local approval describing the complete action bundle. Returns per-step results; visible windows are never assumed.")]
    public async Task<CallToolResult> RestoreWorkspace(string name, CancellationToken cancellationToken)
    {
        SavedWorkspace item;
        string directory;
        try { item = store.Load(name); directory = store.Resolve(item); }
        catch (Exception ex) when (IsExpected(ex)) { return Failure(ex); }
        if (!policy.Enabled) return Result("Denied: desktop actions are disabled by the server operator.", true);
        if ((item.OpenFolder && !folderLauncher.Supported) || (item.OpenNotepad && !notepadLauncher.Supported))
            return Result("Not supported: selected workspace actions require Windows.", true);
        var description = $"Restore workspace '{item.Name}': " +
            (item.OpenFolder ? $"open approved directory [{directory}]" : "no folder launch") +
            (item.OpenNotepad ? "; open Windows Notepad" : "; no Notepad launch");
        if (!await approval.ApproveAsync(description, cancellationToken))
            return Result("Denied: workspace restore was not approved. No actions were started.", true);
        cancellationToken.ThrowIfCancellationRequested();
        var steps = new List<WorkspaceStep>();
        var actions = new List<(string Name, Func<int?> Launch)>();
        if (item.OpenFolder) actions.Add(("open_folder", () => folderLauncher.Launch(store.Resolve(item))));
        if (item.OpenNotepad) actions.Add(("open_notepad", notepadLauncher.Launch));
        // Never reload a mutable record after approval. Revalidate its path before each launch.
        foreach (var action in actions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                steps.Add(new(action.Name, "cancelled", Detail: "No launch attempted for this step."));
                continue;
            }
            try
            {
                store.Resolve(item);
                var pid = action.Launch();
                steps.Add(new(action.Name, pid.HasValue ? "process_reported" : "request_sent_unverified", pid));
            }
            catch (Exception ex) when (IsExpected(ex) || ex is Win32Exception or PlatformNotSupportedException)
            { steps.Add(new(action.Name, "failed", Detail: ex.GetType().Name)); }
        }
        return Result(new { workspace = item.Name, summary = item.Summary, steps,
            note = "A reported process does not prove a visible window. Check the desktop before retrying; completed launches cannot be undone." },
            steps.Any(s => s.State != "process_reported"));
    }

    private static bool IsExpected(Exception ex) => ex is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException;
    private static CallToolResult Guard(Func<CallToolResult> action)
    {
        try { return action(); }
        catch (Exception ex) when (IsExpected(ex)) { return Failure(ex); }
    }
    private static CallToolResult Failure(Exception ex) => Result(ex is ArgumentException or InvalidDataException or InvalidOperationException
        ? ex.Message : "Unable to read or save workspace: " + ex.GetType().Name + ". Check the approved folder and saved record.", true);
    private static CallToolResult Result(object value, bool error = false) => new() {
        IsError = error, Content = [new TextContentBlock { Text = value is string text ? text : JsonSerializer.Serialize(value) }]
    };
}
