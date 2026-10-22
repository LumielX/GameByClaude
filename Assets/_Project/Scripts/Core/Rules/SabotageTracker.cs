using System.Collections.Generic;
using System.Linq;

namespace BooTrayal.Core.Rules
{
    /// <summary>Spy secret goal (GDD 4.2): 3 successful sabotages, or 4 combined with two Spies.</summary>
    public sealed class SabotageTracker
    {
        readonly HashSet<ulong> _spies = new HashSet<ulong>();
        readonly HashSet<ulong> _caught = new HashSet<ulong>();

        public SabotageTracker(int spyCount)
        {
            Required = spyCount >= 2 ? 4 : 3;
        }

        public int Required { get; }
        public int Successes { get; private set; }
        public bool GoalReached => Successes >= Required;
        /// <summary>Latched when an uncaught Spy completes the goal; catching the Spy later does not undo it (GDD 4.2).</summary>
        public bool GoalWon { get; private set; }
        public IReadOnlyCollection<ulong> Spies => _spies;
        public bool AnyUncaught => _spies.Any(s => !_caught.Contains(s));

        public void Register(ulong spyId) => _spies.Add(spyId);

        public void MarkCaught(ulong spyId) => _caught.Add(spyId);

        public bool IsCaught(ulong spyId) => _caught.Contains(spyId);

        /// <summary>Returns true when the sabotage counted toward the goal.</summary>
        public bool Record(ulong spyId, bool hadEffect)
        {
            if (!hadEffect || GoalReached || !_spies.Contains(spyId) || _caught.Contains(spyId))
                return false;
            Successes++;
            if (GoalReached) GoalWon = true;
            return true;
        }
    }
}
