using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class SabotageTrackerTests
    {
        static SabotageTracker OneSpy()
        {
            var t = new SabotageTracker(1);
            t.Register(10);
            return t;
        }

        [Test]
        public void Required_OneSpy_3()
        {
            Assert.AreEqual(3, new SabotageTracker(1).Required);
        }

        [Test]
        public void Required_TwoSpies_4()
        {
            Assert.AreEqual(4, new SabotageTracker(2).Required);
        }

        [Test]
        public void NoEffect_NotCounted()
        {
            var t = OneSpy();
            Assert.IsFalse(t.Record(10, false));
            Assert.AreEqual(0, t.Successes);
        }

        [Test]
        public void CaughtSpy_NotCounted()
        {
            var t = OneSpy();
            t.MarkCaught(10);
            Assert.IsFalse(t.Record(10, true));
            Assert.AreEqual(0, t.Successes);
        }

        [Test]
        public void ThreeSuccesses_GoalReached()
        {
            var t = OneSpy();
            Assert.IsTrue(t.Record(10, true));
            Assert.IsTrue(t.Record(10, true));
            Assert.IsFalse(t.GoalReached);
            Assert.IsTrue(t.Record(10, true));
            Assert.IsTrue(t.GoalReached);
            Assert.IsFalse(t.Record(10, true));
            Assert.AreEqual(3, t.Successes);
        }

        [Test]
        public void TwoSpiesShareCounter()
        {
            var t = new SabotageTracker(2);
            t.Register(10);
            t.Register(11);
            t.Record(10, true);
            t.Record(10, true);
            t.Record(11, true);
            Assert.IsFalse(t.GoalReached);
            t.Record(11, true);
            Assert.IsTrue(t.GoalReached);
        }

        [Test]
        public void UnregisteredId_NotCounted()
        {
            var t = OneSpy();
            Assert.IsFalse(t.Record(99, true));
            Assert.AreEqual(0, t.Successes);
        }

        [Test]
        public void AnyUncaught_FalseWhenAllCaught()
        {
            var t = new SabotageTracker(2);
            t.Register(10);
            t.Register(11);
            Assert.IsTrue(t.AnyUncaught);
            t.MarkCaught(10);
            Assert.IsTrue(t.AnyUncaught);
            t.MarkCaught(11);
            Assert.IsFalse(t.AnyUncaught);
        }
    }
}
