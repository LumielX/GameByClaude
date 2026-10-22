using System;
using System.Collections.Generic;
using System.Linq;

namespace BooTrayal.Core.Puzzles
{
    /// <summary>Puzzle #3: carry the (heavy) ritual items to the altar.</summary>
    public sealed class RitualOfferingPuzzle : IPuzzle
    {
        readonly HashSet<string> _required;
        readonly List<string> _delivered = new List<string>();

        public RitualOfferingPuzzle(IReadOnlyList<string> requiredItemIds)
        {
            if (requiredItemIds == null || requiredItemIds.Count == 0)
                throw new ArgumentException("At least one ritual item is required", nameof(requiredItemIds));
            _required = new HashSet<string>(requiredItemIds);
        }

        public string Kind => "ritual_offering";
        public bool RequiresTwoPlayers => true;
        public bool Solved => _delivered.Count == _required.Count;
        public float Progress01 => _delivered.Count / (float)_required.Count;
        public IReadOnlyCollection<string> Delivered => _delivered;
        /// <summary>Item knocked off the altar by the last sabotage; the game returns it to its spawn.</summary>
        public string LastSabotagedItem { get; private set; }

        public bool Deliver(string itemId)
        {
            if (Solved || itemId == null || !_required.Contains(itemId) || _delivered.Contains(itemId)) return false;
            _delivered.Add(itemId);
            return true;
        }

        public bool TrySabotage(IRandom rng)
        {
            if (Solved || _delivered.Count == 0) return false;
            int index = rng.Next(0, _delivered.Count);
            LastSabotagedItem = _delivered[index];
            _delivered.RemoveAt(index);
            return true;
        }
    }
}
