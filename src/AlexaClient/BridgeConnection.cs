using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AlexaClient;

public sealed record DiscoveredTool(string Name, string Description);

/// <summary>Shared authenticated transport for both local simulators; no desktop implementation is linked.</summary>
public sealed class BridgeConnection : IAsyncDisposable
{
    public const string Endpoint = "http://127.0.0.1:32178/mcp";
    public McpClient Client { get; }

    private BridgeConnection(McpClient client) => Client = client;

    public static async Task<BridgeConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var path = AlexaShared.LocalRuntime.TokenPath;
        var token = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        if (token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new IOException("Invalid local credential. Restart the bridge, then reconnect.");
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(Endpoint),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token }
        });
        try
        {
            return new BridgeConnection(await McpClient.CreateAsync(transport, cancellationToken: cancellationToken));
        }
        catch
        {
            await transport.DisposeAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<DiscoveredTool>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var tools = await Client.ListToolsAsync(cancellationToken: cancellationToken);
        return tools.Select(t => new DiscoveredTool(t.Name, t.Description ?? "")).ToArray();
    }

    public async Task<CallToolResult> CallAsync(string name, Dictionary<string, object?>? arguments, CancellationToken cancellationToken) =>
        await Client.CallToolAsync(name, arguments, cancellationToken: cancellationToken);

    public ValueTask DisposeAsync() => Client.DisposeAsync();

    public static string Text(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(c => c.Text));
}
