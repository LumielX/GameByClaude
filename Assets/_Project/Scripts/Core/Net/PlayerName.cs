using System.Globalization;
using System.Text;

namespace BooTrayal.Core.Net
{
    public static class PlayerName
    {
        public const int MaxLength = 16;
        /// <summary>UTF-8 budget so the connection payload always stays under ConnectionPayload.MaxBytes.</summary>
        public const int MaxBytes = 96;
        /// <summary>Thai stacks at most a vowel and a tone mark on one consonant; more is spam/zalgo.</summary>
        public const int MaxMarksPerCharacter = 2;

        /// <summary>
        /// Cleans a typed name. Length counts a base character with its combining marks as one
        /// (Mono's StringInfo does not group Thai marks), drops control/format characters,
        /// collapses whitespace and keeps the result within <see cref="MaxBytes"/>.
        /// </summary>
        public static string Sanitize(string raw, int fallbackNumber)
        {
            var result = new StringBuilder();
            var element = new StringBuilder();
            int elements = 0;
            int marks = 0;
            bool pendingSpace = false;
            bool full = false;

            void Flush()
            {
                if (element.Length == 0 || full) return;
                if (elements == MaxLength ||
                    Encoding.UTF8.GetByteCount(result.ToString() + element) > MaxBytes)
                {
                    full = true;
                    element.Clear();
                    return;
                }
                result.Append(element);
                elements++;
                element.Clear();
            }

            foreach (var c in raw ?? "")
            {
                if (full) break;
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (char.IsWhiteSpace(c))
                {
                    Flush();
                    pendingSpace = result.Length > 0;
                    continue;
                }
                if (char.IsControl(c) || cat == UnicodeCategory.Format) continue;
                if (IsCombining(cat))
                {
                    if (element.Length == 0 || marks >= MaxMarksPerCharacter) continue;
                    element.Append(c);
                    marks++;
                    continue;
                }
                if (char.IsLowSurrogate(c) && element.Length > 0)
                {
                    element.Append(c);
                    continue;
                }

                Flush();
                if (full) break;
                if (pendingSpace)
                {
                    element.Append(' ');
                    Flush();
                    pendingSpace = false;
                    if (full) break;
                }
                element.Append(c);
                marks = 0;
            }
            Flush();

            var cleaned = result.ToString().TrimEnd();
            return cleaned.Length == 0 ? "Player" + fallbackNumber : cleaned;
        }

        static bool IsCombining(UnicodeCategory cat)
        {
            return cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark;
        }
    }
}
