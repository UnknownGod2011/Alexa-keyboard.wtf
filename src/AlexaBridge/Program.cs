using System.Net;
using AlexaBridge;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
// Never inherit ASP.NET's default wildcard binding: this controls a desktop.
builder.WebHost.UseUrls("http://127.0.0.1:32178");
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

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
    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    name = "keyboard.wtf Alexa MCP companion",
    status = "local-only",
    ai_provider = "none",
    note = "Health response does not establish MCP interoperability."
}));
app.MapMcp("/mcp");
Console.WriteLine("Local MCP endpoint: http://127.0.0.1:32178/mcp");
app.Run();
