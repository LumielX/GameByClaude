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
        public void Parse_MissingValue_Ignored()
        {
            var a = LaunchArgs.Parse(new[] { "x.exe", "-autojoin", "-profile" });
            Assert.IsNull(a.AutoJoinCode);
            Assert.AreEqual("main", a.Profile);
        }
    }
}
