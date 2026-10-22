using System;
using System.Text;

namespace BooTrayal.Core.Net
{
    /// <summary>Data a client sends with its connection request (NGO ConnectionData).</summary>
    public readonly struct ConnectionPayload
    {
        const string Prefix = "BT1";
        public const int MaxBytes = 256;

        public string Version { get; }
        public string PlayerName { get; }

        ConnectionPayload(string version, string playerName)
        {
            Version = version;
            PlayerName = playerName;
        }

        public static byte[] Encode(string version, string playerName)
        {
            return Encoding.UTF8.GetBytes(Prefix + "\n" + version + "\n" + playerName);
        }

        public static bool TryDecode(byte[] data, out ConnectionPayload payload)
        {
            payload = default;
            if (data == null || data.Length == 0 || data.Length > MaxBytes) return false;
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(data);
            }
            catch (ArgumentException)
            {
                return false;
            }
            var parts = text.Split('\n');
            if (parts.Length != 3 || parts[0] != Prefix) return false;
            payload = new ConnectionPayload(parts[1], parts[2]);
            return true;
        }
    }
}
