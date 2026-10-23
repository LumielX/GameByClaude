using System;
using System.Collections.Generic;

namespace BooTrayal.Core.Night
{
    /// <summary>Spy powers in the vertical slice (GDD 7.2). The other three come with content (M12).</summary>
    public enum SpyPower : byte { SabotagePuzzle, Blackout }

    public static class SpyPowerRules
    {
        public const float SabotageRange = 3f;
        public const float BlackoutSeconds = 10f;
        /// <summary>Cold breath clue shown on the Spy after any power (GDD 7.3).</summary>
        public const float TraceSeconds = 3f;

        public static double Cooldown(SpyPower power)
        {
            switch (power)
            {
                case SpyPower.SabotagePuzzle: return 60.0;
                case SpyPower.Blackout: return 75.0;
                default: throw new ArgumentOutOfRangeException(nameof(power));
            }
        }
    }

    /// <summary>One Spy's per-power cooldowns, on the night clock.</summary>
    public sealed class SpyPowerCooldowns
    {
        const double Epsilon = 1e-4;
        readonly Dictionary<SpyPower, double> _usedAt = new Dictionary<SpyPower, double>();

        public bool Ready(SpyPower power, double now) =>
            !_usedAt.TryGetValue(power, out var at) || now - at >= SpyPowerRules.Cooldown(power) - Epsilon;

        public void Use(SpyPower power, double now) => _usedAt[power] = now;
    }
}
