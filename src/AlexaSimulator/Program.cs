using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net;

using var chat = AlexaClient.OptionalChat.FromEnvironment();

const string Endpoint = "http://127.0.0.1:32178/mcp";

try
{
    using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    await using var connection = args.Contains("--chat-only", StringComparer.Ordinal) ? null : await AlexaClient.BridgeConnection.ConnectAsync(connectTimeout.Token);
    var client = connection?.Client;

    if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
    {
        return await RunSmokeAsync(client ?? throw new IOException("Smoke requires the bridge."));
    }

    Console.WriteLine("keyboard.wtf x Alexa | local MCP console");
    Console.WriteLine(client is null ? "Chat-only console. Use ai status, ask <question>, ai reset, quit." : "Connected over Streamable HTTP. Manual MCP commands need no AI key.");
    Console.WriteLine(chat.Status);
    Console.WriteLine("This is a local simulator, not an official Alexa+ connection.");
    Console.WriteLine("Commands: tools, status, save, load, notepad, workspace policy, save workspace, load workspace, restore workspace, ask <question>, ai status, ai reset, quit");
    while (true)
    {
        Console.Write("> ");
        var entered = Console.ReadLine()?.Trim();
        var command = entered?.ToLowerInvariant();
        if (command is null or "quit" or "exit") return 0;
        if (string.IsNullOrEmpty(command)) continue;
        try
        {
            // User typing does not consume the network request deadline.
            using var callTimeout = new CancellationTokenSource();
            var parsed = AlexaClient.ManualCommands.Parse(entered!);
            if (parsed == AlexaClient.ManualCommand.AiStatus) { Console.WriteLine(chat.Status); continue; }
            if (parsed == AlexaClient.ManualCommand.AiReset) { await chat.ResetAsync(callTimeout.Token); Console.WriteLine("AI ask/reply history cleared."); continue; }
            if (parsed == AlexaClient.ManualCommand.Ask)
            {
                callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                var reply = await chat.AskAsync(AlexaClient.ManualCommands.AskPrompt(entered!), callTimeout.Token);
                Console.WriteLine(reply.Refused ? "[AI refusal]" : reply.Incomplete ? "[AI partial answer]" : "[AI text answer]");
                Console.WriteLine(reply.Text);
                Console.WriteLine("No desktop action was executed by this answer.");
                continue;
            }
            if (client is null) { Console.WriteLine("This chat-only session has no desktop bridge connection. Use ask, ai status, ai reset or quit."); continue; }
            switch (command)
            {
                case "tools":
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    var tools = await client.ListToolsAsync(cancellationToken: callTimeout.Token);
                    foreach (var tool in tools) Console.WriteLine("  " + tool.Name);
                    break;
                case "status":
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Print(await client.CallToolAsync("get_desktop_status", cancellationToken: callTimeout.Token));
                    break;
                case "save":
                    Console.Write("Session name: ");
                    var saveName = Console.ReadLine() ?? "";
                    Console.Write("Task summary (not sensitive data): ");
                    var summary = Console.ReadLine() ?? "";
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Print(await client.CallToolAsync("save_task_handoff", new Dictionary<string, object?>
                    {
                        ["name"] = saveName, ["summary"] = summary
                    }, cancellationToken: callTimeout.Token));
                    break;
                case "load":
                    Console.Write("Session name: ");
                    var loadName = Console.ReadLine() ?? "";
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Print(await client.CallToolAsync("load_task_handoff", new Dictionary<string, object?>
                    {
                        ["name"] = loadName
                    }, cancellationToken: callTimeout.Token));
                    break;
                case "restore my development workspace":
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Print(await client.CallToolAsync("restore_workspace", new Dictionary<string, object?> { ["name"] = "Development" }, cancellationToken: callTimeout.Token));
                    break;
                case "workspace policy":
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Print(await client.CallToolAsync("get_workspace_policy", cancellationToken: callTimeout.Token));
                    break;
                case "save workspace":
                    Console.Write("Workspace name: ");
                    var workspaceName = Console.ReadLine() ?? "";
                    Console.Write("Folder relative to the approved root ('.' = root): ");
                    var relativeFolder = Console.ReadLine() ?? "";
                    Console.Write("Non-sensitive summary: ");
                    var workspaceSummary = Console.ReadLine() ?? "";
                    Console.Write("Open folder on restore? Type yes: ");
                    var openFolder = Console.ReadLine() == "yes";
                    Console.Write("Open Notepad on restore? Type yes: ");
                    var openNotepad = Console.ReadLine() == "yes";
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Print(await client.CallToolAsync("save_workspace", new Dictionary<string, object?> {
                        ["name"] = workspaceName, ["relativeFolder"] = relativeFolder, ["summary"] = workspaceSummary,
                        ["openFolder"] = openFolder, ["openNotepad"] = openNotepad
                    }, cancellationToken: callTimeout.Token));
                    break;
                case "load workspace":
                case "restore workspace":
                    Console.Write("Workspace name: ");
                    var selectedWorkspace = Console.ReadLine() ?? "";
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Print(await client.CallToolAsync(command == "load workspace" ? "load_workspace" : "restore_workspace",
                        new Dictionary<string, object?> { ["name"] = selectedWorkspace }, cancellationToken: callTimeout.Token));
                    break;
                case "notepad":
                    callTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                    Console.WriteLine("Requesting Notepad. Approve or deny this request in the server console.");
                    Print(await client.CallToolAsync("open_notepad", cancellationToken: callTimeout.Token));
                    break;
                default:
                    Console.WriteLine("Unknown command. Use: tools, status, save, load, notepad, workspace policy, save workspace, load workspace, restore workspace, ask <question>, ai status, ai reset, quit");
                    break;
            }
        }
        catch (AlexaClient.ChatUnavailableException ex) { Console.Error.WriteLine(ex.Message); }
        catch (Exception ex) when (ex is McpException or HttpRequestException or OperationCanceledException)
        {
            Console.Error.WriteLine("MCP request failed: " + ex.Message);
        }
    }
}
catch (Exception ex) when (ex is McpException or HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Unable to connect to the local MCP server: " + ex.Message);
    Console.Error.WriteLine("Start the AlexaBridge server first, then retry.");
    return 1;
}

static void Print(CallToolResult result)
{
    Console.WriteLine(result.IsError == true ? "[tool error]" : "[tool result]");
    Console.WriteLine(Text(result));
}

static string Text(CallToolResult result) =>
    string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));

static async Task<int> RunSmokeAsync(McpClient client)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
    var ct = timeout.Token;
    var tools = await client.ListToolsAsync(cancellationToken: ct);
    var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
    Console.WriteLine("Discovered MCP tools: " + string.Join(", ", names.OrderBy(name => name)));

    foreach (var expected in new[] { "get_desktop_status", "open_notepad", "save_task_handoff", "load_task_handoff",
        "get_workspace_policy", "save_workspace", "load_workspace", "restore_workspace" })
        Ensure(names.Contains(expected), "Missing MCP tool: " + expected);

    var status = await client.CallToolAsync("get_desktop_status", cancellationToken: ct);
    Ensure(status.IsError != true && Text(status).Contains("platform", StringComparison.Ordinal),
        "Desktop status tool returned an unexpected response.");

    var name = "Smoke" + Guid.NewGuid().ToString("N");
    const string summary = "MCP client to server round-trip test";
    var saved = await client.CallToolAsync("save_task_handoff",
        new Dictionary<string, object?> { ["name"] = name, ["summary"] = summary },
        cancellationToken: ct);
    Ensure(saved.IsError != true && Text(saved).Contains("Saved task handoff", StringComparison.Ordinal),
        "SaveTaskHandoff did not report success.");

    var loaded = await client.CallToolAsync("load_task_handoff",
        new Dictionary<string, object?> { ["name"] = name },
        cancellationToken: ct);
    Ensure(loaded.IsError != true && Text(loaded).Contains(summary, StringComparison.Ordinal),
        "Saved task handoff was not returned.");

    var invalid = await client.CallToolAsync("save_task_handoff",
        new Dictionary<string, object?> { ["name"] = "../escape", ["summary"] = "reject" },
        cancellationToken: ct);
    Ensure(invalid.IsError == true && Text(invalid).Contains("Invalid input", StringComparison.Ordinal),
        "Path traversal name was not rejected.");

    var missingHandoff = await client.CallToolAsync("load_task_handoff",
        new Dictionary<string, object?> { ["name"] = "Missing" + Guid.NewGuid().ToString("N") }, cancellationToken: ct);
    Ensure(missingHandoff.IsError == true, "Missing handoff was incorrectly reported as success.");

    var workspace = await client.CallToolAsync("save_workspace", new Dictionary<string, object?> {
        ["name"] = "Development", ["relativeFolder"] = "Example", ["summary"] = "Workspace MCP round trip",
        ["openFolder"] = true, ["openNotepad"] = true
    }, cancellationToken: ct);
    Ensure(workspace.IsError != true, "Workspace save failed.");
    var workspaceLoaded = await client.CallToolAsync("load_workspace", new Dictionary<string, object?> { ["name"] = "Development" }, cancellationToken: ct);
    Ensure(workspaceLoaded.IsError != true && Text(workspaceLoaded).Contains("Workspace MCP round trip", StringComparison.Ordinal), "Workspace round trip failed.");
    var restoreDenied = await client.CallToolAsync("restore_workspace", new Dictionary<string, object?> { ["name"] = "Development" }, cancellationToken: ct);
    Ensure(restoreDenied.IsError == true && Text(restoreDenied).Contains("disabled", StringComparison.Ordinal), "Headless workspace restore was not denied.");

    var deniedLaunch = await client.CallToolAsync("open_notepad", cancellationToken: ct);
    Ensure(deniedLaunch.IsError == true, "Headless launch must fail without server-side user approval.");

    // Verify missing/incorrect credentials are denied BEFORE MCP handling.
    using var unauthenticatedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    using (var request = new HttpRequestMessage(HttpMethod.Get, Endpoint))
    {
        using var response = await unauthenticatedHttp.SendAsync(request, ct);
        Ensure(response.StatusCode == HttpStatusCode.Unauthorized,
            "Unauthenticated MCP GET was accepted.");
    }
    using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
    {
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await unauthenticatedHttp.SendAsync(request, ct);
        Ensure(response.StatusCode == HttpStatusCode.Unauthorized,
            "Unauthenticated MCP POST was accepted.");
    }
    using (var request = new HttpRequestMessage(HttpMethod.Get, Endpoint))
    {
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + new string('0', 64));
        using var response = await unauthenticatedHttp.SendAsync(request, ct);
        Ensure(response.StatusCode == HttpStatusCode.Unauthorized,
            "Invalid local bearer token was accepted.");
    }

    using (var request = new HttpRequestMessage(HttpMethod.Delete, Endpoint))
    {
        using var response = await unauthenticatedHttp.SendAsync(request, ct);
        Ensure(response.StatusCode == HttpStatusCode.Unauthorized, "Unauthenticated MCP DELETE was accepted.");
    }

    // Check the existing browser-origin and DNS-rebinding defenses over HTTP.
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    using (var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:32178/health"))
    {
        request.Headers.TryAddWithoutValidation("Origin", "https://untrusted.example");
        using var response = await http.SendAsync(request, ct);
        Ensure(response.StatusCode == HttpStatusCode.Forbidden, "Untrusted Origin was accepted.");
    }
    using (var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:32178/health"))
    {
        request.Headers.Host = "untrusted.example";
        using var response = await http.SendAsync(request, ct);
        Ensure(response.StatusCode == HttpStatusCode.Forbidden, "Untrusted Host was accepted.");
    }

    // Unknown tools must not produce a successful invocation.
    var rejected = false;
    try
    {
        var missing = await client.CallToolAsync("NonexistentTool_Smoke", cancellationToken: ct);
        rejected = missing.IsError == true;
    }
    catch (McpException)
    {
        rejected = true;
    }
    Ensure(rejected, "Unknown MCP tool was accepted.");

    Console.WriteLine("PASS: SDK MCP initialize, discovery, status, save/load, tool errors, denied launch, unknown tool, local bearer authentication GET/POST/DELETE, Host/Origin.");
    Console.WriteLine("NOTE: No interactive Windows GUI action was attempted by this headless smoke test.");
    return 0;
}

static void Ensure(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
