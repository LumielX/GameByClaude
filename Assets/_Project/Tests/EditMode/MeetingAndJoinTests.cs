using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class MeetingAndJoinTests
    {
        [Test]
        public void Ring_OncePerNight()
        {
            var m = new MeetingRules();
            Assert.IsTrue(m.CanRing(1, LifeState.Alive, false, false));
            m.MarkRang(1);
            Assert.IsFalse(m.CanRing(1, LifeState.Alive, false, false));
            Assert.IsTrue(m.CanRing(2, LifeState.Alive, false, false));
        }

        [Test]
        public void Ring_WhileChased_Denied()
        {
            Assert.IsFalse(new MeetingRules().CanRing(1, LifeState.Alive, true, false));
        }

        [Test]
        public void Ring_MiniGhost_Denied()
        {
            Assert.IsFalse(new MeetingRules().CanRing(1, LifeState.MiniGhost, false, false));
        }

        [Test]
        public void Ring_DuringMeeting_Denied()
        {
            Assert.IsFalse(new MeetingRules().CanRing(1, LifeState.Alive, false, true));
        }

        [Test]
        public void ResetForNight_AllowsAgain()
        {
            var m = new MeetingRules();
            m.MarkRang(1);
            m.ResetForNight();
            Assert.IsTrue(m.CanRing(1, LifeState.Alive, false, false));
        }

        [Test]
        public void Meeting_Durations()
        {
            Assert.AreEqual(45f, MeetingRules.DiscussSeconds);
            Assert.AreEqual(15f, MeetingRules.VoteSeconds);
        }

        [Test]
        public void Exorcise_Values()
        {
            Assert.AreEqual(0.8f, ExorciseEffects.Correct.SpeedMul, 1e-5f);
            Assert.AreEqual(1f, ExorciseEffects.Correct.HearingMul, 1e-5f);
            Assert.IsTrue(double.IsPositiveInfinity(ExorciseEffects.Correct.Seconds));
            Assert.AreEqual(1.2f, ExorciseEffects.Wrong.SpeedMul, 1e-5f);
            Assert.AreEqual(1.5f, ExorciseEffects.Wrong.HearingMul, 1e-5f);
            Assert.AreEqual(30.0, ExorciseEffects.Wrong.Seconds);
            Assert.AreEqual(60.0, ExorciseEffects.CorrectBonusSeconds);
        }

        [Test]
        public void Join_InNight_Denied()
        {
            Assert.IsFalse(JoinPolicy.CanJoin(SessionPhase.InNight, 3, 8, false));
            Assert.IsFalse(JoinPolicy.CanJoin(SessionPhase.InNight, 3, 8, true));
        }

        [Test]
        public void Join_Shop_Allowed()
        {
            Assert.IsTrue(JoinPolicy.CanJoin(SessionPhase.Shop, 3, 8, true));
            Assert.IsTrue(JoinPolicy.CanJoin(SessionPhase.Summary, 3, 8, false));
            Assert.IsTrue(JoinPolicy.CanJoin(SessionPhase.Lobby, 7, 8, false));
        }

        [Test]
        public void Join_Full_Denied()
        {
            Assert.IsFalse(JoinPolicy.CanJoin(SessionPhase.Lobby, 8, 8, false));
            Assert.IsFalse(JoinPolicy.CanJoin(SessionPhase.Shop, 8, 8, true));
        }

        [Test]
        public void Join_GameOver_Denied()
        {
            Assert.IsFalse(JoinPolicy.CanJoin(SessionPhase.GameOver, 2, 8, false));
        }
    }
}
