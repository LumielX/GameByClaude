using System;
using System.Collections.Generic;

namespace BooTrayal.Core.Player
{
    /// <summary>Server's own record of who is knocked down, so push rules never trust a client-written flag.</summary>
    public sealed class KnockdownLedger
    {
        readonly Dictionary<ulong, double> _downUntil = new Dictionary<ulong, double>();

        public void MarkDown(ulong playerId, double now, double seconds = PushRules.KnockdownSeconds)
        {
            double until = now + seconds;
            _downUntil[playerId] = _downUntil.TryGetValue(playerId, out var existing) ? Math.Max(existing, until) : until;
        }

        public bool IsDown(ulong playerId, double now) => _downUntil.TryGetValue(playerId, out var until) && now < until;

        public void Forget(ulong playerId) => _downUntil.Remove(playerId);
    }
}
