using System.Linq;
using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class RoomRulesTests
    {
        [Test]
        public void SpyCount_FourPlayersEnabled_Zero()
        {
            Assert.AreEqual(0, RoomRules.SpyCount(4, true, 1));
        }

        [Test]
        public void SpyCount_FivePlayers_One()
        {
            Assert.AreEqual(1, RoomRules.SpyCount(5, true, 1));
            Assert.AreEqual(1, RoomRules.SpyCount(5, true, 2));
        }

        [Test]
        public void SpyCount_Disabled_Zero()
        {
            Assert.AreEqual(0, RoomRules.SpyCount(6, false, 1));
        }

        [Test]
        public void SpyCount_EightPlayersTwoRequested_Two()
        {
            Assert.AreEqual(2, RoomRules.SpyCount(8, true, 2));
            Assert.AreEqual(1, RoomRules.SpyCount(7, true, 1));
        }

        [TestCase(2, 3)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        [TestCase(5, 5)]
        [TestCase(6, 5)]
        [TestCase(7, 6)]
        [TestCase(8, 6)]
        public void SealCount_ByPlayers(int players, int seals)
        {
            Assert.AreEqual(seals, RoomRules.SealCount(players));
        }

        [Test]
        public void CanToggleSpy_FromFivePlayers()
        {
            Assert.IsFalse(RoomRules.CanToggleSpy(4));
            Assert.IsTrue(RoomRules.CanToggleSpy(5));
        }

        [Test]
        public void MaxSpiesAllowed_ByPlayers()
        {
            Assert.AreEqual(0, RoomRules.MaxSpiesAllowed(4));
            Assert.AreEqual(1, RoomRules.MaxSpiesAllowed(6));
            Assert.AreEqual(2, RoomRules.MaxSpiesAllowed(7));
        }

        [Test]
        public void Validate_Defaults_NoErrors()
        {
            CollectionAssert.IsEmpty(RoomRules.Validate(new GameConfig()));
        }

        [Test]
        public void Validate_NineMaxPlayers_Error()
        {
            CollectionAssert.Contains(RoomRules.Validate(new GameConfig { MaxPlayers = 9 }).ToList(), "max_players_range");
            CollectionAssert.Contains(RoomRules.Validate(new GameConfig { MaxPlayers = 1 }).ToList(), "max_players_range");
        }

        [Test]
        public void Validate_NightMinutes9_Error()
        {
            CollectionAssert.Contains(RoomRules.Validate(new GameConfig { NightMinutes = 9 }).ToList(), "night_minutes_invalid");
        }

        [Test]
        public void Validate_Nights2_Error()
        {
            CollectionAssert.Contains(RoomRules.Validate(new GameConfig { Nights = 2 }).ToList(), "nights_invalid");
        }

        [Test]
        public void Validate_TwoSpiesWithMax6_Error()
        {
            var errors = RoomRules.Validate(new GameConfig { MaxPlayers = 6, RequestedSpies = 2 }).ToList();
            CollectionAssert.Contains(errors, "spies_invalid");
        }
    }
}
