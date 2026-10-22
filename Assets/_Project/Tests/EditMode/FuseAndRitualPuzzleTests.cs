using BooTrayal.Core;
using BooTrayal.Core.Puzzles;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class FuseBoxPuzzleTests
    {
        const ulong Crew = 100, Spy = 999;

        [Test]
        public void Fuse_BothWithinWindow_Solves()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 10.0, Crew);
            p.Pull(1, 10.8, Crew);
            Assert.IsTrue(p.Solved);
            Assert.IsTrue(p.RequiresTwoPlayers);
        }

        [Test]
        public void Fuse_OutsideWindow_NotSolved()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 10.0, Crew);
            p.Pull(1, 11.5, Crew);
            Assert.IsFalse(p.Solved);
        }

        [Test]
        public void Fuse_OnePulled_HalfProgress()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 10.0, Crew);
            Assert.AreEqual(0.5f, p.Progress01, 1e-5f);
        }

        [Test]
        public void Fuse_PropOneLever_SoloPlayerSolves()
        {
            var p = new FuseBoxPuzzle();
            p.Prop(0, Crew);
            p.Pull(1, 500.0, Crew);
            Assert.IsTrue(p.Solved);
        }

        [Test]
        public void Fuse_Sabotage_ClearsProps()
        {
            var p = new FuseBoxPuzzle();
            p.Prop(0, Crew);
            Assert.IsTrue(p.TrySabotage(new SeededRandom(1), Spy));
            Assert.AreEqual(0f, p.Progress01);
            p.Pull(1, 600.0, Crew);
            Assert.IsFalse(p.Solved);
        }

        [Test]
        public void Fuse_Sabotage_OnlySaboteurPulled_NotCounted()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 10.0, Spy);
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1), Spy));
        }

        [Test]
        public void Fuse_Sabotage_CrewPropped_Counted()
        {
            var p = new FuseBoxPuzzle();
            p.Prop(0, Crew);
            Assert.IsTrue(p.TrySabotage(new SeededRandom(1), Spy));
            Assert.AreEqual(0f, p.Progress01);
        }

        [Test]
        public void Fuse_Sabotage_NoProgress_False()
        {
            Assert.IsFalse(new FuseBoxPuzzle().TrySabotage(new SeededRandom(1), Spy));
        }

        [Test]
        public void Fuse_Sabotage_Solved_False()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 1.0, Crew);
            p.Pull(1, 1.2, Crew);
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1), Spy));
            Assert.IsTrue(p.Solved);
        }
    }

    public class RitualOfferingPuzzleTests
    {
        const ulong Crew = 100, Spy = 999;

        static RitualOfferingPuzzle Make() => new RitualOfferingPuzzle(new[] { "urn", "bell", "candle" });

        [Test]
        public void Ritual_DeliverAll_Solves()
        {
            var p = Make();
            Assert.IsTrue(p.Deliver("urn", Crew));
            Assert.IsTrue(p.Deliver("bell", Crew));
            Assert.AreEqual(2f / 3f, p.Progress01, 1e-5f);
            Assert.IsTrue(p.Deliver("candle", Crew));
            Assert.IsTrue(p.Solved);
        }

        [Test]
        public void Ritual_DuplicateOrUnknown_Rejected()
        {
            var p = Make();
            p.Deliver("urn", Crew);
            Assert.IsFalse(p.Deliver("urn", Crew));
            Assert.IsFalse(p.Deliver("pizza", Crew));
            Assert.AreEqual(1, p.Delivered.Count);
        }

        [Test]
        public void Ritual_Sabotage_RemovesOne_ReturnsTrue()
        {
            var p = Make();
            p.Deliver("urn", Crew);
            p.Deliver("bell", Crew);
            Assert.IsTrue(p.TrySabotage(new SeededRandom(2), Spy));
            Assert.AreEqual(1, p.Delivered.Count);
            Assert.IsNotNull(p.LastSabotagedItem);
            CollectionAssert.DoesNotContain(p.Delivered, p.LastSabotagedItem);
        }

        [Test]
        public void Ritual_Sabotage_OnlySaboteurDelivered_NotCounted()
        {
            var p = Make();
            p.Deliver("urn", Spy);
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1), Spy));
            Assert.AreEqual(1, p.Delivered.Count);
        }

        [Test]
        public void Ritual_Sabotage_RemovesCrewItem_NotSaboteurs()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var q = Make();
                q.Deliver("urn", Spy);
                q.Deliver("bell", Crew);
                Assert.IsTrue(q.TrySabotage(new SeededRandom(seed), Spy));
                Assert.AreEqual("bell", q.LastSabotagedItem);
            }
        }

        [Test]
        public void Ritual_Sabotage_Empty_False()
        {
            Assert.IsFalse(Make().TrySabotage(new SeededRandom(1), Spy));
        }

        [Test]
        public void Ritual_Sabotage_Solved_False()
        {
            var p = Make();
            p.Deliver("urn", Crew); p.Deliver("bell", Crew); p.Deliver("candle", Crew);
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1), Spy));
            Assert.IsTrue(p.Solved);
        }
    }
}
