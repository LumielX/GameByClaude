using System;
using System.Collections.Generic;
using BooTrayal.Core.Rules;

namespace BooTrayal.Core.Voice
{
    public enum VoiceChannel { Positional, Meeting, Ghost }

    /// <summary>
    /// Who hears whom (GDD 5.1, 7.4, 13): the living talk by proximity and to the whole room during meetings; mini ghosts
    /// talk only to each other but still hear the living nearby. Also the mic level the Krasue hears (GDD 6.4).
    /// </summary>
    public static class VoiceRules
    {
        public const int AudibleDistance = 32;
        public const int ConversationalDistance = 3;
        public const double EnergyThreshold = 0.15;
        public const double EnergyFull = 0.6;
        public const double ClientSendInterval = 0.5;
        public const double ServerAcceptInterval = 0.4;

        static readonly VoiceChannel[] LivingChannels = { VoiceChannel.Positional, VoiceChannel.Meeting };
        static readonly VoiceChannel[] GhostChannels = { VoiceChannel.Positional, VoiceChannel.Meeting, VoiceChannel.Ghost };
        static readonly VoiceChannel[] None = new VoiceChannel[0];

        public static string ChannelName(string roomCode, VoiceChannel channel)
        {
            if (string.IsNullOrEmpty(roomCode)) return null;
            switch (channel)
            {
                case VoiceChannel.Positional: return $"bt-{roomCode}-pos";
                case VoiceChannel.Meeting: return $"bt-{roomCode}-meet";
                default: return $"bt-{roomCode}-ghost";
            }
        }

        public static IReadOnlyList<VoiceChannel> Joined(LifeState state)
        {
            switch (state)
            {
                case LifeState.Alive:
                case LifeState.Escaped: return LivingChannels;
                case LifeState.MiniGhost: return GhostChannels;
                default: return None;
            }
        }

        public static VoiceChannel? TransmitTo(LifeState state, bool meetingRunning)
        {
            switch (state)
            {
                case LifeState.Alive:
                case LifeState.Escaped: return meetingRunning ? VoiceChannel.Meeting : VoiceChannel.Positional;
                case LifeState.MiniGhost: return VoiceChannel.Ghost;
                default: return null;
            }
        }

        /// <summary>Vivox audio energy (0..1) → noise loudness 0..1: quiet talk is silent to the ghost, shouting is full.</summary>
        public static float LoudnessFromEnergy(double energy)
        {
            if (double.IsNaN(energy) || energy <= EnergyThreshold) return 0f;
            return (float)Math.Min(1.0, (energy - EnergyThreshold) / (EnergyFull - EnergyThreshold));
        }

        /// <summary>Server-side flood guard for mic reports.</summary>
        public static bool AcceptVoice(double now, double lastAccepted) =>
            !double.IsNaN(now) && now - lastAccepted >= ServerAcceptInterval - 1e-6;
    }
}
