using System.Text;

namespace BooTrayal.Core.Net
{
    /// <summary>Room codes as players type them: forgiving on case, spaces and dashes.</summary>
    public static class JoinCode
    {
        public const int Length = 6;

        public static string Normalize(string raw)
        {
            if (raw == null) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsWhiteSpace(c) || c == '-') continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        public static bool IsValid(string normalized)
        {
            if (normalized == null || normalized.Length != Length) return false;
            foreach (var c in normalized)
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            return true;
        }
    }
}
