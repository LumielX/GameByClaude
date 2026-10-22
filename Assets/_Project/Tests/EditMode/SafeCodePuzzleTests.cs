using System;
using System.Linq;
using BooTrayal.Core;
using BooTrayal.Core.Puzzles;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class SafeCodePuzzleTests
    {
        const ulong Crew = 100, Spy = 999;

        static SafeCodePuzzle Make() => new SafeCodePuzzle(new[] { 1, 2, 3, 4 });

        static void Enter(SafeCodePuzzle p, params int[] digits)
        {
            foreach (var d in digits) p.EnterDigit(d, Crew);
        }

        [Test]
        public void CorrectCode_Solves()
        {
            var p = Make();
            Enter(p, 1, 2, 3, 4);
            Assert.IsTrue(p.Solved);
            Assert.AreEqual(1f, p.Progress01);
            Assert.IsTrue(p.RequiresTwoPlayers);
        }

        [Test]
        public void WrongCode_ClearsEntry()
        {
            var p = Make();
            Enter(p, 1, 2, 3, 9);
            Assert.IsFalse(p.Solved);
            CollectionAssert.IsEmpty(p.Entered);
        }

        [Test]
        public void Progress_CountsCorrectPrefix()
        {
            var p = Make();
            Enter(p, 1, 2);
            Assert.AreEqual(0.5f, p.Progress01, 1e-5f);
            p.Clear();
            Enter(p, 9, 2);
            Assert.AreEqual(0f, p.Progress01);
        }

        [Test]
        public void Sabotage_NoProgress_ReturnsFalse_CodeUnchanged()
        {
            var p = Make();
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1), Spy));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, p.Code);
        }

        [Test]
        public void Sabotage_WithProgress_ClearsAndChangesCode()
        {
            var p = Make();
            Enter(p, 1, 2);
            Assert.IsTrue(p.TrySabotage(new SeededRandom(1), Spy));
            CollectionAssert.IsEmpty(p.Entered);
            CollectionAssert.AreNotEqual(new[] { 1, 2, 3, 4 }, p.Code);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, p.Code);
        }

        [Test]
        public void Sabotage_RepeatedDigits_StillChangesCode()
        {
            var p = new SafeCodePuzzle(new[] { 7, 7, 7, 1 });
            p.EnterDigit(7, Crew);
            Assert.IsTrue(p.TrySabotage(new SeededRandom(3), Spy));
            CollectionAssert.AreNotEqual(new[] { 7, 7, 7, 1 }, p.Code);
        }

        [Test]
        public void Sabotage_Solved_ReturnsFalse_StaysSolved()
        {
            var p = Make();
            Enter(p, 1, 2, 3, 4);
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1), Spy));
            Assert.IsTrue(p.Solved);
        }

        [Test]
        public void Sabotage_OnlySaboteurProgress_NotCounted()
        {
            var p = Make();
            p.EnterDigit(1, Spy);
            p.EnterDigit(2, Spy);
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1), Spy));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, p.Code);
        }

        [Test]
        public void Contributors_TracksEnteredBy()
        {
            var p = Make();
            p.EnterDigit(1, Crew);
            p.EnterDigit(2, Spy);
            CollectionAssert.AreEquivalent(new[] { Crew, Spy }, p.Contributors);
        }

        [Test]
        public void InvalidDigit_Rejected()
        {
            var p = Make();
            Assert.IsFalse(p.EnterDigit(10, Crew));
            Assert.IsFalse(p.EnterDigit(-1, Crew));
            CollectionAssert.IsEmpty(p.Entered);
        }

        [Test]
        public void Ctor_BadLength_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SafeCodePuzzle(new[] { 1, 2, 3 }));
            Assert.Throws<ArgumentException>(() => new SafeCodePuzzle(new[] { 1, 2, 3, 12 }));
        }
    }
}
