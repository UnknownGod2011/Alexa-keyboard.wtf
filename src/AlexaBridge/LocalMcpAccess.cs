using System.Security.Cryptography;
using System.Text;

namespace AlexaBridge;

/// <summary>
/// Rotating local bearer credential for same-user MCP clients. A cross-process
/// file lease prevents another server from replacing a running server's token.
/// This is not a defense against malicious processes running as the same user.
/// </summary>
public sealed class LocalMcpAccess : IDisposable
{
    private readonly FileStream _lease;
    private readonly string _token;
    private readonly string _tokenPath;

    private LocalMcpAccess(FileStream lease, string token, string tokenPath)
    {
        _lease = lease;
        _token = token;
        _tokenPath = tokenPath;
    }

    public static LocalMcpAccess Create()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            throw new InvalidOperationException("Local application data directory is unavailable.");
        var directory = Path.Combine(localData, "AlexaKeyboardBridge", "runtime");
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var lease = new FileStream(Path.Combine(directory, "server.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var path = Path.Combine(directory, "mcp-auth-token");
            var temporary = Path.Combine(directory, "token-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, token + Environment.NewLine);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return new LocalMcpAccess(lease, token, path);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public bool IsAuthorized(string header)
    {
        const string prefix = "Bearer ";
        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var supplied = header[prefix.Length..];
        if (supplied.Length != _token.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(_token));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tokenPath) &&
                string.Equals(File.ReadAllText(_tokenPath).Trim(), _token, StringComparison.Ordinal))
                File.Delete(_tokenPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        finally { _lease.Dispose(); }
    }
}
