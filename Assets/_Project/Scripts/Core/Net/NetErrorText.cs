using System;

namespace BooTrayal.Core.Net
{
    /// <summary>Maps Lobby/Relay error message text to reason keys when the error code alone is not specific.</summary>
    public static class NetErrorText
    {
        public static string Classify(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;
            if (Contains(message, "invalid character") || Contains(message, "invalid join code") || Contains(message, "invalid lobby code"))
                return "code_invalid";
            if (Contains(message, "full")) return "room_full";
            if (Contains(message, "not found")) return "room_not_found";
            return null;
        }

        static bool Contains(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
