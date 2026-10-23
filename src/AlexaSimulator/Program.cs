using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net;

const string Endpoint = "http://127.0.0.1:32178/mcp";

try
{
    using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    await using var connection = await AlexaClient.BridgeConnection.ConnectAsync(connectTimeout.Token);
    var client = connection.Client;

    if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
    {
        return await RunSmokeAsync(client);
    }

    Console.WriteLine("keyboard.wtf x Alexa | local MCP console");
    Console.WriteLine("Connected over Streamable HTTP. No API key is required.");
    Console.WriteLine("This is a local simulator, not an official Alexa+ connection.");
    Console.WriteLine("Commands: tools, status, save, load, notepad, quit");
    while (true)
    {
        Console.Write("> ");
        var command = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (command is null or "quit" or "exit") return 0;
        if (string.IsNullOrEmpty(command)) continue;
        try
        {
            using var callTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            switch (command)
            {
                case "tools":
                    var tools = await client.ListToolsAsync(cancellationToken: callTimeout.Token);
                    foreach (var tool in tools) Console.WriteLine("  " + tool.Name);
                    break;
                case "status":
                    Print(await client.CallToolAsync("get_desktop_status", cancellationToken: callTimeout.Token));
                    break;
                case "save":
                    Console.Write("Session name: ");
                    var saveName = Console.ReadLine() ?? "";
                    Console.Write("Task summary (not sensitive data): ");
                    var summary = Console.ReadLine() ?? "";
                    Print(await client.CallToolAsync("save_task_handoff", new Dictionary<string, object?>
                    {
                        ["name"] = saveName, ["summary"] = summary
                    }, cancellationToken: callTimeout.Token));
                    break;
                case "load":
                    Console.Write("Session name: ");
                    var loadName = Console.ReadLine() ?? "";
                    Print(await client.CallToolAsync("load_task_handoff", new Dictionary<string, object?>
                    {
                        ["name"] = loadName
                    }, cancellationToken: callTimeout.Token));
                    break;
                case "notepad":
                    Console.WriteLine("Requesting Notepad. Approve or deny this request in the server console.");
                    Print(await client.CallToolAsync("open_notepad", cancellationToken: callTimeout.Token));
                    break;
                default:
                    Console.WriteLine("Unknown command. Use: tools, status, save, load, notepad, quit");
                    break;
            }
        }
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

    foreach (var expected in new[] { "get_desktop_status", "open_notepad", "save_task_handoff", "load_task_handoff" })
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
