using System.Text.Json;

namespace AlexaBridge;

public sealed record TaskHandoff(int Version, string Name, string Summary, DateTimeOffset SavedAt);

public sealed class HandoffStore
{
    public const int MaxSummaryLength = 1500;
    private const int MaxFileBytes = 16384; // Bound decoding of corrupt or manually replaced local files.
    private readonly string _root;
    private readonly object _gate = new();

    public HandoffStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new ArgumentException("An absolute local data directory is required.", nameof(root));
        _root = Path.GetFullPath(root);
    }

    public static string DefaultRoot()
    {
        var root = Environment.GetEnvironmentVariable("ALEXA_BRIDGE_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(root)) return root;
        root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Local application data directory is unavailable.");
        return Path.Combine(root, "AlexaKeyboardBridge", "hand-offs");
    }

    public static bool ValidName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value == value.Trim() &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '_') &&
        !IsReservedName(value);

    private static bool IsReservedName(string value) => value.ToUpperInvariant() is
        "CON" or "PRN" or "AUX" or "NUL" or
        "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or
        "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9";

    public void Save(string name, string summary, CancellationToken cancellationToken = default)
    {
        if (!ValidName(name) || string.IsNullOrWhiteSpace(summary) || summary.Length > MaxSummaryLength)
            throw new ArgumentException("Invalid input: name must use letters, digits, internal spaces, dash or underscore (1-64 chars, no Windows device names); summary must be 1-1500 chars.");
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, name + ".json");
            var temp = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(new TaskHandoff(1, name, summary, DateTimeOffset.UtcNow)));
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temp, path, overwrite: true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public TaskHandoff Load(string name)
    {
        if (!ValidName(name)) throw new ArgumentException("Invalid task handoff name.");
        lock (_gate)
        {
            var path = Path.Combine(_root, name + ".json");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxFileBytes) throw new InvalidDataException("Saved task handoff is too large.");
            var item = JsonSerializer.Deserialize<TaskHandoff>(stream);
            if (item is not { Version: 1 } || !string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.Summary) || item.Summary.Length > MaxSummaryLength)
                throw new InvalidDataException("Unsupported or invalid task handoff format.");
            return item;
        }
    }
}
