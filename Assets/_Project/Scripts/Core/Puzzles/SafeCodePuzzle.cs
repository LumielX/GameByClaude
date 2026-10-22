using System;
using System.Collections.Generic;
using System.Linq;

namespace BooTrayal.Core.Puzzles
{
    /// <summary>Puzzle #1: one player reads the clue, another enters the 4-digit code at the safe.</summary>
    public sealed class SafeCodePuzzle : IPuzzle
    {
        public const int Length = 4;

        readonly int[] _code;
        readonly List<int> _entered = new List<int>();

        public SafeCodePuzzle(int[] code)
        {
            if (code == null || code.Length != Length || code.Any(d => d < 0 || d > 9))
                throw new ArgumentException("Code must be 4 digits 0-9", nameof(code));
            _code = (int[])code.Clone();
        }

        public string Kind => "safe_code";
        public bool RequiresTwoPlayers => true;
        public bool Solved { get; private set; }
        public IReadOnlyList<int> Code => _code;
        public IReadOnlyList<int> Entered => _entered;

        public float Progress01
        {
            get
            {
                if (Solved) return 1f;
                int prefix = 0;
                while (prefix < _entered.Count && _entered[prefix] == _code[prefix]) prefix++;
                return prefix / (float)Length;
            }
        }

        public bool EnterDigit(int digit)
        {
            if (Solved || digit < 0 || digit > 9) return false;
            _entered.Add(digit);
            if (_entered.Count == Length)
            {
                if (_entered.SequenceEqual(_code)) Solved = true;
                else _entered.Clear();
            }
            return true;
        }

        public void Clear() => _entered.Clear();

        /// <summary>Clears the entry and swaps two differing digits so the clue now shows a new code.</summary>
        public bool TrySabotage(IRandom rng)
        {
            if (Solved || Progress01 <= 0f) return false;
            _entered.Clear();
            var pairs = new List<(int, int)>();
            for (int i = 0; i < Length; i++)
                for (int j = i + 1; j < Length; j++)
                    if (_code[i] != _code[j]) pairs.Add((i, j));
            if (pairs.Count > 0)
            {
                var (a, b) = pairs[rng.Next(0, pairs.Count)];
                (_code[a], _code[b]) = (_code[b], _code[a]);
            }
            return true;
        }
    }
}
