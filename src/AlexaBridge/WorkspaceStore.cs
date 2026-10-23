using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlexaBridge;

public sealed record SavedWorkspace(int Version, string Name, string RelativeFolder, string Summary,
    bool OpenFolder, bool OpenNotepad, string RootFingerprint, DateTimeOffset SavedAt);

// Configuration is captured once by the host; an MCP caller cannot approve a root.
public sealed class WorkspaceRoot(string? configuredRoot)
{
    public string Resolve(string relativeFolder)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("No workspace root is configured. Set ALEXA_WORKSPACE_ROOT to an existing local project directory before starting the bridge.");
        if (!Path.IsPathFullyQualified(configuredRoot) || configuredRoot.StartsWith(@"\\", StringComparison.Ordinal) || configuredRoot.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException("Workspace root must be an absolute local directory, not a network path.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        if (OperatingSystem.IsWindows() && new DriveInfo(Path.GetPathRoot(root)!).DriveType == DriveType.Network)
            throw new InvalidOperationException("Workspace root must not use a mapped network drive.");
        ValidateDirectoryChain(root);
        if (relativeFolder == ".") return root;
        if (string.IsNullOrWhiteSpace(relativeFolder)) throw new ArgumentException("Choose a relative workspace folder, or '.' for the approved root.");
        var segments = relativeFolder.Replace('\\', '/').Split('/');
        if (segments.Any(s => string.IsNullOrWhiteSpace(s) || s is "." or ".." || s != s.Trim() || s.EndsWith('.') ||
            s.IndexOfAny([':', '*', '?', '"', '<', '>', '|']) >= 0 || s.Any(char.IsControl)))
            throw new ArgumentException("Workspace folder must be a simple relative path without traversal, drive names or ambiguous segments.");
        var target = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Workspace folder must stay inside the approved root.");
        ValidateDirectoryChain(target);
        return target;
    }

    public string Fingerprint()
    {
        var root = Resolve(".");
        if (OperatingSystem.IsWindows()) root = root.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)));
    }

    private static void ValidateDirectoryChain(string path)
    {
        // Check every ancestor too: a plain-looking child of a junction is unsafe.
        for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
        {
            if (!directory.Exists) throw new DirectoryNotFoundException("The approved workspace directory does not exist.");
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Workspace directories and their ancestors must not be symbolic links or junctions.");
        }
    }
}

public sealed class WorkspaceStore(string dataRoot, WorkspaceRoot root)
{
    private readonly object _gate = new();
    public static string DefaultDataRoot() => Path.Combine(HandoffStore.DefaultRoot(), "workspaces");

    public SavedWorkspace Save(string name, string relativeFolder, string summary, bool openFolder, bool openNotepad, CancellationToken ct)
    {
        Validate(name, summary, openFolder, openNotepad);
        var target = root.Resolve(relativeFolder);
        var relative = Path.GetRelativePath(root.Resolve("."), target);
        var item = new SavedWorkspace(1, name, relative, summary, openFolder, openNotepad, root.Fingerprint(), DateTimeOffset.UtcNow);
        lock (_gate)
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(dataRoot);
            var temporary = Path.Combine(dataRoot, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(item));
                ct.ThrowIfCancellationRequested();
                File.Move(temporary, Path.Combine(dataRoot, name + ".json"), overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return item;
    }

    public SavedWorkspace Load(string name)
    {
        if (!HandoffStore.ValidName(name)) throw new ArgumentException("Invalid workspace name.");
        lock (_gate)
        {
            using var stream = File.OpenRead(Path.Combine(dataRoot, name + ".json"));
            if (stream.Length > 524288) throw new InvalidDataException("Saved workspace is too large.");
            var item = JsonSerializer.Deserialize<SavedWorkspace>(stream);
            if (item is not { Version: 1 } || !string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unsupported workspace format or mismatched name.");
            Validate(item.Name, item.Summary, item.OpenFolder, item.OpenNotepad);
            Resolve(item); // Recheck configuration, path and stored root binding on every read.
            return item;
        }
    }

    public string Resolve(SavedWorkspace item)
    {
        if (item.RootFingerprint != root.Fingerprint())
            throw new InvalidDataException("The approved root changed. Save this workspace again under the current root before restoring it.");
        return root.Resolve(item.RelativeFolder);
    }

    private static void Validate(string name, string summary, bool openFolder, bool openNotepad)
    {
        if (!HandoffStore.ValidName(name) || string.IsNullOrWhiteSpace(summary) || summary.Length > HandoffStore.MaxSummaryLength)
            throw new ArgumentException("Workspace requires a valid name and a non-sensitive summary of 1-1500 characters.");
        if (!openFolder && !openNotepad) throw new ArgumentException("Select at least one workspace action.");
    }
}
