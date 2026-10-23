using System;
using UnityEngine;

namespace BooTrayal.Game.Audio
{
    /// <summary>
    /// Placeholder sounds synthesized in code (self-made, nothing to license) until real audio in M7.
    /// Loops use whole numbers of cycles so they repeat without clicks. Clips are cached.
    /// </summary>
    public static class ProceduralClips
    {
        const int Rate = 22050;
        static AudioClip _heartbeat, _wind, _drone, _screech, _boo, _ambience, _whispers, _bell, _click, _hover;

        public static AudioClip Heartbeat() => _heartbeat ??= Make("Heartbeat", 1f, (t, _) =>
        {
            // lub-dub: two low thumps per second
            float a = Thump(t, 0f, 55f) + 0.7f * Thump(t, 0.28f, 48f);
            return a * 0.9f;
        });

        public static AudioClip Wind() => _wind ??= MakeNoise("Wind", 4f, 0.02f, t => 0.35f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / 4f));

        public static AudioClip Drone() => _drone ??= Make("Drone", 4f, (t, _) =>
        {
            float tremolo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            return tremolo * (0.35f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 82.5f * t)
                              + 0.1f * Mathf.Sin(2f * Mathf.PI * 110.25f * t));
        });

        public static AudioClip Screech() => _screech ??= Make("Screech", 0.8f, (t, rng) =>
        {
            float f = Mathf.Lerp(1800f, 900f, t / 0.8f);
            float saw = 2f * (t * f % 1f) - 1f;
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 3f);
            return env * (0.6f * saw + 0.25f * (float)(rng.NextDouble() * 2.0 - 1.0));
        });

        /// <summary>Mini ghost "boo": a wobbly falling hoot.</summary>
        public static AudioClip Boo() => _boo ??= Make("Boo", 0.7f, (t, _) =>
        {
            float f = Mathf.Lerp(420f, 240f, t / 0.7f) * (1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 7f * t));
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.7f - t) / 0.2f);
            return 0.7f * env * Mathf.Sin(2f * Mathf.PI * f * t);
        });

        /// <summary>Night yard: cricket chirp groups over a soft wind bed (8 s loop).</summary>
        public static AudioClip Ambience() => _ambience ??= MakeNoise("Ambience", 8f, 0.01f, t =>
        {
            return 0.18f + 0.08f * Mathf.Sin(2f * Mathf.PI * t / 8f);
        }, (t, noise) =>
        {
            // Crickets: 3 quick 4.6 kHz chirps every 0.8 s, a second insect offset at 0.37 s.
            float a = Chirps(t, 0.8f, 0f, 4600f) + 0.6f * Chirps(t, 1.0f, 0.37f, 5200f);
            return noise + 0.22f * a;
        });

        /// <summary>Third fear layer: breathy, syllable-like noise (6 s loop).</summary>
        public static AudioClip Whispers() => _whispers ??= MakeNoise("Whispers", 6f, 0.25f, t =>
        {
            float syllables = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (3f / 6f) * t * 6f) * Mathf.Sin(2f * Mathf.PI * (5f / 6f) * t);
            float breath = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 6f);
            return 0.35f * Mathf.Clamp01(syllables) * (0.4f + 0.6f * breath);
        });

        /// <summary>The shrine bell: inharmonic partials that ring out (2.5 s).</summary>
        public static AudioClip Bell() => _bell ??= Make("Bell", 2.5f, (t, _) =>
        {
            float f = 520f;
            float v = 0.40f * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 1.2f)
                    + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 2.0f * t) * Mathf.Exp(-t * 1.8f)
                    + 0.18f * Mathf.Sin(2f * Mathf.PI * f * 2.41f * t) * Mathf.Exp(-t * 2.4f)
                    + 0.10f * Mathf.Sin(2f * Mathf.PI * f * 3.03f * t) * Mathf.Exp(-t * 3.5f)
                    + 0.06f * Mathf.Sin(2f * Mathf.PI * f * 4.2f * t) * Mathf.Exp(-t * 5f);
            return v * Mathf.Clamp01(t / 0.004f);
        });

        public static AudioClip UiClick() => _click ??= Make("UiClick", 0.08f, (t, _) =>
            0.6f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1800f, 1100f, t / 0.08f) * t) * Mathf.Exp(-t * 55f));

        public static AudioClip UiHover() => _hover ??= Make("UiHover", 0.05f, (t, _) =>
            0.25f * Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 70f));

        static float Chirps(float t, float period, float offset, float freq)
        {
            float local = (t + period - offset) % period;
            float v = 0f;
            for (int i = 0; i < 3; i++)
            {
                float x = local - i * 0.05f;
                if (x >= 0f && x < 0.035f) v += Mathf.Sin(2f * Mathf.PI * freq * x) * Mathf.Sin(Mathf.PI * x / 0.035f);
            }
            return v;
        }

        static float Thump(float t, float start, float freq)
        {
            float x = t - start;
            if (x < 0f) return 0f;
            return Mathf.Sin(2f * Mathf.PI * freq * x) * Mathf.Exp(-x * 18f);
        }

        static AudioClip Make(string name, float seconds, Func<float, System.Random, float> sample)
        {
            int n = Mathf.RoundToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(name.Length * 7919);
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, rng), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Low-passed noise with a slow envelope; <paramref name="smoothing"/> is the one-pole filter coefficient.</summary>
        static AudioClip MakeNoise(string name, float seconds, float smoothing, Func<float, float> envelope, Func<float, float, float> mix = null)
        {
            float y = 0f;
            return Make(name, seconds, (t, rng) =>
            {
                y += smoothing * ((float)(rng.NextDouble() * 2.0 - 1.0) - y);
                float noise = y * 8f * envelope(t);
                return mix != null ? mix(t, noise) : noise;
            });
        }
    }
}
