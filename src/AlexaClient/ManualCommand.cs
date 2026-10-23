namespace AlexaClient;

public enum ManualCommand { Help, Discover, Status, Save, Load, Notepad, WorkspacePolicy, SaveWorkspace, LoadWorkspace, RestoreWorkspace }

public static class ManualCommands
{
    // Explicit manual vocabulary. Unsupported free text never silently executes a tool.
    public static ManualCommand Parse(string text) => string.Join(" ", text.Trim().ToLowerInvariant()
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
        "restore workspace" or "restore my development workspace" => ManualCommand.RestoreWorkspace,
        _ => ManualCommand.Help
    };
}
