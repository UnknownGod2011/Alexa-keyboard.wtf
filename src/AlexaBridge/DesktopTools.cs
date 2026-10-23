using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace AlexaBridge;

[McpServerToolType]
public static class DesktopTools
{
    private static readonly object SessionLock = new();

    [McpServerTool(Name = "get_desktop_status", ReadOnly = true, OpenWorld = false), Description("Return basic non-sensitive information about this MCP desktop companion.")]
    public static string GetDesktopStatus()
    {
        return JsonSerializer.Serialize(new {
            platform = RuntimeInformation.OSDescription,
            windows = OperatingSystem.IsWindows(),
            processId = Environment.ProcessId,
            mode = "offline-manual",
            supportsApplicationLaunch = OperatingSystem.IsWindows()
        });
    }

    [McpServerTool(Name = "open_notepad", OpenWorld = false), Description("Open Windows Notepad on the local interactive Windows desktop. No command or path arguments are accepted.")]
    public static string OpenNotepad()
    {
        if (!OperatingSystem.IsWindows())
            return "Not supported: this action requires Windows.";
        try
        {
            using var process = Process.Start(new ProcessStartInfo {
                FileName = "notepad.exe",
                UseShellExecute = true
            });
            if (process is null) return "Windows did not return a process handle; launch is unverified.";
            return $"Windows reported a process launch (PID {process.Id}). Verify the Notepad window is visible.";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return $"Notepad launch failed: {ex.GetType().Name}.";
        }
    }

    [McpServerTool(Name = "save_task_handoff", OpenWorld = false), Description("Save a small task handoff in the companion's private local application data. Do not supply credentials, secrets or private text.")]
    public static string SaveTaskHandoff(
        [Description("Short session name, letters/numbers/spaces/dash/underscore only, up to 64 characters.")] string name,
        [Description("Task summary up to 1500 characters; never include secrets.")] string summary)
    {
        if (!ValidName(name) || string.IsNullOrWhiteSpace(summary) || summary.Length > 1500)
            return "Invalid input: name must use letters, digits, spaces, dash or underscore (1-64 chars); summary must be 1-1500 chars.";
        try
        {
            var path = SessionPath(name);
            lock (SessionLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temp, JsonSerializer.Serialize(new TaskHandoff(1, name, summary, DateTimeOffset.UtcNow)));
                    File.Move(temp, path, overwrite: true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            return $"Saved task handoff '{name}' to local application data.";
        }
        catch (IOException) { return "Task handoff could not be saved (I/O error)."; }
        catch (UnauthorizedAccessException) { return "Task handoff could not be saved (access denied)."; }
    }

    [McpServerTool(Name = "load_task_handoff", ReadOnly = true, OpenWorld = false), Description("Retrieve an existing task handoff from this companion's local application data.")]
    public static string LoadTaskHandoff(
        [Description("Previously saved task handoff name.")] string name)
    {
        if (!ValidName(name)) return "Invalid task handoff name.";
        try
        {
            lock (SessionLock)
            {
                var path = SessionPath(name);
                if (!File.Exists(path)) return "No task handoff with that name exists.";
                var item = JsonSerializer.Deserialize<TaskHandoff>(File.ReadAllText(path));
                return item is { Version: 1 } ? JsonSerializer.Serialize(item) : "Unsupported or invalid task handoff format.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return "Unable to read the saved task handoff.";
        }
    }

    private static bool ValidName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '_');

    private static string SessionPath(string name)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
        return Path.Combine(root, "AlexaKeyboardBridge", "hand-offs", name.Trim() + ".json");
    }

    private sealed record TaskHandoff(int Version, string Name, string Summary, DateTimeOffset SavedAt);
}
