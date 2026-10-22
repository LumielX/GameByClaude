using System.Collections.Generic;
using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class NightJudgeTests
    {
        const ulong Spy = 1;

        static NightPlayer P(ulong id, Role role, LifeState state) =>
            new NightPlayer { Id = id, Role = role, State = state };

        static SabotageTracker Tracker()
        {
            var t = new SabotageTracker(1);
            t.Register(Spy);
            return t;
        }

        static List<NightPlayer> Crew(params LifeState[] states)
        {
            var list = new List<NightPlayer> { P(Spy, Role.Spy, LifeState.Alive) };
            for (int i = 0; i < states.Length; i++) list.Add(P((ulong)(i + 2), Role.Crew, states[i]));
            return list;
        }

        [Test]
        public void CrewEscaped_RitualDone_CrewWins()
        {
            var v = NightJudge.Evaluate(Crew(LifeState.Escaped, LifeState.MiniGhost, LifeState.Escaped), true, 120, Tracker());
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Crew, v.Winner);
            Assert.AreEqual("escaped", v.Reason);
        }

        [Test]
        public void AllCrewDead_SpiesWin_crew_wiped()
        {
            var v = NightJudge.Evaluate(Crew(LifeState.MiniGhost, LifeState.MiniGhost), false, 120, Tracker());
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
            Assert.AreEqual("crew_wiped", v.Reason);
        }

        [Test]
        public void LastCrewDisconnects_SpiesWin()
        {
            var v = NightJudge.Evaluate(Crew(LifeState.MiniGhost, LifeState.Disconnected), false, 120, Tracker());
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
        }

        [Test]
        public void TimeUp_NoRitual_SpiesWin()
        {
            var v = NightJudge.Evaluate(Crew(LifeState.Alive, LifeState.Alive), false, 0, Tracker());
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
            Assert.AreEqual("time_up", v.Reason);
        }

        [Test]
        public void TimeUp_RitualDoneSomeEscaped_CrewWins()
        {
            var v = NightJudge.Evaluate(Crew(LifeState.Escaped, LifeState.Alive), true, 0, Tracker());
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Crew, v.Winner);
        }

        [Test]
        public void SabotageGoal_SpiesWinButNightContinues()
        {
            var t = Tracker();
            t.Record(Spy, true); t.Record(Spy, true); t.Record(Spy, true);
            var v = NightJudge.Evaluate(Crew(LifeState.Alive, LifeState.Alive), false, 300, t);
            Assert.IsFalse(v.Ended);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
            Assert.AreEqual("sabotage_goal", v.Reason);
        }

        [Test]
        public void SabotageGoal_ThenCrewEscapes_SpiesStillWin()
        {
            var t = Tracker();
            t.Record(Spy, true); t.Record(Spy, true); t.Record(Spy, true);
            var v = NightJudge.Evaluate(Crew(LifeState.Escaped, LifeState.Escaped), true, 100, t);
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
        }

        [Test]
        public void SpyDisconnected_TimeUp_SpiesDoNotWin()
        {
            var t = Tracker();
            t.MarkCaught(Spy);
            var players = Crew(LifeState.Alive, LifeState.Alive);
            players[0].State = LifeState.Disconnected;
            players[0].Exposed = true;
            var v = NightJudge.Evaluate(players, false, 0, t);
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.None, v.Winner);
        }

        [Test]
        public void SpyCaught_CrewWiped_SpiesDoNotWin()
        {
            var t = Tracker();
            t.MarkCaught(Spy);
            var v = NightJudge.Evaluate(Crew(LifeState.MiniGhost, LifeState.MiniGhost), false, 100, t);
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.None, v.Winner);
        }

        [Test]
        public void SabotageGoal_ThenSpyCaught_SpiesStillWin()
        {
            var t = Tracker();
            t.Record(Spy, true); t.Record(Spy, true); t.Record(Spy, true);
            t.MarkCaught(Spy);
            var v = NightJudge.Evaluate(Crew(LifeState.Alive, LifeState.Alive), false, 300, t);
            Assert.IsFalse(v.Ended);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
        }

        [Test]
        public void CoOp_TimeUp_WinnerNone()
        {
            var players = new List<NightPlayer> { P(2, Role.Crew, LifeState.Alive), P(3, Role.Crew, LifeState.Alive) };
            var v = NightJudge.Evaluate(players, false, 0, new SabotageTracker(0));
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.None, v.Winner);
        }

        [Test]
        public void CrewStillInside_AfterSomeEscaped_NotEnded()
        {
            var v = NightJudge.Evaluate(Crew(LifeState.Escaped, LifeState.Alive), true, 100, Tracker());
            Assert.IsFalse(v.Ended);
            Assert.AreEqual(NightWinner.None, v.Winner);
        }
    }
}
