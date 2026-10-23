using BooTrayal.Game.Networking;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class LaunchArgsTests
    {
        [Test]
        public void Parse_Defaults()
        {
            var a = LaunchArgs.Parse(new[] { "BOO-TRAYAL.exe" });
            Assert.AreEqual("main", a.Profile);
            Assert.IsFalse(a.AutoHost);
            Assert.IsNull(a.AutoJoinCode);
            Assert.IsFalse(a.AutoHostLocal);
            Assert.IsFalse(a.AutoJoinLocal);
            Assert.IsNull(a.FakeVersion);
            Assert.IsNull(a.CodeFile);
            Assert.IsNull(a.PlayerName);
        }

        [Test]
        public void Parse_AllFlags()
        {
            var a = LaunchArgs.Parse(new[]
            {
                "x.exe", "-profile", "b", "-autohost", "-autojoin", "abc123", "-autohost-local", "-autojoin-local",
                "-fakeversion", "0.0.9", "-codefile", "C:/t/code.txt", "-name", "Pim"
            });
            Assert.AreEqual("b", a.Profile);
            Assert.IsTrue(a.AutoHost);
            Assert.AreEqual("abc123", a.AutoJoinCode);
            Assert.IsTrue(a.AutoHostLocal);
            Assert.IsTrue(a.AutoJoinLocal);
            Assert.AreEqual("0.0.9", a.FakeVersion);
            Assert.AreEqual("C:/t/code.txt", a.CodeFile);
            Assert.AreEqual("Pim", a.PlayerName);
        }

        [Test]
        public void Parse_SmokeProbe()
        {
            Assert.AreEqual("walker", LaunchArgs.Parse(new[] { "x.exe", "-smokeprobe", "walker" }).SmokeProbe);
            Assert.IsNull(LaunchArgs.Parse(new[] { "x.exe" }).SmokeProbe);
        }

        [Test]
        public void Parse_MissingValue_Ignored()
        {
            var a = LaunchArgs.Parse(new[] { "x.exe", "-autojoin", "-profile" });
            Assert.IsNull(a.AutoJoinCode);
            Assert.AreEqual("main", a.Profile);
        }

        [Test]
        public void PlayScene_DefaultNull_ParsesValue()
        {
            Assert.IsNull(LaunchArgs.Parse(new[] { "x.exe" }).PlayScene);
            Assert.AreEqual("Sandbox", LaunchArgs.Parse(new[] { "x.exe", "-playscene", "Sandbox" }).PlayScene);
        }

        [Test]
        public void GhostWait_Flag()
        {
            Assert.IsFalse(LaunchArgs.Parse(new[] { "x.exe" }).GhostWait);
            Assert.IsTrue(LaunchArgs.Parse(new[] { "x.exe", "-ghostwait" }).GhostWait);
        }
    
        [Test]
        public void Parse_NightFlags()
        {
            var a = LaunchArgs.Parse(new[] { "x.exe", "-autostart", "5", "-nights", "2", "-nospy", "-spies", "2", "-noghost", "-meetingscale", "0.1", "-seed", "11" });
            Assert.AreEqual(5, a.AutoStart);
            Assert.AreEqual(2, a.Nights);
            Assert.IsTrue(a.NoSpy);
            Assert.AreEqual(2, a.Spies);
            Assert.IsTrue(a.NoGhost);
            Assert.AreEqual(0.1f, a.MeetingScale, 1e-6f);
            Assert.AreEqual(11, a.Seed);
            var d = LaunchArgs.Parse(new[] { "x.exe" });
            Assert.AreEqual(0, d.AutoStart);
            Assert.AreEqual(0, d.Nights);
            Assert.AreEqual(1f, d.MeetingScale);
            Assert.IsNull(d.Seed);
            Assert.IsNull(d.NightSeconds);
        }

        [Test]
        public void NightSeconds_CommaList()
        {
            var a = LaunchArgs.Parse(new[] { "x.exe", "-nightseconds", "200,15.5" });
            CollectionAssert.AreEqual(new[] { 200f, 15.5f }, a.NightSeconds);
            Assert.AreEqual(200f, a.NightSecondsFor(0));
            Assert.AreEqual(15.5f, a.NightSecondsFor(1));
            Assert.AreEqual(15.5f, a.NightSecondsFor(4), "the last value repeats");
        }

        [Test]
        public void Bad_NightSeconds_Ignored()
        {
            Assert.IsNull(LaunchArgs.Parse(new[] { "x.exe", "-nightseconds", "abc" }).NightSeconds);
            Assert.IsNull(LaunchArgs.Parse(new[] { "x.exe", "-nightseconds", "10,-5" }).NightSeconds);
            Assert.AreEqual(0, LaunchArgs.Parse(new[] { "x.exe", "-autostart", "lots" }).AutoStart);
            Assert.AreEqual(1f, LaunchArgs.Parse(new[] { "x.exe", "-meetingscale", "0" }).MeetingScale);
        }
    }
}
