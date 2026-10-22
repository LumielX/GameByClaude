using System;

namespace BooTrayal.Core
{
    /// <summary>Game version shared by build settings and the network handshake.</summary>
    public static class GameVersion
    {
        public const string Current = "0.1.0";

        /// <summary>Host and client must run the exact same version.</summary>
        public static bool IsCompatible(string host, string client)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(client))
                return false;
            return string.Equals(host.Trim(), client.Trim(), StringComparison.Ordinal);
        }
    }
}
