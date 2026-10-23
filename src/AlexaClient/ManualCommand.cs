namespace AlexaClient;

public enum ManualCommand { Help, Discover, Status, Save, Load, Notepad, WorkspacePolicy, SaveWorkspace, LoadWorkspace, RestoreWorkspace, RestoreDevelopmentWorkspace, Ask, AiStatus, AiReset }

public static class ManualCommands
{
    // Explicit manual vocabulary. Unsupported free text never silently executes a tool.
    public static string AskPrompt(string text) => text.TrimStart()[3..].Trim();
    public static ManualCommand Parse(string text)
    {
        var value = text.TrimStart();
        if (value.Length >= 3 && value[..3].Equals("ask", StringComparison.OrdinalIgnoreCase) &&
            (value.Length == 3 || char.IsWhiteSpace(value[3]))) return ManualCommand.Ask;
        return string.Join(" ", text.Trim().ToLowerInvariant()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) switch
    {
        "tools" or "show tools" => ManualCommand.Discover,
        "status" or "desktop status" => ManualCommand.Status,
        "save" or "save handoff" => ManualCommand.Save,
        "load" or "load handoff" or "resume" => ManualCommand.Load,
        "notepad" or "open notepad" => ManualCommand.Notepad,
        "workspace policy" => ManualCommand.WorkspacePolicy,
        "save workspace" => ManualCommand.SaveWorkspace,
        "load workspace" => ManualCommand.LoadWorkspace,
        "restore workspace" => ManualCommand.RestoreWorkspace,
        "restore my development workspace" => ManualCommand.RestoreDevelopmentWorkspace,
        "ai status" => ManualCommand.AiStatus,
        "ai reset" => ManualCommand.AiReset,
        _ => ManualCommand.Help
        };
    }
}
