using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<WebSocketConnectionManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("AllowAll");
app.UseWebSockets();

// WebSocket endpoint
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var connectionManager = app.Services.GetRequiredService<WebSocketConnectionManager>();
        await connectionManager.HandleWebSocketAsync(context);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Health check endpoint
app.MapGet("/health", () => "OK");

app.Run();

// WebSocket Connection Manager
public class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task HandleWebSocketAsync(HttpContext context)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();
        var connection = new WebSocketConnection(connectionId, webSocket);

        _connections.TryAdd(connectionId, connection);

        try
        {
            await HandleMessagesAsync(connection);
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            if (!string.IsNullOrEmpty(connection.Username))
            {
                await BroadcastUserDisconnectedAsync(connection.Username);
                await BroadcastConnectedUsersAsync();
            }
        }
    }

    private async Task HandleMessagesAsync(WebSocketConnection connection)
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);

        while (connection.WebSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await connection.WebSocket.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                    await ProcessMessageAsync(connection, message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "Closed by client", CancellationToken.None);
                    break;
                }
            }
            catch (WebSocketException)
            {
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(WebSocketConnection connection, string messageJson)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ChatMessage>(messageJson, _jsonOptions);
            if (message == null) return;

            switch (message.Type)
            {
                case "join":
                    connection.Username = message.Username;
                    await BroadcastUserJoinedAsync(message.Username);
                    await BroadcastConnectedUsersAsync();
                    break;

                case "message":
                    if (!string.IsNullOrEmpty(connection.Username))
                    {
                        await BroadcastMessageAsync(new ChatMessage
                        {
                            Type = "message",
                            Username = connection.Username,
                            Content = message.Content,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    break;
            }
        }
        catch (JsonException)
        {
            // Invalid message format
        }
    }

    private async Task BroadcastMessageAsync(ChatMessage message)
    {
        var messageJson = JsonSerializer.Serialize(message, _jsonOptions);
        await BroadcastAsync(messageJson);
    }

    private async Task BroadcastUserJoinedAsync(string username)
    {
        var message = new ChatMessage
        {
            Type = "user-joined",
            Username = username,
            Content = $"{username} joined the chat",
            Timestamp = DateTime.UtcNow
        };
        await BroadcastAsync(JsonSerializer.Serialize(message, _jsonOptions));
    }

    private async Task BroadcastUserDisconnectedAsync(string username)
    {
        var message = new ChatMessage
        {
            Type = "user-left",
            Username = username,
            Content = $"{username} left the chat",
            Timestamp = DateTime.UtcNow
        };
        await BroadcastAsync(JsonSerializer.Serialize(message, _jsonOptions));
    }

    private async Task BroadcastConnectedUsersAsync()
    {
        var users = _connections.Values
            .Where(c => !string.IsNullOrEmpty(c.Username))
            .Select(c => c.Username)
            .Distinct()
            .ToList();

        var message = new
        {
            Type = "users-list",
            Users = users
        };

        await BroadcastAsync(JsonSerializer.Serialize(message, _jsonOptions));
    }

    private async Task BroadcastAsync(string message)
    {
        var tasks = _connections.Values
            .Where(c => c.WebSocket.State == WebSocketState.Open)
            .Select(c => SendAsync(c, message));

        await Task.WhenAll(tasks);
    }

    private async Task SendAsync(WebSocketConnection connection, string message)
    {
        if (connection.WebSocket.State == WebSocketState.Open)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await connection.WebSocket.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

public class WebSocketConnection
{
    public string Id { get; }
    public WebSocket WebSocket { get; }
    public string? Username { get; set; }

    public WebSocketConnection(string id, WebSocket webSocket)
    {
        Id = id;
        WebSocket = webSocket;
    }
}

public class ChatMessage
{
    public string Type { get; set; } = "";
    public string? Username { get; set; }
    public string? Content { get; set; }
    public DateTime Timestamp { get; set; }
}
