using System.Collections.Generic;
using BooTrayal.Core.Rules;

namespace BooTrayal.Core.Economy
{
    /// <summary>Coin values from GDD 4.3.</summary>
    public static class ScoreRules
    {
        public const int SealClosed = 20;
        public const int RitualItemDelivered = 10;
        public const int Escaped = 50;
        public const int CrewWinEach = 50;
        public const int SpyWin = 150;
        public const int CorrectVoteEach = 30;

        public static IReadOnlyDictionary<ulong, int> NightEndAwards(
            IReadOnlyList<NightPlayer> players, NightWinner winner, IReadOnlyCollection<ulong> correctVoters)
        {
            var awards = new Dictionary<ulong, int>();
            void Add(ulong id, int amount) => awards[id] = (awards.TryGetValue(id, out var n) ? n : 0) + amount;

            foreach (var p in players)
            {
                if (p.Role == Role.Crew)
                {
                    if (p.State == LifeState.Escaped) Add(p.Id, Escaped);
                    if (winner == NightWinner.Crew) Add(p.Id, CrewWinEach);
                }
                else if (winner == NightWinner.Spies && !p.Exposed && p.State != LifeState.Disconnected)
                {
                    Add(p.Id, SpyWin);
                }
            }

            foreach (var id in correctVoters) Add(id, CorrectVoteEach);
            return awards;
        }
    }
}
