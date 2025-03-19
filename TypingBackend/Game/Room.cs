using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game
{
    /// <summary>
    /// A room.
    /// </summary>
    class Room
    {
        #region Public Members
        /// <summary>The room name.</summary>
        public string RoomKey { get; private set;} = string.Empty;

        /// <summary>The players in the room.</summary>
        public List<Player> Players { get; private set; } = new List<Player>();
        
        /// <summary>The cancellation token to close the room.</summary>
        [JsonIgnore]
        public CancellationToken RoomCancellationToken { get; private set; }
        #endregion

        #region Private Members
        /// <summary>The cancellation token source to close the room.</summary>
        private CancellationTokenSource roomCancellationTokenSource = new CancellationTokenSource();
        /// <summary>The attacking player.</summary>
        private Player? attackingPlayer;
        /// <summary>The defending player.</summary>
        private Player? defendingPlayer;
        #endregion

        #region Public Methods
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="player">The player who created the room.</param>
        /// <param name="name">The room name.</param>
        public Room(Player player, string name)
        {
            RoomKey = name;
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
                    await updatePlayerList();
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
                roomKey = RoomKey
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

                // GET PENDING PHRASE TO DISPLAY TO THE DEFENDER.
                Message? pendingPhrase = await getMessage(this.attackingPlayer, 20);
                bool pendingPhraseRetrieved = pendingPhrase != null;
                if (!pendingPhraseRetrieved)
                {
                    // Either an error has occured, or the user has disconnected.
                    // Remove the player from the room and end the game.
                    Players.Remove(this.attackingPlayer);
                    break;
                }
                while (pendingPhrase!.type == Message.Type.pendingPhrase)
                {
                    // Send the pending message to the defender.
                    var pendingPhraseMessage = new Message
                    {
                        type = Message.Type.pendingPhrase,
                        phrase = pendingPhrase!.phrase,
                    };
                    bool pendingPhraseSent = await sendMessage(pendingPhraseMessage, defendingPlayer);
                    if (!pendingPhraseSent)
                    {
                        // Either an error has occured, or the user has disconnected.
                        // Remove the player from the room and end the game.
                        Players.Remove(defendingPlayer);
                        break;
                    }

                    // Get the pending message from the attacker.
                    pendingPhrase = await getMessage(this.attackingPlayer, 20);
                    pendingPhraseRetrieved = pendingPhrase != null;
                    if (!pendingPhraseRetrieved)
                    {
                        // Either an error has occured, or the user has disconnected.
                        // Remove the player from the room and end the game.
                        Players.Remove(this.attackingPlayer);
                        break;
                    }
                }

                // GET COMPLETE PHRASE AND TIME FROM USER.
                Message? attackResponse = pendingPhrase;
                bool attackResponseRetrieved = attackResponse != null;
                if (!attackResponseRetrieved)
                {
                    // Either an error has occured, or the user has disconnected.
                    // Remove the player from the room and end the game.
                    Players.Remove(this.attackingPlayer);
                    break;
                }

                // Check if the phrase is valid.
                if (!phraseIsValid(attackResponse!.phrase))
                {
                    // Skip the attackers turn.
                    this.swapPlayers();
                    continue;
                }

                // PROMPT THE DEFENDING PLAYER.
                var defenseMessage = new Message
                {
                    type = Message.Type.promptDefense,
                    phrase = attackResponse!.phrase,
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
                double timeDifferenceInSeconds = (defenseResponse!.time ?? 0) - (attackResponse!.time ?? 0);
                double damageMultiplier = timeDifferenceInSeconds / (attackResponse!.time ?? double.MaxValue);
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
                    resultMessage = $"Attacker time: {attackResponse!.time} Defense time: {defenseResponse!.time} Attack landed: {attackLanded}"
                };
                bool attackResultSent = await sendMessage(attackerResultMessage, attackingPlayer);
                if (!attackResultSent)
                {
                    // Either an error has occured, or the user has disconnected.
                    // Remove the player from the room and end the game.
                    Players.Remove(attackingPlayer);
                    break;
                }

                // Send the results to the defender.
                var defenderResultMessage = new Message
                {
                    type = Message.Type.result,
                    health = defendingPlayer.Health,
                    resultMessage = $"Attacker time: {attackResponse!.time} Defense time: {defenseResponse!.time} Damage: {damageMultiplier}"
                };
                bool defenseResultSent = await sendMessage(defenderResultMessage, defendingPlayer);
                if (!defenseResultSent)
                {
                    // Either an error has occured, or the user has disconnected.
                    // Remove the player from the room and end the game.
                    Players.Remove(defendingPlayer);
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
        private  async Task updatePlayerList()
        {
            foreach (Player player in Players)
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
            };
        }

        /// <summary>
        /// Swaps the attacking and defending players,
        /// </summary>
        private void swapPlayers()
        {
            Player previousAttackingPlayer = attackingPlayer!;
            attackingPlayer = defendingPlayer;
            defendingPlayer = previousAttackingPlayer;
        }

        /// <summary>
        /// Checks if the provided phrase is valid.
        /// </summary>
        /// <param name="phrase">The phrase to check.</param>
        /// <returns>True if the phrase is valid; false otherwise.</returns>
        private bool phraseIsValid(string? phrase)
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
                var receiveResult = await player.WebSocketConnection!.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);
                
                // Check if the message was a request to close.
                bool closeMessageReceived = receiveResult.CloseStatus.HasValue;
                if (closeMessageReceived)
                {
                    await player.WebSocketConnection.CloseAsync(
                        receiveResult.CloseStatus!.Value,
                        receiveResult.CloseStatusDescription,
                        CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(timeoutInSeconds));

                    return null;
                }

                // Return the message.
                Message? message = JsonSerializer.Deserialize<Message>(buffer.Take(receiveResult.Count).ToArray());
                Console.WriteLine(message);
                return message;
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
                    await player!.WebSocketConnection!.SendAsync(
                        new ArraySegment<byte>(Encoding.ASCII.GetBytes(messageJson)),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                else
                {
                    // Send to both players.
                    await attackingPlayer!.WebSocketConnection!.SendAsync(
                        new ArraySegment<byte>(Encoding.ASCII.GetBytes(messageJson)),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                    await defendingPlayer!.WebSocketConnection!.SendAsync(
                        new ArraySegment<byte>(Encoding.ASCII.GetBytes(messageJson)),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
        
                Console.WriteLine($"Sent: {messageJson}");
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
}
