using System.Collections.Generic;

namespace BooTrayal.Core.Net
{
    /// <summary>
    /// Turns a client-side disconnect into a reason key. NGO fills DisconnectReason with transport text
    /// (e.g. "[Disconnect Event]...[ProtocolTimeout]") when the host vanishes, so only known keys are trusted.
    /// </summary>
    public static class DisconnectClassifier
    {
        static readonly HashSet<string> Rejections = new HashSet<string>
        {
            "version_mismatch", "room_full", "game_in_progress", "bad_payload"
        };

        /// <summary>Sent by the host when it removes a player from the lobby.</summary>
        public const string Kicked = "kicked";

        public static bool IsRejection(string key) => key != null && Rejections.Contains(key);

        public static string Classify(bool wasConnected, string disconnectReason)
        {
            if (IsRejection(disconnectReason) || disconnectReason == Kicked) return disconnectReason;
            return wasConnected ? "host_left" : "connect_failed";
        }
    }
}
