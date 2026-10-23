using System;

namespace BooTrayal.Core.Player
{
    public readonly struct Noise
    {
        /// <summary>0..1</summary>
        public float Loudness { get; }
        /// <summary>Hearing radius in metres.</summary>
        public float Radius { get; }

        public Noise(float loudness, float radius)
        {
            Loudness = loudness;
            Radius = radius;
        }
    }

    /// <summary>Sounds the ghost can hear (GDD 1.4/5): dropped items, dragging, running.</summary>
    public static class NoiseRules
    {
        public const float MinImpulse = 2f;
        const float ImpulseRange = 10f;
        const float BaseRadius = 4f;
        const float RadiusPerLoudness = 16f;
        const float RunLoudness = 0.25f;

        public static Noise FromImpact(float impulse, bool heavy)
        {
            if (float.IsNaN(impulse) || impulse < MinImpulse) return new Noise(0f, 0f);
            float loud = (impulse - MinImpulse) / ImpulseRange;
            if (heavy) loud *= 2f;
            return Make(Math.Min(1f, loud));
        }

        public static Noise Footstep(bool running, bool crouching)
        {
            return running && !crouching ? Make(RunLoudness) : new Noise(0f, 0f);
        }

        /// <summary>Voice level 0..1 (scream = 1, later the mic level from Vivox). Krasue hears this (GDD 6.4).</summary>
        public static Noise FromVoice(float level)
        {
            if (float.IsNaN(level) || level < MinVoiceLevel) return new Noise(0f, 0f);
            return Make(Math.Min(1f, level));
        }

        public const float MinVoiceLevel = 0.05f;

        static Noise Make(float loudness) => new Noise(loudness, BaseRadius + RadiusPerLoudness * loudness);
    }
}
