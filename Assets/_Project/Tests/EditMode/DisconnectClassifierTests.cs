using BooTrayal.Core.Net;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class DisconnectClassifierTests
    {
        const string TransportTimeout = "[Disconnect Event][Client-1][TransportClientId-4294967296][ProtocolTimeout] Connection closed due to timed out.";

        [Test]
        public void Connected_TransportText_HostLeft()
        {
            Assert.AreEqual("host_left", DisconnectClassifier.Classify(true, TransportTimeout));
        }

        [Test]
        public void Connected_Empty_HostLeft()
        {
            Assert.AreEqual("host_left", DisconnectClassifier.Classify(true, ""));
            Assert.AreEqual("host_left", DisconnectClassifier.Classify(true, null));
        }

        [Test]
        public void Joining_RejectionKey_Kept()
        {
            Assert.AreEqual("version_mismatch", DisconnectClassifier.Classify(false, "version_mismatch"));
            Assert.AreEqual("room_full", DisconnectClassifier.Classify(false, "room_full"));
        }

        [Test]
        public void Joining_TransportText_ConnectFailed()
        {
            Assert.AreEqual("connect_failed", DisconnectClassifier.Classify(false, TransportTimeout));
            Assert.AreEqual("connect_failed", DisconnectClassifier.Classify(false, ""));
        }

        [Test]
        public void IsRejection_KnownKeys()
        {
            Assert.IsTrue(DisconnectClassifier.IsRejection("game_in_progress"));
            Assert.IsFalse(DisconnectClassifier.IsRejection("host_left"));
        }
    }
}
