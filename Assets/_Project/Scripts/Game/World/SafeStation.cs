using BooTrayal.Core;
using BooTrayal.Core.Puzzles;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.World
{
    /// <summary>
    /// Puzzle #1 (GDD 8): a safe with a keypad (parts 0–9 digits, 10 clear) and a clue note in another room
    /// showing the code. Sabotage swaps two digits, so the note changes too.
    /// </summary>
    public class SafeStation : SealStation
    {
        public const int ClearPart = 10;

        public string Letter = "A";
        public TextMesh Clue;
        public TextMesh Display;

        /// <summary>The 4-digit code packed as an int (e.g. 4821), readable by everyone (it is on the note).</summary>
        public readonly NetworkVariable<int> Code =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> EnteredCount =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        SafeCodePuzzle _puzzle;

        public override bool TwoPerson => false;
        public override string Kind => "safe";
        protected override IPuzzle Puzzle => _puzzle;

        public int[] Digits => Unpack(Code.Value);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Code.OnValueChanged += (_, __) => RefreshText();
            EnteredCount.OnValueChanged += (_, __) => RefreshText();
            Active.OnValueChanged += (_, __) => RefreshText();
            RefreshText();
        }

        protected override void OnReset(IRandom rng)
        {
            var code = new int[SafeCodePuzzle.Length];
            for (int i = 0; i < code.Length; i++) code[i] = rng.Next(0, 10);
            _puzzle = new SafeCodePuzzle(code);
            Code.Value = Pack(code);
            EnteredCount.Value = 0;
        }

        protected override void Interact(ulong clientId, int part)
        {
            if (part == ClearPart) _puzzle.Clear();
            else if (part >= 0 && part <= 9 && _puzzle.EnterDigit(part, clientId)) ContributorSet.Add(clientId);
            EnteredCount.Value = _puzzle.Entered.Count;
        }

        protected override void OnSabotaged(IRandom rng)
        {
            var code = new int[SafeCodePuzzle.Length];
            for (int i = 0; i < code.Length; i++) code[i] = _puzzle.Code[i];
            Code.Value = Pack(code);
            EnteredCount.Value = 0;
        }

        void RefreshText()
        {
            if (Clue != null)
                // Digits and Latin only: the built-in TextMesh font has no Thai glyphs.
                Clue.text = Active.Value ? $"SAFE {Letter}\n{string.Join(" ", Digits)}" : $"SAFE {Letter}\n- - - -";
            if (Display != null)
                Display.text = Solved.Value ? "OPEN" : new string('*', EnteredCount.Value) + new string('_', SafeCodePuzzle.Length - EnteredCount.Value);
        }

        static int Pack(int[] d) => ((d[0] * 10 + d[1]) * 10 + d[2]) * 10 + d[3];

        static int[] Unpack(int v) => new[] { v / 1000 % 10, v / 100 % 10, v / 10 % 10, v % 10 };
    }
}
