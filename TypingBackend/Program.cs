using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Urls.Add("http://*:8080");
app.UseDefaultFiles();
app.UseStaticFiles();

Dictionary<string, Room> roomsByKey = new Dictionary<string, Room>();

// SET UP THE WEBSOCKET.
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(1)
};
app.UseWebSockets(webSocketOptions);

// ALLOW ESTABLISHING WEBSOCKET CONNECTION.
app.Use(async (context, next) =>
{
    // THE PATH FOR CREATING A ROOM.
    if (context.Request.Path == "/createRoom")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            Room room = CreateRoom(webSocket);
            bool roomCreatedSuccefully = room != null;
            if (!roomCreatedSuccefully)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return;
            }

            // Keep the connection alive until the game ends.
            while (!room.GameCancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100);
            }

            // Remove the room.
            roomsByKey.Remove(room.RoomKey);
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
            
            Room room = JoinRoom(webSocket, roomKey);
            bool roomJoinedSuccessfully = room != null;
            if (!roomJoinedSuccessfully)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            // Start the game.
            try
            {
                await room.Run();
            }
            catch(Exception e)
            {

            }

            // Remove the room.
            roomsByKey.Remove(room.RoomKey);
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

app.Run();

Room? JoinRoom(WebSocket webSocket, String roomKey)
{
    // Create the player.
    Player player = new Player
    {
        WebSocketConnection = webSocket,
        Health = 100,
        Name = "bobby2"
    };

    // Get the room.
    bool roomRetrievedSuccessfully = roomsByKey.TryGetValue(roomKey, out Room room);
    if (!roomRetrievedSuccessfully)
    {
        return null;
    }

    // Check if the room is full.
    bool roomIsFull = room.AwayPlayer != null;
    if (roomIsFull)
    {
        return null;
    }

    // Add the player to the room.
    room.AwayPlayer = player;

    return room;
}

Room? CreateRoom(WebSocket webSocket)
{
    // Create the player.
    Player player = new Player
    {
        WebSocketConnection = webSocket,
        Health = 100,
        Name = "bobby"
    };

    // Create a room.
    string 
    roomKey = Guid.NewGuid().ToString();
    Room room = new Room(player, roomKey);

    // Add the room to the list of rooms.
    roomsByKey.Add(room.RoomKey, room);

    // Show the user the room key.
     var createdMessage = new Message
    {
        type = "created",
        roomKey = room.RoomKey
    };
    var createdMessageJson = JsonSerializer.Serialize(createdMessage);
    webSocket.SendAsync(
        new ArraySegment<byte>(Encoding.ASCII.GetBytes(createdMessageJson)),
        WebSocketMessageType.Text,
        true,
        CancellationToken.None);

    return room;
}

class Room
{
    // PUBLIC MEMBERS.
    public string RoomKey { get; private set;}
    public Player HomePlayer { get; private set; }
    public Player? AwayPlayer { get; set; } = null;
    public CancellationToken GameCancellationToken { get; private set; }

    // PRIVATE MEMBERS.
    private CancellationTokenSource gameCancellationTokenSource = new();

    // PUBLIC FUNCTIONS.
    // Constructor.
    public Room(Player player, string roomKey)
    {
        RoomKey = roomKey;
        HomePlayer = player;
        GameCancellationToken = gameCancellationTokenSource.Token;        
    }

    public async Task Run()
    {
        // Choose which player attacks first.
        // TODO.
        Player attackingPlayer = this.HomePlayer;
        Player defendingPlayer = this.AwayPlayer;

        // Prompt the players that the game is starting.
        var startMessage = new Message
        {
            type = "start",
            roomKey = RoomKey
        };

        var startMessageJson = JsonSerializer.Serialize(startMessage);
        await attackingPlayer.WebSocketConnection.SendAsync(
            new ArraySegment<byte>(Encoding.ASCII.GetBytes(startMessageJson)),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
        await defendingPlayer.WebSocketConnection.SendAsync(
            new ArraySegment<byte>(Encoding.ASCII.GetBytes(startMessageJson)),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);

        // Start the game loop.
        while (true)
        {
            // Prompt attacking player.
            var attackMessage = new Message
                {
                    type = "attack",
                };
            var attackMessageJson = JsonSerializer.Serialize(attackMessage);
            await attackingPlayer.WebSocketConnection.SendAsync(
                new ArraySegment<byte>(Encoding.ASCII.GetBytes(attackMessageJson)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            // Get phrase and time from user.
            var buffer = new byte[1024 * 4];
            var receiveResult = await attackingPlayer.WebSocketConnection.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
            var attackResponse = JsonSerializer.Deserialize<Message>(buffer.Take(receiveResult.Count).ToArray());

            // Check if the phrase is valid.
            if (!phraseIsValid(attackResponse?.phrase))
            {
                // Switch the attackers turn.
                attackingPlayer = defendingPlayer;
                defendingPlayer = defendingPlayer == this.HomePlayer ? this.AwayPlayer : this.HomePlayer;
                continue;
            }

            // Prompt the defending player.
            var defenseMessage = new Message
            {
                type = "defend",
                phrase = attackResponse?.phrase,
            };
            var defenseMessageJson = JsonSerializer.Serialize(defenseMessage);
            await defendingPlayer.WebSocketConnection.SendAsync(
                new ArraySegment<byte>(Encoding.ASCII.GetBytes(defenseMessageJson)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            // Get the time to type the phrase.
            receiveResult = await defendingPlayer.WebSocketConnection.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
            var defenseResponse = JsonSerializer.Deserialize<Message>(buffer.Take(receiveResult.Count).ToArray());
            
            // Calculate the damage.
            double timeDifferenceInSeconds = (defenseResponse.time ?? 0) - (attackResponse.time ?? 0);
            double damageMultiplier = timeDifferenceInSeconds / (attackResponse.time ?? double.MaxValue);
            bool attackLanded = damageMultiplier > .2;
            bool attackCountered = damageMultiplier < -.2;
            if (attackLanded)
            {
                // Apply the damage to the defender.
                defendingPlayer.Health -= (int)Math.Floor(15 * damageMultiplier);
            }
            else if (attackCountered)
            {
                // Apply the damage to the attacker.
                attackingPlayer.Health += (int)Math.Floor(10 * damageMultiplier);
            }
            else
            {
                // Attack was dodged.
            }

            // Send the results to the players.
            var attackerResultMessage = new Message
            {
                type = "result",
                health = attackingPlayer.Health,
                resultMessage = $"Attacker time: {attackResponse?.time} Defense time: {defenseResponse?.time} Attack landed: {attackLanded}"
            };
            var defenderResultMessage = new Message
            {
                type = "result",
                health = defendingPlayer.Health,
                resultMessage = $"Attacker time: {attackResponse?.time} Defense time: {defenseResponse?.time} Damage: {damageMultiplier}"
            };
            var attackerResultMessageJson = JsonSerializer.Serialize(attackerResultMessage);
            await attackingPlayer.WebSocketConnection.SendAsync(
                new ArraySegment<byte>(Encoding.ASCII.GetBytes(attackerResultMessageJson)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            var defenderResultMessageJson = JsonSerializer.Serialize(defenderResultMessage);
            await defendingPlayer.WebSocketConnection.SendAsync(
                new ArraySegment<byte>(Encoding.ASCII.GetBytes(defenderResultMessageJson)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            // Switch the players.
            attackingPlayer = defendingPlayer;
            defendingPlayer = defendingPlayer == this.HomePlayer ? this.AwayPlayer : this.HomePlayer;
            
            // Check if any of the players lost.
            bool playerLost = defendingPlayer.Health <= 0 || attackingPlayer.Health <= 0;
            if (playerLost)
            {
                gameCancellationTokenSource.Cancel();
                break;
            }
        }
    }

    // PRIVATE FUNCITONS.
    private bool phraseIsValid(string phrase)
    {
        // Check if there is a message.
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        // Check if all the words are valid.
        return true;
    }

    private async Task<Message?> getPlayerMessage(Player player)
    {
        // Receive the message from the socket.
        var buffer = new byte[1024 * 4];
        var receiveResult = await player.WebSocketConnection.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
        
        // Check if the message was a request to close.
        bool closeMessageReceived = receiveResult.CloseStatus.HasValue;
        if (closeMessageReceived)
        {
            await player.WebSocketConnection.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                CancellationToken.None);

            return new Message
            {
                type = "close"
            };
        }

        // Return the message.
        return JsonSerializer.Deserialize<Message>(buffer.Take(receiveResult.Count).ToArray());
    }
}

class Message
{
    public string? type { get; set; }
    public string? phrase { get; set; }
    public double? time { get; set; }
    public int health { get; set; }
    public string? resultMessage { get; set; }
    public string? roomKey { get; set; }
}

class Player
{
    public WebSocket? WebSocketConnection { get; set; } = null;
    public string? Name { get; set; } = null;
    public int Health { get; set; } = 100;
}
