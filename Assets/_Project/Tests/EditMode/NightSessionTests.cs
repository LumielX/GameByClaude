using System.Collections.Generic;
using System.Linq;
using BooTrayal.Core;
using BooTrayal.Core.Economy;
using BooTrayal.Core.Fear;
using BooTrayal.Core.Night;
using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class NightSessionTests
    {
        static readonly ulong[] Five = { 0, 1, 2, 3, 4 };

        static NightSession Make(ulong[] players, int spies, double seconds = 600, int seed = 3) =>
            new NightSession(players, spies, seconds, new SeededRandom(seed));

        static ulong SpyOf(NightSession s) => s.Roles.Spies[0];
        static ulong[] CrewOf(NightSession s) => s.Players.Where(p => p.Role == Role.Crew).Select(p => p.Id).ToArray();

        static NightVerdict Run(NightSession s, float seconds, float step = 0.5f)
        {
            var v = default(NightVerdict);
            for (float t = 0f; t < seconds - 1e-4f; t += step)
            {
                v = s.Tick(step);
                if (v.Ended) break;
            }
            return v;
        }

        /// <summary>Rings and runs the meeting to its vote phase.</summary>
        static void OpenVote(NightSession s, ulong caller)
        {
            Assert.IsTrue(s.RingBell(caller, false, 1f, 2f));
            s.Tick(1f);
            Assert.AreEqual(MeetingPhase.Vote, s.ActiveMeeting.Phase);
        }

        [Test]
        public void Start_DealsSpiesBySpyCount()
        {
            var s = Make(Five, 1);
            Assert.AreEqual(1, s.Roles.Spies.Count);
            Assert.AreEqual(5, s.Players.Count);
            Assert.IsTrue(s.Players.All(p => p.State == LifeState.Alive));
            Assert.AreEqual(600.0, s.SecondsLeft);
        }

        [Test]
        public void CoOp_NoSpies_EveryoneHarmable()
        {
            var s = Make(new ulong[] { 0, 1 }, 0);
            Assert.AreEqual(0, s.Roles.Spies.Count);
            Assert.IsTrue(s.IsHarmable(0) && s.IsHarmable(1));
        }

        [Test]
        public void IsHarmable_UnexposedSpyFalse_ExposedTrue()
        {
            var s = Make(Five, 1);
            var spy = SpyOf(s);
            Assert.IsFalse(s.IsHarmable(spy));
            OpenVote(s, CrewOf(s)[0]);
            foreach (var p in Five) s.ActiveMeeting.Cast(p, spy);
            s.Tick(0.1f);
            Assert.IsFalse(s.IsHarmable(spy), "an ejected Spy is a mini ghost, not a target");
            Assert.IsTrue(s.Players.First(p => p.Id == spy).Exposed);
        }

        [Test]
        public void Kill_MiniGhost_CanRevive_Revive()
        {
            var s = Make(Five, 1);
            var crew = CrewOf(s)[0];
            Assert.IsTrue(s.Kill(crew));
            Assert.IsFalse(s.Kill(crew), "already a mini ghost");
            Assert.IsFalse(s.IsAlive(crew));
            Assert.IsTrue(s.CanRevive(crew));
            Assert.IsTrue(s.Revive(crew));
            Assert.IsTrue(s.IsAlive(crew));
            Assert.IsFalse(s.Revive(crew));
        }

        [Test]
        public void Kill_CaughtSpy_CannotRevive()
        {
            var s = Make(Five, 1);
            var spy = SpyOf(s);
            OpenVote(s, CrewOf(s)[0]);
            foreach (var p in Five) s.ActiveMeeting.Cast(p, spy);
            s.Tick(0.1f);
            Assert.IsFalse(s.CanRevive(spy));
            Assert.IsFalse(s.Revive(spy));
        }

        [Test]
        public void Escape_OnlyAfterRitual()
        {
            var s = Make(new ulong[] { 0, 1 }, 0);
            Assert.IsFalse(s.Escape(0));
            s.CompleteRitual();
            Assert.IsTrue(s.Escape(0));
            Assert.IsFalse(s.Escape(0));
            s.Kill(1);
            Assert.IsFalse(s.Escape(1), "mini ghosts cannot escape");
        }

        [Test]
        public void Escape_AllOut_CrewWins()
        {
            var s = Make(new ulong[] { 0, 1 }, 0);
            s.CompleteRitual();
            s.Escape(0);
            Assert.IsFalse(s.Tick(0.1f).Ended);
            s.Escape(1);
            var v = s.Tick(0.1f);
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Crew, v.Winner);
            Assert.AreEqual("escaped", v.Reason);
        }

        [Test]
        public void PlayerLeft_Spy_ExposedAndCaught()
        {
            var s = Make(Five, 1);
            var spy = SpyOf(s);
            s.PlayerLeft(spy);
            var p = s.Players.First(x => x.Id == spy);
            Assert.AreEqual(LifeState.Disconnected, p.State);
            Assert.IsTrue(p.Exposed);
            Assert.IsTrue(s.Sabotage.IsCaught(spy));
            var v = Run(s, 601f, 50f);
            Assert.AreEqual(NightWinner.None, v.Winner, "a Spy who left cannot win the night");
        }

        [Test]
        public void PlayerLeft_OnlyCrew_NightEndsCrewWiped()
        {
            var s = Make(new ulong[] { 0, 1 }, 0);
            s.PlayerLeft(0);
            s.Kill(1);
            var v = s.Tick(0.1f);
            Assert.IsTrue(v.Ended);
            Assert.AreEqual("crew_wiped", v.Reason);
        }

        [Test]
        public void SealAndItem_AwardsContributors()
        {
            var s = Make(new ulong[] { 0, 1 }, 0);
            s.SealClosed(new ulong[] { 0, 1 });
            s.SealClosed(new ulong[] { 0, 0 });
            s.ItemDelivered(new ulong[] { 1 });
            var v = s.Tick(0f);
            var awards = s.FinishAwards(new NightVerdict(true, NightWinner.None, "time_up"));
            Assert.AreEqual(2 * ScoreRules.SealClosed, awards[0], "duplicate contributor ids pay once per seal");
            Assert.AreEqual(ScoreRules.SealClosed + ScoreRules.RitualItemDelivered, awards[1]);
            Assert.AreEqual(2, s.SealsClosed);
        }

        [Test]
        public void RingBell_OncePerNight_NotWhileChased_NotMiniGhost()
        {
            var s = Make(Five, 1);
            Assert.IsFalse(s.RingBell(0, true, 45f, 15f), "not while chased");
            s.Kill(1);
            Assert.IsFalse(s.RingBell(1, false, 45f, 15f), "mini ghosts cannot ring");
            Assert.IsTrue(s.RingBell(0, false, 45f, 15f));
            Assert.IsFalse(s.RingBell(2, false, 45f, 15f), "a meeting is already running");
            Assert.IsFalse(s.ActiveMeeting.Voters.Contains(1ul), "mini ghosts do not vote");
            Run(s, 61f);
            Assert.IsNull(s.ActiveMeeting);
            Assert.IsFalse(s.RingBell(0, false, 45f, 15f), "once per night");
            Assert.IsTrue(s.RingBell(2, false, 45f, 15f));
        }

        [Test]
        public void Meeting_CorrectVote_SpyMiniGhost_Plus60s_GhostSlow()
        {
            var s = Make(Five, 1);
            var spy = SpyOf(s);
            var crew = CrewOf(s);
            OpenVote(s, crew[0]);
            double before = s.SecondsLeft;
            foreach (var c in crew) s.ActiveMeeting.Cast(c, spy);
            s.ActiveMeeting.Cast(spy, null);
            s.Tick(0.5f);
            var outcome = s.TakeOutcome();
            Assert.IsTrue(outcome.HasValue);
            Assert.IsTrue(outcome.Value.Ejected);
            Assert.AreEqual(spy, outcome.Value.Target);
            Assert.IsTrue(outcome.Value.WasSpy);
            CollectionAssert.AreEquivalent(crew, outcome.Value.CorrectVoters);
            Assert.IsNull(s.TakeOutcome(), "an outcome is taken once");
            Assert.AreEqual(LifeState.MiniGhost, s.Players.First(p => p.Id == spy).State);
            Assert.AreEqual(before - 0.5 + ExorciseEffects.CorrectBonusSeconds, s.SecondsLeft, 1e-6);
            Assert.AreEqual(0.8f, s.GhostSpeedMultiplier, 1e-5f);
            Run(s, 100f);
            Assert.AreEqual(0.8f, s.GhostSpeedMultiplier, 1e-5f, "for the rest of the night");
        }

        [Test]
        public void Meeting_WrongVote_Rage30s_ThenNormal()
        {
            var s = Make(Five, 1);
            var crew = CrewOf(s);
            OpenVote(s, crew[0]);
            foreach (var p in Five) s.ActiveMeeting.Cast(p, crew[1]);
            s.Tick(0.5f);
            var outcome = s.TakeOutcome().Value;
            Assert.IsTrue(outcome.Ejected);
            Assert.IsFalse(outcome.WasSpy);
            Assert.AreEqual(0, outcome.CorrectVoters.Count);
            Assert.AreEqual(LifeState.MiniGhost, s.Players.First(p => p.Id == crew[1]).State);
            Assert.IsTrue(s.CanRevive(crew[1]));
            Assert.AreEqual(1.2f, s.GhostSpeedMultiplier, 1e-5f);
            Assert.AreEqual(1.5f, s.GhostHearingMultiplier, 1e-5f);
            Run(s, 29.5f);
            Assert.AreEqual(1.2f, s.GhostSpeedMultiplier, 1e-5f);
            Run(s, 0.5f);
            Assert.AreEqual(1f, s.GhostSpeedMultiplier, 1e-5f);
            Assert.AreEqual(1f, s.GhostHearingMultiplier, 1e-5f);
        }

        [Test]
        public void Meeting_Skip_NoEjection()
        {
            var s = Make(Five, 1);
            OpenVote(s, 0);
            foreach (var p in Five) s.ActiveMeeting.Cast(p, null);
            s.Tick(0.5f);
            var outcome = s.TakeOutcome().Value;
            Assert.IsFalse(outcome.Ejected);
            Assert.IsTrue(s.Players.All(p => p.State == LifeState.Alive));
            Assert.AreEqual(1f, s.GhostSpeedMultiplier);
        }

        [Test]
        public void Power_OnlyUnexposedAliveSpy_Cooldowns_60_75()
        {
            var s = Make(Five, 1);
            var spy = SpyOf(s);
            Assert.IsFalse(s.TryUsePower(CrewOf(s)[0], SpyPower.SabotagePuzzle, out var reason));
            Assert.AreEqual("not_spy", reason);
            Assert.IsTrue(s.TryUsePower(spy, SpyPower.SabotagePuzzle, out _));
            Assert.IsFalse(s.TryUsePower(spy, SpyPower.SabotagePuzzle, out reason));
            Assert.AreEqual("cooldown", reason);
            Assert.IsTrue(s.TryUsePower(spy, SpyPower.Blackout, out _), "each power has its own cooldown");
            Run(s, 59.5f);
            Assert.IsFalse(s.TryUsePower(spy, SpyPower.SabotagePuzzle, out _));
            Run(s, 0.5f);
            Assert.IsTrue(s.TryUsePower(spy, SpyPower.SabotagePuzzle, out _));
            Run(s, 14.5f);
            Assert.IsFalse(s.TryUsePower(spy, SpyPower.Blackout, out _));
            Run(s, 0.5f);
            Assert.IsTrue(s.TryUsePower(spy, SpyPower.Blackout, out _));

            s.Kill(spy);
            Assert.IsFalse(s.TryUsePower(spy, SpyPower.Blackout, out reason));
            Assert.AreEqual("not_alive", reason);
        }

        [Test]
        public void Power_DuringMeeting_Denied()
        {
            var s = Make(Five, 1);
            var spy = SpyOf(s);
            s.RingBell(CrewOf(s)[0], false, 45f, 15f);
            Assert.IsFalse(s.TryUsePower(spy, SpyPower.SabotagePuzzle, out var reason));
            Assert.AreEqual("meeting", reason);
        }

        [Test]
        public void RecordSabotage_GoalReached_SpiesWin()
        {
            var s = Make(Five, 1);
            var spy = SpyOf(s);
            Assert.IsFalse(s.RecordSabotage(spy, false));
            for (int i = 0; i < 3; i++) Assert.IsTrue(s.RecordSabotage(spy, true));
            Assert.IsTrue(s.Sabotage.GoalWon);
            var v = Run(s, 601f, 50f);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
            Assert.AreEqual("sabotage_goal", v.Reason);
        }

        [Test]
        public void TimeUp_UncaughtSpy_SpiesWin()
        {
            var s = Make(Five, 1, seconds: 10);
            var v = Run(s, 10f);
            Assert.IsTrue(v.Ended);
            Assert.AreEqual(NightWinner.Spies, v.Winner);
            Assert.AreEqual("time_up", v.Reason);
            var awards = s.FinishAwards(v);
            Assert.AreEqual(ScoreRules.SpyWin, awards[SpyOf(s)]);
        }

        [Test]
        public void TimeUpDuringMeeting_Ends()
        {
            var s = Make(Five, 1, seconds: 5);
            s.RingBell(0, false, 45f, 15f);
            var v = Run(s, 5f);
            Assert.IsTrue(v.Ended);
            Assert.IsNull(s.ActiveMeeting, "the meeting is cancelled when the night ends");
            Assert.IsNull(s.TakeOutcome());
        }

        [Test]
        public void FinishAwards_CrewWinEscaped()
        {
            var s = Make(new ulong[] { 0, 1 }, 0);
            s.CompleteRitual();
            s.Escape(0);
            s.Escape(1);
            var v = s.Tick(0.1f);
            var awards = s.FinishAwards(v);
            Assert.AreEqual(ScoreRules.Escaped + ScoreRules.CrewWinEach, awards[0]);
            Assert.AreEqual(ScoreRules.Escaped + ScoreRules.CrewWinEach, awards[1]);
        }

        [Test]
        public void Escape_And_Revive_DuringMeeting_Refused()
        {
            var s = Make(new ulong[] { 0, 1, 2 }, 0);
            s.CompleteRitual();
            s.Kill(2);
            s.RingBell(0, false, 45f, 15f);
            Assert.IsFalse(s.Escape(1), "a meeting is not a free escape window");
            Assert.IsFalse(s.Revive(2), "nor a free revive");
            Run(s, 61f);
            Assert.IsTrue(s.Escape(1));
            Assert.IsTrue(s.Revive(2));
        }

        [Test]
        public void Meeting_EjectsTargetWhoDied_StaysMiniGhostOnce()
        {
            var s = Make(Five, 1);
            var crew = CrewOf(s);
            OpenVote(s, crew[0]);
            foreach (var p in Five) s.ActiveMeeting?.Cast(p, crew[1]);
            s.Kill(crew[1]);
            s.Tick(0.5f);
            var o = s.TakeOutcome().Value;
            Assert.IsTrue(o.Ejected);
            Assert.AreEqual(LifeState.MiniGhost, s.StateOf(crew[1]));
            Assert.IsTrue(s.CanRevive(crew[1]));
        }

        [Test]
        public void Meeting_VoterLeaves_StillCloses()
        {
            var s = Make(Five, 1);
            OpenVote(s, 0);
            foreach (var p in new ulong[] { 0, 1, 2, 3 }) s.ActiveMeeting.Cast(p, null);
            s.PlayerLeft(4);
            s.Tick(0.1f);
            Assert.IsNull(s.ActiveMeeting, "every remaining voter has voted");
        }
    }

    public class MeetingTests
    {
        static Meeting Make() => new Meeting(0, new ulong[] { 0, 1, 2 }, new ulong[] { 0, 1, 2 }, 45f, 15f);

        [Test]
        public void Discuss45_ThenVote15_ThenClosed()
        {
            var m = Make();
            Assert.AreEqual(MeetingPhase.Discuss, m.Phase);
            m.Tick(44.5f);
            Assert.AreEqual(MeetingPhase.Discuss, m.Phase);
            Assert.AreEqual(MeetingPhase.Vote, m.Tick(0.5f));
            Assert.AreEqual(15f, m.TimeLeft, 1e-4f);
            m.Tick(14.5f);
            Assert.AreEqual(MeetingPhase.Vote, m.Phase);
            Assert.AreEqual(MeetingPhase.Closed, m.Tick(0.5f));
        }

        [Test]
        public void Cast_OnlyDuringVote()
        {
            var m = Make();
            Assert.IsFalse(m.Cast(0, 1));
            m.Tick(45f);
            Assert.IsTrue(m.Cast(0, 1));
        }

        [Test]
        public void Cast_LastVoteWins()
        {
            var m = Make();
            m.Tick(45f);
            m.Cast(0, 1);
            m.Cast(0, 2);
            Assert.AreEqual(2ul, m.Ballots[0]);
        }

        [Test]
        public void Cast_IneligibleVoterOrTarget_Ignored()
        {
            var m = new Meeting(0, new ulong[] { 0, 1 }, new ulong[] { 0, 1 }, 1f, 1f);
            m.Tick(1f);
            Assert.IsFalse(m.Cast(5, 0), "not a voter");
            Assert.IsFalse(m.Cast(0, 5), "not a target");
            Assert.IsTrue(m.Cast(0, 0), "voting for yourself is allowed");
            Assert.IsTrue(m.Cast(1, null));
        }

        [Test]
        public void AllVoted_ClosesEarly()
        {
            var m = Make();
            m.Tick(45f);
            m.Cast(0, 1);
            m.Cast(1, 1);
            Assert.AreEqual(MeetingPhase.Vote, m.Tick(0f));
            m.Cast(2, null);
            Assert.AreEqual(MeetingPhase.Closed, m.Tick(0f));
            Assert.IsTrue(m.Result.Ejected);
            Assert.AreEqual(1ul, m.Result.Target);
        }

        [Test]
        public void RemoveVoter_DropsBallot()
        {
            var m = Make();
            m.Tick(45f);
            m.Cast(2, 1);
            m.RemoveVoter(2);
            Assert.IsFalse(m.Ballots.ContainsKey(2));
            Assert.IsFalse(m.Voters.Contains(2ul));
            Assert.IsFalse(m.Cast(2, 1));
        }
    }

    public class GameSessionTests
    {
        [Test]
        public void Ranking_ByTotalDescThenId()
        {
            var g = new GameSession(3);
            foreach (var id in new ulong[] { 2, 0, 1 }) g.AddPlayer(id);
            g.ApplyAwards(new Dictionary<ulong, int> { { 0, 50 }, { 1, 120 }, { 2, 50 } });
            var r = g.Ranking();
            CollectionAssert.AreEqual(new ulong[] { 1, 0, 2 }, r.Select(x => x.Id).ToArray());
            Assert.AreEqual(120, r[0].Score);
            Assert.AreEqual(50, g.WalletOf(0).Coins);
        }

        [Test]
        public void ApplyAwards_UnknownPlayerAdded()
        {
            var g = new GameSession(1);
            g.ApplyAwards(new Dictionary<ulong, int> { { 9, 10 } });
            Assert.AreEqual(10, g.WalletOf(9).TotalScore);
        }

        [Test]
        public void NightsCount_IsOver()
        {
            var g = new GameSession(2);
            Assert.AreEqual(0, g.NightIndex);
            Assert.IsFalse(g.IsOver);
            g.NextNight();
            Assert.AreEqual(1, g.NightIndex);
            Assert.IsFalse(g.IsOver);
            g.NextNight();
            Assert.IsTrue(g.IsOver);
        }

        [Test]
        public void SealPlanner_PicksDistinctCount()
        {
            var two = new[] { false, false, false, true, true, true };
            for (int seed = 0; seed < 20; seed++)
            {
                var pick = SealPlanner.Pick(new SeededRandom(seed), two, 5, false);
                Assert.AreEqual(5, pick.Count);
                Assert.AreEqual(5, pick.Distinct().Count());
                Assert.IsTrue(pick.All(i => i >= 0 && i < 6));
            }
            Assert.AreEqual(6, SealPlanner.Pick(new SeededRandom(1), two, 9, false).Count, "never more than there are");
        }

        [Test]
        public void SealPlanner_TwoPlayers_AtMostOneTwoPerson()
        {
            var two = new[] { false, false, false, true, true, true };
            for (int seed = 0; seed < 20; seed++)
            {
                var pick = SealPlanner.Pick(new SeededRandom(seed), two, 3, true);
                Assert.AreEqual(3, pick.Count);
                Assert.LessOrEqual(pick.Count(i => two[i]), 1);
            }
            var mostlyTwo = new[] { true, true, true, false };
            Assert.AreEqual(3, SealPlanner.Pick(new SeededRandom(2), mostlyTwo, 3, true).Count, "falls back when there are not enough solo stations");
        }

        [Test]
        public void Stamina_Refill()
        {
            var s = new Stamina();
            s.Tick(10f, true, 1f);
            Assert.IsFalse(s.CanRun);
            s.Refill();
            Assert.AreEqual(Stamina.Max, s.Value);
            Assert.IsTrue(s.CanRun);
        }
    }
}
