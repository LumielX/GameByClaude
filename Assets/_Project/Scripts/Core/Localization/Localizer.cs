using System;
using System.Collections.Generic;
using System.Globalization;

namespace BooTrayal.Core.Localization
{
    public enum Language { Thai, English }

    /// <summary>
    /// Thai/English text tables (GDD 11). Tables are plain <c>key=value</c> lines so the owner can edit them;
    /// a missing key falls back to the other language, then to the key itself.
    /// </summary>
    public sealed class Localizer
    {
        readonly Dictionary<Language, IReadOnlyDictionary<string, string>> _tables = new Dictionary<Language, IReadOnlyDictionary<string, string>>();

        public Language Current { get; private set; } = Language.Thai;
        public event Action Changed;

        /// <summary><c>key=value</c> per line; <c>#</c> comments and blank lines skipped; <c>\n</c> means a new line.</summary>
        public static IReadOnlyDictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                if (key.Length > 0) result[key] = value;
            }
            return result;
        }

        public void Load(Language language, IReadOnlyDictionary<string, string> table) => _tables[language] = table;

        public void Set(Language language)
        {
            if (language == Current) return;
            Current = language;
            Changed?.Invoke();
        }

        public bool Has(string key) => Lookup(Current, key) != null || Lookup(Other, key) != null;

        public string Get(string key) => Lookup(Current, key) ?? Lookup(Other, key) ?? key;

        public string Format(string key, params object[] args)
        {
            var pattern = Get(key);
            try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
            catch (FormatException) { return pattern; }
        }

        Language Other => Current == Language.Thai ? Language.English : Language.Thai;

        string Lookup(Language language, string key) =>
            key != null && _tables.TryGetValue(language, out var t) && t.TryGetValue(key, out var v) ? v : null;
    }
}
