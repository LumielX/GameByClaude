using System;
using BooTrayal.Core.Night;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Night
{
    /// <summary>
    /// Server-side entry points the NightDirector installs while a game runs, so player components do not depend
    /// on it directly. All are null in scenes without a NightDirector (Sandbox).
    /// </summary>
    public static class NightHooks
    {
        /// <summary>(victim, position) → true if the night rules took over (mini ghost + soul orb).</summary>
        public static Func<ulong, Vector3, bool> PlayerKilled;
        /// <summary>(client, power)</summary>
        public static Action<ulong, SpyPower> Power;
        /// <summary>(client, target or null for skip)</summary>
        public static Action<ulong, ulong?> Vote;
        /// <summary>(client) asks to ring the shrine bell.</summary>
        public static Action<ulong> RingBell;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            PlayerKilled = null;
            Power = null;
            Vote = null;
            RingBell = null;
        }
    }
}
