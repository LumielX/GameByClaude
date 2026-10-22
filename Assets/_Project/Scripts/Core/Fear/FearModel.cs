using System;

namespace BooTrayal.Core.Fear
{
    /// <summary>Client-side scare intensities, each 0..1.</summary>
    public readonly struct FearLevels
    {
        public float Whisper { get; }
        public float Heartbeat { get; }
        public float Vignette { get; }
        public float Flicker { get; }
        public float Shake { get; }
        public float Chromatic { get; }

        public FearLevels(float whisper, float heartbeat, float vignette, float flicker, float shake, float chromatic)
        {
            Whisper = whisper;
            Heartbeat = heartbeat;
            Vignette = vignette;
            Flicker = flicker;
            Shake = shake;
            Chromatic = chromatic;
        }
    }

    /// <summary>Fear Proximity bands from GDD 6.2: 25 m whispers, 15 m heartbeat/vignette/flicker, 8 m shake/chromatic.</summary>
    public static class FearModel
    {
        public const float FarRadius = 25f;
        public const float MidRadius = 15f;
        public const float NearRadius = 8f;
        public const float PeakRadius = 2f;

        public static FearLevels Evaluate(float distance)
        {
            if (float.IsNaN(distance) || distance < 0f) return default;
            float whisper = Band(distance, FarRadius, MidRadius);
            float mid = Band(distance, MidRadius, NearRadius);
            float near = Band(distance, NearRadius, PeakRadius);
            return new FearLevels(whisper, mid, mid, mid, near, near);
        }

        /// <summary>0 at <paramref name="start"/> metres, 1 at <paramref name="full"/> metres, smoothstepped.</summary>
        static float Band(float distance, float start, float full)
        {
            float t = (start - distance) / (start - full);
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
