using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

            // Keep the connection alive until the room closes.
            while (!room.RoomCancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(TimeSpan.FromSeconds(500));
            }
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

            // Keep the connection alive until the room closes.
            while (!room.RoomCancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100);
            }
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
    bool roomIsFull = room.Players.Count == 2;
    if (roomIsFull)
    {
        return null;
    }

    // Add the player to the room.
    room.Players.Add(player);

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
    roomsByKey.Add(room.Name, room);

    // Show the user the room key.
     var createdMessage = new Message
    {
        type = Message.Type.created,
        roomKey = room.Name
    };
    var createdMessageJson = JsonSerializer.Serialize(createdMessage);
    webSocket.SendAsync(
        new ArraySegment<byte>(Encoding.ASCII.GetBytes(createdMessageJson)),
        WebSocketMessageType.Text,
        true,
        CancellationToken.None);

    // Run the room until there are no players.
    room.RunRoom().ContinueWith((task) =>
    {
        Console.WriteLine($"Room: {room.Name} removed");
        roomsByKey.Remove(room.Name);
    });

    return room;
}

/// <summary>
/// A room.
/// </summary>
class Room
{
    #region Public Members
    /// <summary>The room name.</summary>
    public string Name { get; private set;} = string.Empty;

    /// <summary>The players in the room.</summary>
    public List<Player> Players { get; private set; } = new List<Player>();
    
    /// <summary>The cancellation token to close the room.</summary>
    public CancellationToken RoomCancellationToken { get; private set; }
    #endregion

    #region Private Members
    /// <summary>The cancellation token source to close the room.</summary>
    private CancellationTokenSource roomCancellationTokenSource = new CancellationTokenSource();
    /// <summary>The attacking player.</summary>
    private Player attackingPlayer;
    /// <summary>The defending player.</summary>
    private Player defendingPlayer;
    #endregion

    #region Public Methods
    /// <summary>
    /// The constructor.
    /// </summary>
    /// <param name="player">The player who created the room.</param>
    /// <param name="name">The room name.</param>
    public Room(Player player, string name)
    {
        Name = name;
        Players.Add(player);
        RoomCancellationToken = roomCancellationTokenSource.Token;        
    }

    public async Task RunRoom()
    {
        while (true)
        {
            // WAIT FOR THERE TO BE ENOUGH PLAYERS.
            bool enoughPlayers = Players.Count == 2;
            while(!enoughPlayers)
            {
                // Close the room if no players remain.
                updatePlayerList();
                if (!Players.Any())
                {
                    roomCancellationTokenSource.Cancel();
                    return;
                }

                // Keep wating for new players.
                Thread.Sleep(500);
                enoughPlayers = Players.Count == 2;
            }

            // START THE GAME.
            await startGame();
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Starts the game.
    /// </summary>
    /// <returns>A task the completes once the game ends.</returns>
    private async Task startGame()
    {       
        // CHOOSE WHICH PLAYER ATTACKS FIRST.
        // TODO.
        this.attackingPlayer = Players[0];
        this.defendingPlayer = Players[1];

        // PROMPT THE PLAYERS THAT THE GAME IS STARTING.
        Message startMessage = new Message
        {
            type = Message.Type.start,
            roomKey = Name
        };
        await sendMessage(startMessage);

        // START THE GAME LOOP.
        while (true)
        {
            // PROMPT ATTACKING PLAYER.
            Message attackMessage = new Message
                {
                    type = Message.Type.promptAttack,
                };
            bool promptToAttackSent = await sendMessage(attackMessage, this.attackingPlayer);
            if (!promptToAttackSent)
            {
                // Either an error has occured, or the user has disconnected.
                // Remove the player from the room and end the game.
                Players.Remove(this.attackingPlayer);
                break;
            }

            // GET PHRASE AND TIME FROM USER.
            Message? attackResponse = await getMessage(this.attackingPlayer, 20);
            bool attackResponseRetrieved = attackResponse != null;
            if (!attackResponseRetrieved)
            {
                // Either an error has occured, or the user has disconnected.
                // Remove the player from the room and end the game.
                Players.Remove(this.attackingPlayer);
                break;
            }

            // Check if the phrase is valid.
            if (!phraseIsValid(attackResponse?.phrase))
            {
                // Skip the attackers turn.
                this.swapPlayers();
                continue;
            }

            // PROMPT THE DEFENDING PLAYER.
            var defenseMessage = new Message
            {
                type = Message.Type.promptDefense,
                phrase = attackResponse?.phrase,
            };
            bool promptToDefendSent = await sendMessage(defenseMessage, this.defendingPlayer);
            if (!promptToDefendSent)
            {
                // Either an error has occured, or the user has disconnected.
                // Remove the player from the room and end the game.
                Players.Remove(this.defendingPlayer);
                break;
            }
            
            // GET THE TIME TO TYPE THE PHRASE.
            Message? defenseResponse = await getMessage(this.defendingPlayer, 20);
            bool defenseResponseRetrieved = defenseResponse != null;
            if (!defenseResponseRetrieved)
            {
                // Either an error has occured, or the user has disconnected.
                // Remove the player from the room and end the game.
                Players.Remove(this.defendingPlayer);
                break;
            }
            
            // CALCULATE THE DAMAGE.
            double timeDifferenceInSeconds = (defenseResponse.time ?? 0) - (attackResponse.time ?? 0);
            double damageMultiplier = timeDifferenceInSeconds / (attackResponse.time ?? double.MaxValue);
            bool attackLanded = damageMultiplier > .2;
            bool attackCountered = damageMultiplier < -.2;
            if (attackLanded)
            {
                // Apply the damage to the defender.
                this.defendingPlayer.Health -= (int)Math.Floor(15 * damageMultiplier);
            }
            else if (attackCountered)
            {
                // Apply the damage to the attacker.
                this.attackingPlayer.Health += (int)Math.Floor(10 * damageMultiplier);
            }
            else
            {
                // Attack was dodged.
            }

            // SEND THE RESULTS TO THE PLAYERS.
            // Send the results to the attacker.
            var attackerResultMessage = new Message
            {
                type = Message.Type.result,
                health = attackingPlayer.Health,
                resultMessage = $"Attacker time: {attackResponse?.time} Defense time: {defenseResponse?.time} Attack landed: {attackLanded}"
            };
            bool attackResultSent = await sendMessage(attackerResultMessage, this.attackingPlayer);
            if (!attackResultSent)
            {
                // Either an error has occured, or the user has disconnected.
                // Remove the player from the room and end the game.
                Players.Remove(this.attackingPlayer);
                break;
            }

            // Send the results to the defender.
            var defenderResultMessage = new Message
            {
                type = Message.Type.result,
                health = defendingPlayer.Health,
                resultMessage = $"Attacker time: {attackResponse?.time} Defense time: {defenseResponse?.time} Damage: {damageMultiplier}"
            };
            bool defenseResultSent = await sendMessage(defenderResultMessage, this.defendingPlayer);
            if (!defenseResultSent)
            {
                // Either an error has occured, or the user has disconnected.
                // Remove the player from the room and end the game.
                Players.Remove(this.defendingPlayer);
                break;
            }
            
            // Add a small delay to let the players see the results.
            //Thread.Sleep(TimeSpan.FromSeconds(5));

            // END THE GAME IF ANY OF THE PLAYERS LOST.
            bool playerLost = defendingPlayer.Health <= 0 || attackingPlayer.Health <= 0;
            if (playerLost)
            {
                break;
            }

            // SWAP THE PLAYERS.
            this.swapPlayers();
        }
        
        // CHECK IF THE GAME ENDING WAS DUE TO A PLAYER DISCONNECTING.
        bool playerDisconnected = Players.Count != 2;
        if (playerDisconnected)
        {
            // Let the remaining connected player know that the other player disconnected.
            Message playerDisconnectedMessage = new Message
            {
                type = Message.Type.opponentDisconnected
            };
            await sendMessage(playerDisconnectedMessage, Players[0]);
        }

        // RESET THE HEALTH OF THE PLAYERS.
        Players.ForEach(player => player.Health = 100);
    }

    /// <summary>
    /// Removes any players that have disconnected.
    /// </summary>
    private async Task updatePlayerList()
    {
        Players.ForEach(async (player) =>
        {
            // Remove the player if we cannot ping them.
            Message pingMessage = new Message
            {
                type = Message.Type.ping
            };
            bool pingSuccessful = await sendMessage(pingMessage, player);
            if (!pingSuccessful)
            {
                Players.Remove(player);
            }
        });
    }

    /// <summary>
    /// Swaps the attacking and defending players,
    /// </summary>
    private void swapPlayers()
    {
        Player previousAttackingPlayer = attackingPlayer;
        attackingPlayer = defendingPlayer;
        defendingPlayer = previousAttackingPlayer;
    }

    /// <summary>
    /// Checks if the provided phrase is valid.
    /// </summary>
    /// <param name="phrase">The phrase to check.</param>
    /// <returns>True if the phrase is valid; false otherwise.</returns>
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

    /// <summary>
    /// Recieves a message from the specified player.
    /// </summary>
    /// <param name="player">The player to send the message to.</param>
    /// <param name="timeoutInSeconds">The time to wait to receive the message, 300 seconds by default.</param>
    /// <returns>The message from the player; null otherwise.</returns>
    private async Task<Message?> getMessage(Player player, uint timeoutInSeconds = 300)
    {
        try
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
                    CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(timeoutInSeconds));

                return null;
            }

            // Return the message.
            return JsonSerializer.Deserialize<Message>(buffer.Take(receiveResult.Count).ToArray());
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return null;
        }
    }

    /// <summary>
    /// Sends a message to both players, or a specific player if specified.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="player">The player to send the message to.</param>
    /// <returns>True if the message was sent; false otherwise.</returns>
    private async Task<bool> sendMessage(Message message, Player? player = null)
    {
        try
        {
            // Serialize the message.
            var messageJson = JsonSerializer.Serialize(message);

            // Check if a player was was specified.
            bool playerWasSpecified = player != null;
            if (playerWasSpecified)
            {
                // Send to the specific player.
                await player.WebSocketConnection.SendAsync(
                    new ArraySegment<byte>(Encoding.ASCII.GetBytes(messageJson)),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            else
            {
                // Send to both players.
                await this.attackingPlayer.WebSocketConnection.SendAsync(
                    new ArraySegment<byte>(Encoding.ASCII.GetBytes(messageJson)),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
                await this.defendingPlayer.WebSocketConnection.SendAsync(
                    new ArraySegment<byte>(Encoding.ASCII.GetBytes(messageJson)),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }

            // Return that the message was sent successfully.
            return true;
        }
        catch(Exception e)
        {
            Console.Error.WriteLine(e);
            return false;
        }

    }
    #endregion
}

/// <summary>
/// A message sent between the client and server.
/// </summary>
class Message
{
    #region Inner Types
    public enum Type
    {
        ping,
        created,
        start,
        opponentDisconnected,
        promptAttack,
        attackResponse,
        promptDefense,
        defenseResponse,
        result
    }
    #endregion

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Type? type { get; set; }
    public string? phrase { get; set; }
    public double? time { get; set; }
    public int health { get; set; }
    public string? resultMessage { get; set; }
    public string? roomKey { get; set; }
}

/// <summary>
/// A player in the game.
/// </summary>
class Player
{
    public WebSocket? WebSocketConnection { get; set; } = null;
    public string? Name { get; set; } = null;
    public int Health { get; set; } = 100;
}
