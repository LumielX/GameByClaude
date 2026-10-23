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
            int marks = 0;
            foreach (var c in raw)
            {
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format) continue;
                if (PlayerName.IsCombining(cat))
                {
                    // Same rule as names: a vowel + tone mark per letter is Thai, a tower of marks is "zalgo" spam.
                    if (sb.Length == 0 || space || marks >= PlayerName.MaxMarksPerCharacter) continue;
                    marks++;
                }
                else marks = 0;
                if (space) { sb.Append(' '); space = false; }
                if (sb.Length >= MaxLength) break;
                sb.Append(c);
            }
            if (sb.Length > MaxLength) sb.Length = MaxLength;
            // Never leave half of a surrogate pair (it renders as a box).
            if (sb.Length > 0 && char.IsHighSurrogate(sb[sb.Length - 1])) sb.Length--;
            var text = sb.ToString().TrimEnd();
            return text.Length == 0 ? null : text;
        }
    }
}
