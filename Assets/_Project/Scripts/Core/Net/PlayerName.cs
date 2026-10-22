using System.Globalization;
using System.Text;

namespace BooTrayal.Core.Net
{
    public static class PlayerName
    {
        public const int MaxLength = 16;

        /// <summary>Cleans a typed name; length counts text elements so Thai tone marks stay attached.</summary>
        public static string Sanitize(string raw, int fallbackNumber)
        {
            var sb = new StringBuilder();
            bool pendingSpace = false;
            foreach (var c in raw ?? "")
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }
                if (char.IsControl(c)) continue;
                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }
                sb.Append(c);
            }

            var cleaned = CutToElements(sb.ToString(), MaxLength).TrimEnd();
            return cleaned.Length == 0 ? "Player" + fallbackNumber : cleaned;
        }

        /// <summary>
        /// Keeps the first <paramref name="max"/> characters, counting a base character together with its
        /// combining marks (Thai vowels/tone marks) and surrogate pairs as one. Mono's StringInfo does not group Thai marks.
        /// </summary>
        static string CutToElements(string s, int max)
        {
            int elements = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsLowSurrogate(s[i]) || IsCombining(s[i])) continue;
                if (elements == max) return s.Substring(0, i);
                elements++;
            }
            return s;
        }

        static bool IsCombining(char c)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            return cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark;
        }
    }
}
