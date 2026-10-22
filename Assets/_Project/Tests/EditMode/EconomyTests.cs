using System;
using System.Collections.Generic;
using BooTrayal.Core.Economy;
using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class EconomyTests
    {
        static NightPlayer P(ulong id, Role role, LifeState state, bool exposed = false) =>
            new NightPlayer { Id = id, Role = role, State = state, Exposed = exposed };

        [Test]
        public void Spend_DoesNotLowerScore()
        {
            var w = new Wallet();
            w.Earn(100);
            Assert.IsTrue(w.TrySpend(60));
            Assert.AreEqual(40, w.Coins);
            Assert.AreEqual(100, w.TotalScore);
        }

        [Test]
        public void TrySpend_Insufficient_False()
        {
            var w = new Wallet();
            w.Earn(20);
            Assert.IsFalse(w.TrySpend(21));
            Assert.AreEqual(20, w.Coins);
        }

        [Test]
        public void Earn_NonPositive_Throws()
        {
            var w = new Wallet();
            Assert.Throws<ArgumentOutOfRangeException>(() => w.Earn(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => w.Earn(-5));
        }

        [Test]
        public void CrewWin_AllCrewGetBonus_EscapedGetExtra()
        {
            var players = new List<NightPlayer>
            {
                P(1, Role.Spy, LifeState.Alive),
                P(2, Role.Crew, LifeState.Escaped),
                P(3, Role.Crew, LifeState.MiniGhost),
            };
            var awards = ScoreRules.NightEndAwards(players, NightWinner.Crew, new ulong[0]);
            Assert.AreEqual(ScoreRules.Escaped + ScoreRules.CrewWinEach, awards[2]);
            Assert.AreEqual(ScoreRules.CrewWinEach, awards[3]);
            Assert.IsFalse(awards.ContainsKey(1));
        }

        [Test]
        public void SpyWin_OnlyUncaughtSpyPaid()
        {
            var players = new List<NightPlayer>
            {
                P(1, Role.Spy, LifeState.Alive),
                P(4, Role.Spy, LifeState.MiniGhost, exposed: true),
                P(2, Role.Crew, LifeState.MiniGhost),
            };
            var awards = ScoreRules.NightEndAwards(players, NightWinner.Spies, new ulong[0]);
            Assert.AreEqual(150, awards[1]);
            Assert.IsFalse(awards.ContainsKey(4));
            Assert.IsFalse(awards.ContainsKey(2));
        }

        [Test]
        public void DisconnectedSpy_NotPaid()
        {
            var players = new List<NightPlayer> { P(1, Role.Spy, LifeState.Disconnected) };
            var awards = ScoreRules.NightEndAwards(players, NightWinner.Spies, new ulong[0]);
            Assert.IsFalse(awards.ContainsKey(1));
        }

        [Test]
        public void CorrectVoters_Paid()
        {
            var players = new List<NightPlayer> { P(2, Role.Crew, LifeState.Alive), P(3, Role.Crew, LifeState.Alive) };
            var awards = ScoreRules.NightEndAwards(players, NightWinner.None, new ulong[] { 2 });
            Assert.AreEqual(30, awards[2]);
            Assert.IsFalse(awards.ContainsKey(3));
        }

        [Test]
        public void Constants_MatchSpec()
        {
            Assert.AreEqual(20, ScoreRules.SealClosed);
            Assert.AreEqual(10, ScoreRules.RitualItemDelivered);
            Assert.AreEqual(50, ScoreRules.Escaped);
            Assert.AreEqual(50, ScoreRules.CrewWinEach);
            Assert.AreEqual(150, ScoreRules.SpyWin);
            Assert.AreEqual(30, ScoreRules.CorrectVoteEach);
        }
    }
}
