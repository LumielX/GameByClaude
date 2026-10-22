using BooTrayal.Core.Net;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class ConnectionTrackerTests
    {
        [Test]
        public void Begin_ResetsRejectionAndFailed()
        {
            var t = new ConnectionTracker();
            t.Begin();
            t.RecordRejection("game_in_progress");
            Assert.IsTrue(t.TryMarkFailed());
            t.Begin();
            Assert.IsNull(t.RejectionKey);
            Assert.IsTrue(t.TryMarkFailed());
        }

        [Test]
        public void Cancel_InvalidatesToken()
        {
            var t = new ConnectionTracker();
            int token = t.Begin();
            Assert.IsTrue(t.IsCurrent(token));
            t.Cancel();
            Assert.IsFalse(t.IsCurrent(token));
        }

        [Test]
        public void TryMarkFailed_OnlyOnce()
        {
            var t = new ConnectionTracker();
            t.Begin();
            Assert.IsTrue(t.TryMarkFailed());
            Assert.IsFalse(t.TryMarkFailed());
        }

        [Test]
        public void RecordRejection_IgnoresTransportText()
        {
            var t = new ConnectionTracker();
            t.Begin();
            t.RecordRejection("[Disconnect Event] timed out");
            Assert.IsNull(t.RejectionKey);
            t.RecordRejection("version_mismatch");
            Assert.AreEqual("version_mismatch", t.RejectionKey);
        }

        [Test]
        public void ClientDisconnected_UnknownId_False()
        {
            var t = new ConnectionTracker();
            t.ClientConnected(0);
            Assert.IsFalse(t.ClientDisconnected(5));
            Assert.AreEqual(1, t.ClientCount);
        }

        [Test]
        public void ClientCount_TracksConnected()
        {
            var t = new ConnectionTracker();
            t.ClientConnected(0);
            t.ClientConnected(1);
            t.ClientConnected(1);
            Assert.AreEqual(2, t.ClientCount);
            Assert.IsTrue(t.ClientDisconnected(1));
            Assert.AreEqual(1, t.ClientCount);
            t.ResetClients();
            Assert.AreEqual(0, t.ClientCount);
        }
    }
}
