using BooTrayal.Core;
using BooTrayal.Core.Puzzles;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class FuseBoxPuzzleTests
    {
        [Test]
        public void Fuse_BothWithinWindow_Solves()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 10.0);
            p.Pull(1, 10.8);
            Assert.IsTrue(p.Solved);
            Assert.IsTrue(p.RequiresTwoPlayers);
        }

        [Test]
        public void Fuse_OutsideWindow_NotSolved()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 10.0);
            p.Pull(1, 11.5);
            Assert.IsFalse(p.Solved);
        }

        [Test]
        public void Fuse_OnePulled_HalfProgress()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 10.0);
            Assert.AreEqual(0.5f, p.Progress01, 1e-5f);
        }

        [Test]
        public void Fuse_PropOneLever_SoloPlayerSolves()
        {
            var p = new FuseBoxPuzzle();
            p.Prop(0);
            p.Pull(1, 500.0);
            Assert.IsTrue(p.Solved);
        }

        [Test]
        public void Fuse_Sabotage_ClearsProps()
        {
            var p = new FuseBoxPuzzle();
            p.Prop(0);
            Assert.IsTrue(p.TrySabotage(new SeededRandom(1)));
            Assert.AreEqual(0f, p.Progress01);
            p.Pull(1, 600.0);
            Assert.IsFalse(p.Solved);
        }

        [Test]
        public void Fuse_Sabotage_NoProgress_False()
        {
            Assert.IsFalse(new FuseBoxPuzzle().TrySabotage(new SeededRandom(1)));
        }

        [Test]
        public void Fuse_Sabotage_Solved_False()
        {
            var p = new FuseBoxPuzzle();
            p.Pull(0, 1.0);
            p.Pull(1, 1.2);
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1)));
            Assert.IsTrue(p.Solved);
        }
    }

    public class RitualOfferingPuzzleTests
    {
        static RitualOfferingPuzzle Make() => new RitualOfferingPuzzle(new[] { "urn", "bell", "candle" });

        [Test]
        public void Ritual_DeliverAll_Solves()
        {
            var p = Make();
            Assert.IsTrue(p.Deliver("urn"));
            Assert.IsTrue(p.Deliver("bell"));
            Assert.AreEqual(2f / 3f, p.Progress01, 1e-5f);
            Assert.IsTrue(p.Deliver("candle"));
            Assert.IsTrue(p.Solved);
        }

        [Test]
        public void Ritual_DuplicateOrUnknown_Rejected()
        {
            var p = Make();
            p.Deliver("urn");
            Assert.IsFalse(p.Deliver("urn"));
            Assert.IsFalse(p.Deliver("pizza"));
            Assert.AreEqual(1, p.Delivered.Count);
        }

        [Test]
        public void Ritual_Sabotage_RemovesOne_ReturnsTrue()
        {
            var p = Make();
            p.Deliver("urn");
            p.Deliver("bell");
            Assert.IsTrue(p.TrySabotage(new SeededRandom(2)));
            Assert.AreEqual(1, p.Delivered.Count);
            Assert.IsNotNull(p.LastSabotagedItem);
            CollectionAssert.DoesNotContain(p.Delivered, p.LastSabotagedItem);
        }

        [Test]
        public void Ritual_Sabotage_Empty_False()
        {
            Assert.IsFalse(Make().TrySabotage(new SeededRandom(1)));
        }

        [Test]
        public void Ritual_Sabotage_Solved_False()
        {
            var p = Make();
            p.Deliver("urn"); p.Deliver("bell"); p.Deliver("candle");
            Assert.IsFalse(p.TrySabotage(new SeededRandom(1)));
            Assert.IsTrue(p.Solved);
        }
    }
}
