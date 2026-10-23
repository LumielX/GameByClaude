using System;
using System.Collections.Generic;
using UnityEngine;

namespace BooTrayal.Game.Ghosts
{
    /// <summary>Every spawned ghost on this peer (server and clients), for Fear distance checks.</summary>
    public static class GhostRegistry
    {
        static readonly List<Transform> _active = new List<Transform>();

        public static IReadOnlyList<Transform> Active => _active;

        public static void Register(Transform ghost)
        {
            if (ghost != null && !_active.Contains(ghost)) _active.Add(ghost);
        }

        public static void Unregister(Transform ghost) => _active.Remove(ghost);

        /// <summary>Distance from <paramref name="position"/> to the nearest ghost, or +∞ if there is none.</summary>
        public static float NearestDistance(Vector3 position)
        {
            float best = float.PositiveInfinity;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i] == null) { _active.RemoveAt(i); continue; }
                best = Math.Min(best, Vector3.Distance(position, _active[i].position));
            }
            return best;
        }
    }

    /// <summary>Who the ghost may grab. M5's NightDirector sets this so unexposed Spies are spared (GDD 6.1).</summary>
    public static class GhostTargeting
    {
        public static Func<ulong, bool> IsHarmable = _ => true;
    }
}
