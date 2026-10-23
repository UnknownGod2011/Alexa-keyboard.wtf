using AlexaBridge;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

// Isolated integration host using production tool/approval/launch implementations.
// Local console input is simulated by this test process, never by MCP arguments.
internal sealed class NativeMcpFixture : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "alexa-native-mcp-" + Guid.NewGuid().ToString("N"));
    private readonly Input _input = new();
    private readonly Output _output = new();
    private readonly string? _priorRuntime = Environment.GetEnvironmentVariable("ALEXA_BRIDGE_RUNTIME_DIR");
    private LocalMcpAccess? _access;
    private WebApplication? _app;
    private McpClient? _client;
    private readonly ObservedNotepadLauncher _launcher = new();
    internal IReadOnlyDictionary<int, DateTime> OwnedProcesses => _launcher.Owned;

    internal async Task StartAsync()
    {
        Directory.CreateDirectory(_directory);
        Environment.SetEnvironmentVariable("ALEXA_BRIDGE_RUNTIME_DIR", Path.Combine(_directory, "runtime"));
        _access = LocalMcpAccess.Create();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => {
            options.Configure(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), reloadOnChange: false);
            options.Listen(IPAddress.Loopback, 0);
        });
        var root = new WorkspaceRoot(_directory);
        builder.Services.AddSingleton(new HandoffStore(Path.Combine(_directory, "handoffs")));
        builder.Services.AddSingleton(root);
        builder.Services.AddSingleton(new WorkspaceStore(Path.Combine(_directory, "workspaces"), root));
        builder.Services.AddSingleton(new DesktopActionPolicy(true));
        builder.Services.AddSingleton<IActionApproval>(new ConsoleActionApproval(_input, _output, true, TimeSpan.FromSeconds(10)));
        builder.Services.AddSingleton<INotepadLauncher>(_launcher);
        builder.Services.AddSingleton<IWorkspaceFolderLauncher, WorkspaceFolderLauncher>();
        builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly(typeof(DesktopTools).Assembly);
        _app = builder.Build();
        _app.Use(async (context, next) => {
            if (!_access.IsAuthorized(context.Request.Headers.Authorization.ToString())) { context.Response.StatusCode = 401; return; }
            await next();
        });
        _app.MapMcp("/mcp");
        await _app.StartAsync();
        var token = File.ReadAllText(Path.Combine(_directory, "runtime", "mcp-auth-token")).Trim();
        _client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions {
            Endpoint = new Uri(_app.Urls.Single() + "/mcp"), TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token }
        }));
    }

    internal async Task RestoreThroughMcpAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var client = _client ?? throw new InvalidOperationException("Fixture not started.");
        var ct = timeout.Token;
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        if (tools.Count != 8) throw new InvalidOperationException("Native MCP tool inventory differs from the production tool assembly.");
        var saved = await client.CallToolAsync("save_workspace", new Dictionary<string, object?> {
            ["name"] = "Development", ["relativeFolder"] = ".", ["summary"] = "Native Windows MCP workspace validation",
            ["openFolder"] = false, ["openNotepad"] = true
        }, cancellationToken: ct);
        if (saved.IsError == true) throw new InvalidOperationException("Native workspace fixture save failed.");
        async Task<CallToolResult> RestoreAsync() => await client.CallToolAsync("restore_workspace", new Dictionary<string, object?> { ["name"] = "Development" }, cancellationToken: ct);
        var denied = RestoreAsync();
        var first = await _output.NextAsync(ct);
        _input.Send("deny " + first);
        if ((await denied).IsError != true) throw new InvalidOperationException("Native MCP restore ignored a local denial.");
        var before = ProcessCount();
        if (before != 0) throw new InvalidOperationException("Denied native MCP restore created Notepad or another session appeared.");
        var restored = RestoreAsync();
        var second = await _output.NextAsync(ct);
        if (second == first) throw new InvalidOperationException("Native restore approval challenge was reused.");
        _input.Send("approve " + second);
        var result = await restored;
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(x => x.Text));
        if (result.IsError == true || !text.Contains("process_reported", StringComparison.Ordinal))
            throw new InvalidOperationException("Native MCP workspace restore did not report its actual launch: " + text);
        Console.WriteLine("PASS: actual SDK MCP workspace save, denied restore, fresh local-test-console approval and real Windows Notepad launch.");
        Console.WriteLine("NOTE: local console replies are simulated test input; production operator interaction remains an end-user check.");
    }

    private static int ProcessCount()
    {
        var items = System.Diagnostics.Process.GetProcessesByName("notepad");
        var count = items.Length;
        foreach (var process in items) process.Dispose();
        return count;
    }

    public async ValueTask DisposeAsync()
    {
        _input.Complete();
        if (_client is not null) await _client.DisposeAsync();
        if (_app is not null) { await _app.StopAsync(); await _app.DisposeAsync(); }
        _access?.Dispose();
        Environment.SetEnvironmentVariable("ALEXA_BRIDGE_RUNTIME_DIR", _priorRuntime);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class ObservedNotepadLauncher : INotepadLauncher
    {
        private readonly NotepadLauncher _actual = new();
        internal readonly Dictionary<int, DateTime> Owned = new();
        public bool Supported => _actual.Supported;
        public int? Launch()
        {
            var pid = _actual.Launch();
            if (pid.HasValue)
            {
                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById(pid.Value);
                    if (process.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase)) Owned.TryAdd(pid.Value, process.StartTime);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { }
            }
            return pid;
        }
    }

    private sealed class Input : TextReader
    {
        private readonly BlockingCollection<string> _lines = new();
        internal void Send(string value) => _lines.Add(value);
        internal void Complete() => _lines.CompleteAdding();
        public override string? ReadLine() { try { return _lines.Take(); } catch (InvalidOperationException) { return null; } }
    }
    private sealed class Output : TextWriter
    {
        private readonly Channel<string> _codes = Channel.CreateUnbounded<string>();
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value)
        {
            var match = Regex.Match(value ?? "", @"approve ([0-9A-F]{12})");
            if (match.Success) _codes.Writer.TryWrite(match.Groups[1].Value);
        }
        internal async Task<string> NextAsync(CancellationToken ct) => await _codes.Reader.ReadAsync(ct);
    }
}
