namespace AlexaShared;

public static class LocalRuntime
{
    public static string DirectoryPath
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("ALEXA_BRIDGE_RUNTIME_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (!Path.IsPathFullyQualified(configured)) throw new IOException("ALEXA_BRIDGE_RUNTIME_DIR must be an absolute path.");
                return configured;
            }
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local)) throw new IOException("Local application data directory unavailable.");
            return Path.Combine(local, "AlexaKeyboardBridge", "runtime");
        }
    }

    public static string TokenPath => Path.Combine(DirectoryPath, "mcp-auth-token");
}
