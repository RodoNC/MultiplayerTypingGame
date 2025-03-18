using System.Net.WebSockets;

namespace Game
{
    /// <summary>
    /// A player in the game.
    /// </summary>
    class Player
    {
        public WebSocket? WebSocketConnection { get; set; } = null;
        public string? Name { get; set; } = null;
        public int Health { get; set; } = 100;
    }
}
