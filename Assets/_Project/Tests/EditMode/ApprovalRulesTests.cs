using BooTrayal.Core.Net;
using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class ApprovalRulesTests
    {
        static byte[] Payload(string version) => ConnectionPayload.Encode(version, "Pim");

        [Test]
        public void ValidPayload_Approved()
        {
            var d = ApprovalRules.Evaluate(Payload("0.1.0"), "0.1.0", SessionPhase.Lobby, 3, 8);
            Assert.IsTrue(d.Approved);
            Assert.AreEqual("", d.Reason);
        }

        [Test]
        public void BadPayload_Rejected()
        {
            var d = ApprovalRules.Evaluate(new byte[] { 1, 2, 3 }, "0.1.0", SessionPhase.Lobby, 3, 8);
            Assert.IsFalse(d.Approved);
            Assert.AreEqual("bad_payload", d.Reason);
        }

        [Test]
        public void OldVersion_VersionMismatch()
        {
            var d = ApprovalRules.Evaluate(Payload("0.0.9"), "0.1.0", SessionPhase.Lobby, 3, 8);
            Assert.IsFalse(d.Approved);
            Assert.AreEqual("version_mismatch", d.Reason);
        }

        [Test]
        public void Full_RoomFull()
        {
            var d = ApprovalRules.Evaluate(Payload("0.1.0"), "0.1.0", SessionPhase.Lobby, 8, 8);
            Assert.AreEqual("room_full", d.Reason);
        }

        [Test]
        public void InNight_GameInProgress()
        {
            var d = ApprovalRules.Evaluate(Payload("0.1.0"), "0.1.0", SessionPhase.InNight, 3, 8);
            Assert.AreEqual("game_in_progress", d.Reason);
        }

        [Test]
        public void VersionCheckedBeforeFull()
        {
            var d = ApprovalRules.Evaluate(Payload("0.0.9"), "0.1.0", SessionPhase.Lobby, 8, 8);
            Assert.AreEqual("version_mismatch", d.Reason);
        }
    }
}
