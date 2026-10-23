using System.Globalization;
using System.Text;

namespace BooTrayal.Core.Net
{
    /// <summary>Lobby chat text, cleaned on the server before anyone sees it.</summary>
    public static class ChatRules
    {
        public const int MaxLength = 120;

        /// <summary>Trimmed, whitespace runs collapsed, control/format characters (incl. RTL overrides) removed, ≤ 120 chars; null if nothing is left.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var sb = new StringBuilder(raw.Length);
            bool space = false;
            foreach (var c in raw)
            {
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format) continue;
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
                if (sb.Length >= MaxLength) break;
            }
            var text = sb.ToString();
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            return text.Length == 0 ? null : text;
        }
    }
}
