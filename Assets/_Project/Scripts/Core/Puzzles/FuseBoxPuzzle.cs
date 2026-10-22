using System;

namespace BooTrayal.Core.Puzzles
{
    /// <summary>Puzzle #2: two levers in two rooms pulled within the sync window. A lever propped by an item counts as always pulled.</summary>
    public sealed class FuseBoxPuzzle : IPuzzle
    {
        readonly double _syncWindow;
        readonly double?[] _pulledAt = new double?[2];
        readonly bool[] _propped = new bool[2];

        public FuseBoxPuzzle(double syncWindow = 1.0)
        {
            _syncWindow = syncWindow;
        }

        public string Kind => "fuse_box";
        public bool RequiresTwoPlayers => true;
        public bool Solved { get; private set; }

        public float Progress01
        {
            get
            {
                if (Solved) return 1f;
                return HasAnyInput ? 0.5f : 0f;
            }
        }

        bool HasAnyInput => _propped[0] || _propped[1] || _pulledAt[0].HasValue || _pulledAt[1].HasValue;

        public void Pull(int lever, double time)
        {
            CheckLever(lever);
            if (Solved) return;
            _pulledAt[lever] = time;
            int other = 1 - lever;
            if (_propped[other] || (_pulledAt[other].HasValue && Math.Abs(time - _pulledAt[other].Value) <= _syncWindow))
                Solved = true;
        }

        public void Prop(int lever)
        {
            CheckLever(lever);
            if (!Solved) _propped[lever] = true;
        }

        public void Unprop(int lever)
        {
            CheckLever(lever);
            _propped[lever] = false;
        }

        public bool TrySabotage(IRandom rng)
        {
            if (Solved || !HasAnyInput) return false;
            _propped[0] = _propped[1] = false;
            _pulledAt[0] = _pulledAt[1] = null;
            return true;
        }

        static void CheckLever(int lever)
        {
            if (lever != 0 && lever != 1) throw new ArgumentOutOfRangeException(nameof(lever));
        }
    }
}
