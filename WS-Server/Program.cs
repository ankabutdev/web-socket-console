using System.Net;
using System.Net.WebSockets;
using System.Text;

// Source
// https://github.com/slthomason/StartupHakk/blob/main/92_Implementing_WebSocket_Client_Server_ASPNETCORE/WS-Server-Multiple.cs

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:6969");
var app = builder.Build();
app.UseWebSockets();
var connections = new List<WebSocket>();
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var curName = context.Request.Query["name"];

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        connections.Add(ws);

        await Broadcast($"{curName} joined the room");
        await Broadcast($"{connections.Count} users connected");
        await ReciveMessage(ws,
            async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await Broadcast(curName + ": " + message);
                }
                else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                {
                    connections.Remove(ws);
                    await Broadcast($"{curName} left the room");
                    await Broadcast($"{connections.Count} users connected");
                    await ws.CloseAsync(result.CloseStatus.Value,
                        result.CloseStatusDescription, CancellationToken.None);
                }
            });

        //while (true)
        //{
        //    var message = "The curret time is: " + DateTime.Now + 5.ToString("HH:mm:ss");
        //    var bytes = Encoding.UTF8.GetBytes(message);
        //    var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
        //    if (ws.State == WebSocketState.Open)
        //        await ws.SendAsync(arraySegment,
        //                        WebSocketMessageType.Text,
        //                        true,
        //                        CancellationToken.None);
        //    else if (ws.State == WebSocketState.Closed || ws.State == WebSocketState.Aborted)
        //    {
        //        break;
        //    }
        //    Thread.Sleep(1000);
        //}
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task Broadcast(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    foreach (var socket in connections)
    {
        if (socket.State == WebSocketState.Open)
        {
            var arraySegment = new ArraySegment<byte>(bytes,
                0, bytes.Length);

            await socket.SendAsync(arraySegment,
                WebSocketMessageType.Text,
                true, CancellationToken.None);
        }
    }
}

async Task ReciveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer),
            CancellationToken.None);

        handleMessage(result, buffer);
    }
}

await app.RunAsync();
