using System.Net.WebSockets;
using System.Text.Json.Serialization;

namespace Game
{
    /// <summary>
    /// A player in the game.
    /// </summary>
    class Player
    {
        [JsonIgnore]
        public WebSocket? WebSocketConnection { get; set; } = null;
        public string Name { get; set; } = string.Empty;
        [JsonIgnore]
        public int Health { get; set; } = 100;
    }
}
