using BooTrayal.Core.Player;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class KnockdownLedgerTests
    {
        [Test]
        public void Unknown_NotDown()
        {
            Assert.IsFalse(new KnockdownLedger().IsDown(7, 100));
        }

        [Test]
        public void MarkDown_LastsKnockdownSeconds()
        {
            var l = new KnockdownLedger();
            l.MarkDown(7, 100);
            Assert.IsTrue(l.IsDown(7, 100 + PushRules.KnockdownSeconds - 0.01));
            Assert.IsFalse(l.IsDown(7, 100 + PushRules.KnockdownSeconds + 0.01));
        }

        [Test]
        public void MarkDown_DoesNotShorten()
        {
            var l = new KnockdownLedger();
            l.MarkDown(7, 100, 5);
            l.MarkDown(7, 101);
            Assert.IsTrue(l.IsDown(7, 104));
        }

        [Test]
        public void Forget_ClearsPlayer()
        {
            var l = new KnockdownLedger();
            l.MarkDown(7, 100);
            l.Forget(7);
            Assert.IsFalse(l.IsDown(7, 100.5));
        }
    }
}
