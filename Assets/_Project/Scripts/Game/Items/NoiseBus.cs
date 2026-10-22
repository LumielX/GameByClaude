using System;
using BooTrayal.Core.Player;
using UnityEngine;

namespace BooTrayal.Game.Items
{
    /// <summary>Server-side channel for sounds the ghost can hear (M4 subscribes).</summary>
    public static class NoiseBus
    {
        public static event Action<Vector3, Noise> Raised;

        public static void Raise(Vector3 position, Noise noise)
        {
            if (noise.Loudness <= 0f) return;
            Debug.Log($"NOISE loud={noise.Loudness:0.00} r={noise.Radius:0.0} at={position}");
            Raised?.Invoke(position, noise);
        }
    }
}
