using System.Collections.Generic;
using System.Linq;

namespace BooTrayal.Core.Rules
{
    public readonly struct VoteResult
    {
        public bool Ejected { get; }
        public ulong? Target { get; }
        public IReadOnlyDictionary<ulong, int> Tally { get; }
        public int SkipCount { get; }

        public VoteResult(bool ejected, ulong? target, IReadOnlyDictionary<ulong, int> tally, int skipCount)
        {
            Ejected = ejected;
            Target = target;
            Tally = tally;
            SkipCount = skipCount;
        }
    }

    /// <summary>Meeting vote (GDD 7.4): the unique top target is ejected only if it beats the skip count.</summary>
    public static class VoteResolver
    {
        public static VoteResult Resolve(
            IReadOnlyCollection<ulong> eligibleVoters,
            IReadOnlyCollection<ulong> eligibleTargets,
            IReadOnlyDictionary<ulong, ulong?> ballots)
        {
            var tally = new Dictionary<ulong, int>();
            int skips = 0;
            foreach (var voter in eligibleVoters)
            {
                if (ballots.TryGetValue(voter, out var target) && target.HasValue && eligibleTargets.Contains(target.Value))
                    tally[target.Value] = tally.TryGetValue(target.Value, out var n) ? n + 1 : 1;
                else
                    skips++;
            }

            if (tally.Count == 0) return new VoteResult(false, null, tally, skips);

            int top = tally.Values.Max();
            var leaders = tally.Where(kv => kv.Value == top).Select(kv => kv.Key).ToList();
            bool ejected = leaders.Count == 1 && top > skips;
            return new VoteResult(ejected, ejected ? leaders[0] : (ulong?)null, tally, skips);
        }
    }
}
