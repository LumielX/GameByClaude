using System;
using System.Collections.Generic;

namespace BooTrayal.Core.Rules
{
    /// <summary>Player-count driven rules from GDD 4.1 and 7.1.</summary>
    public static class RoomRules
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 8;
        public const int SpyMinPlayers = 5;
        public static readonly int[] NightMinuteOptions = { 8, 10, 12, 15 };
        public static readonly int[] NightOptions = { 1, 3, 5 };

        public static bool CanToggleSpy(int players) => players >= SpyMinPlayers;

        public static int MaxSpiesAllowed(int players)
        {
            if (players < SpyMinPlayers) return 0;
            return players >= 7 ? 2 : 1;
        }

        public static int SpyCount(int players, bool spyEnabled, int requestedSpies)
        {
            if (!spyEnabled) return 0;
            int max = MaxSpiesAllowed(players);
            if (max == 0) return 0;
            return Math.Max(1, Math.Min(requestedSpies, max));
        }

        public static int SealCount(int players)
        {
            if (players <= 2) return 3;
            if (players <= 4) return 4;
            if (players <= 6) return 5;
            return 6;
        }

        public static IReadOnlyList<string> Validate(GameConfig c)
        {
            var errors = new List<string>();
            if (c.MaxPlayers < MinPlayers || c.MaxPlayers > MaxPlayers) errors.Add("max_players_range");
            if (Array.IndexOf(NightMinuteOptions, c.NightMinutes) < 0) errors.Add("night_minutes_invalid");
            if (Array.IndexOf(NightOptions, c.Nights) < 0) errors.Add("nights_invalid");
            if (c.RequestedSpies < 1 || c.RequestedSpies > 2 ||
                (c.SpyEnabled && c.RequestedSpies > Math.Max(1, MaxSpiesAllowed(c.MaxPlayers))))
                errors.Add("spies_invalid");
            return errors;
        }
    }
}
