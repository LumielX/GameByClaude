using System.Collections.Generic;

namespace BooTrayal.Core.Rules
{
    /// <summary>Shrine bell meetings (GDD 7.4): one ring per living player per night, not while being chased.</summary>
    public sealed class MeetingRules
    {
        public const float DiscussSeconds = 45f;
        public const float VoteSeconds = 15f;

        readonly HashSet<ulong> _rang = new HashSet<ulong>();

        public bool CanRing(ulong player, LifeState state, bool beingChased, bool meetingActive)
        {
            return state == LifeState.Alive && !beingChased && !meetingActive && !_rang.Contains(player);
        }

        public void MarkRang(ulong player) => _rang.Add(player);

        public void ResetForNight() => _rang.Clear();
    }
}
