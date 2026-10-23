using System.Net;
using AlexaBridge;
using ModelContextProtocol.Server;

// Rotate a cryptographically random same-user bearer token on server startup.
// No AI key or user-supplied configuration is required.
using var localAccess = LocalMcpAccess.Create();
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--deny-actions").ToArray());
builder.Services.AddSingleton(new HandoffStore(HandoffStore.DefaultRoot()));
builder.Services.AddSingleton<IActionApproval>(new ConsoleActionApproval(args.Contains("--deny-actions", StringComparer.Ordinal)));
builder.Services.AddSingleton<INotepadLauncher, NotepadLauncher>();
builder.Services.AddSingleton(new DesktopActionPolicy(
    Environment.GetEnvironmentVariable("ALEXA_ALLOW_DESKTOP_ACTIONS") == "1" && !args.Contains("--deny-actions", StringComparer.Ordinal)));
// Never inherit ASP.NET's default wildcard binding: this controls a desktop.
builder.WebHost.ConfigureKestrel(options =>
{
    // Replace inherited Kestrel endpoint configuration as well as URL defaults.
    // Otherwise host environment/appsettings endpoints can override UseUrls.
    options.Configure(new ConfigurationBuilder().Build(), reloadOnChange: false);
    options.Listen(IPAddress.Loopback, 32178);
});
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(DesktopTools).Assembly);

var app = builder.Build();
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host;
    if (host != "127.0.0.1" && host != "localhost" && host != "[::1]" && host != "::1")
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    // Deny browser cross-origin requests (and DNS rebinding). A non-browser MCP
    // client normally sends no Origin header.
    var origin = context.Request.Headers.Origin.ToString();
    if (!string.IsNullOrWhiteSpace(origin) &&
        !string.Equals(origin, "http://127.0.0.1:32178", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(origin, "http://localhost:32178", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    // Require authentication for every MCP request, including GET and DELETE.
    // /health remains unauthenticated but reveals no token or private context.
    if ((context.Request.Path.StartsWithSegments("/mcp") || context.Request.Path.StartsWithSegments("/local")) &&
        !localAccess.IsAuthorized(context.Request.Headers.Authorization.ToString()))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "Bearer";
        return;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    name = "keyboard.wtf Alexa MCP companion",
    status = "local-only",
    processId = Environment.ProcessId,
    boundEndpoints = app.Urls.ToArray(),
    ai_provider = "none",
    note = "Health response does not establish MCP interoperability."
}));
app.MapMcp("/mcp");
// Authenticated lifecycle endpoint used by the launcher that owns this process.
app.MapPost("/local/shutdown", (int processId, HttpContext context, IHostApplicationLifetime lifetime) =>
{
    if (processId != Environment.ProcessId) return Results.StatusCode(StatusCodes.Status409Conflict);
    context.Response.OnCompleted(() => { lifetime.StopApplication(); return Task.CompletedTask; });
    return Results.StatusCode(StatusCodes.Status202Accepted);
});
Console.WriteLine("Local MCP endpoint: http://127.0.0.1:32178/mcp");
app.Run();
