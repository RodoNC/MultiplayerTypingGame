using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Game;

// SETUP APPLICATION
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Urls.Add("http://*:8080");
app.Urls.Add("http://*:80");
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// Set up websockets.
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(1)
};
app.UseWebSockets(webSocketOptions);

// THE ROOMS IN THE APPLICATION.
Dictionary<string, Room> roomsByKey = new Dictionary<string, Room>();

// THE ENDPOINTS FOR ESTABLISHING WEBSOCKET CONNECTIONS.
app.Use(async (context, next) =>
{
    // THE PATH FOR CREATING A ROOM.
    if (context.Request.Path == "/createRoom")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            Player player = new Player
            {
                WebSocketConnection = webSocket,
                Health = 100,
                Name = "Billy"
            };
            Room? room = CreateRoom(player);
            bool roomCreatedSuccefully = room != null;
            if (!roomCreatedSuccefully)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return;
            }

            // Keep the connection alive until the websocket is closed.
            while (webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(1000);
            }
            room!.Players.Remove(player);
            Console.WriteLine("Creator left");
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    // THE PATH FOR JOINING A ROOM.
    else if (context.Request.Path == "/joinRoom")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            string? roomKey = context.Request.Query["roomKey"];
            if (roomKey == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            Player player = new Player
            {
                WebSocketConnection = webSocket,
                Health = 100,
                Name = "Bobby"
            };
            Room? room = JoinRoom(player, roomKey);
            bool roomJoinedSuccessfully = room != null;
            if (!roomJoinedSuccessfully)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // Keep the connection alive until the websocket is closed.
            while (webSocket.State == WebSocketState.Open)
            {
                await Task.Delay(100);
            }
            room!.Players.Remove(player);
            Console.WriteLine("Joiner left");
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
    else
    {
        await next(context);
    }
});

// THE ENDPOINT FOR GETTING THE LIST OF ROOMS.
app.MapGet("/getRooms", () =>
{
    string roomsJson = JsonSerializer.Serialize(roomsByKey.Values);
    return roomsJson;
});

app.Run();

// HELPER FUNCTIONS.
// Allow a user to join the room with the given room key.
Room? JoinRoom(Player player, string roomKey)
{
    // Get the room.
    bool roomRetrievedSuccessfully = roomsByKey.TryGetValue(roomKey, out Room? room);
    if (!roomRetrievedSuccessfully)
    {
        return null;
    }

    // Check if the room is full.
    bool roomIsFull = room!.Players.Count == 2;
    if (roomIsFull)
    {
        return null;
    }

    // Add the player to the room.
    room.Players.Add(player);

    return room;
}

// Creates a room.
Room? CreateRoom(Player player)
{
    // Create a room.
    string roomKey = Guid.NewGuid().ToString().Substring(0, 4);
    Room room = new Room(player, roomKey);

    // Add the room to the list of rooms.
    roomsByKey.Add(room.RoomKey, room);

    // Show the user the room key.
     var createdMessage = new Message
    {
        type = Message.Type.created,
        roomKey = room.RoomKey
    };
    var createdMessageJson = JsonSerializer.Serialize(createdMessage);
    player.WebSocketConnection!.SendAsync(
        new ArraySegment<byte>(Encoding.ASCII.GetBytes(createdMessageJson)),
        WebSocketMessageType.Text,
        true,
        CancellationToken.None);

    // Run the room until there are no players.
    Task.Run(room.RunRoom).ContinueWith((task) =>
    {
        Console.WriteLine($"Room: {room.RoomKey} removed");
        roomsByKey.Remove(room.RoomKey);
    });

    return room;
}
