using System.Text.Json.Serialization;

namespace Game
{
    /// <summary>
    /// A message sent between the client and server.
    /// </summary>
    class Message
    {
        #region Inner Types
        public enum Type
        {
            ping,
            pong,
            created,
            start,
            opponentDisconnected,
            promptAttack,
            pendingPhrase,
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
}
