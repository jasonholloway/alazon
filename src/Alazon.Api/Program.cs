using System.Net.WebSockets;
using System.Text.Json;
using Alazon.LanguageServer;
using Microsoft.AspNetCore.WebSockets;
using StreamJsonRpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddWebSockets(o =>
{
    o.AllowedOrigins.Add("*");
});

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseWebSockets();

app.MapRazorPages();

app.Map("/lsp", async context =>
{
    //todo check protocol for jwt here
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        await LspServer.RunSession(
            new WebSocketMessageHandler(webSocket),
            CancellationToken.None
            );
        
        await webSocket.CloseAsync(
            WebSocketCloseStatus.Empty, 
            "", 
            CancellationToken.None);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

await app.RunAsync();