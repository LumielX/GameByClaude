using BooTrayal.Game.Networking;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class NetMessagesTests
    {
        static readonly string[] RequiredKeys =
        {
            "version_mismatch", "room_full", "game_in_progress", "bad_payload", "host_left",
            "code_invalid", "room_not_found", "services_unavailable", "connect_failed"
        };

        [Test]
        public void EveryKey_HasThaiAndEnglish()
        {
            foreach (var key in RequiredKeys)
            {
                CollectionAssert.Contains(NetMessages.Keys, key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(NetMessages.Thai(key)), key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(NetMessages.English(key)), key);
                Assert.AreNotEqual(NetMessages.Thai(key), NetMessages.English(key), key);
            }
        }

        [Test]
        public void VersionMismatch_HasReleaseLink()
        {
            StringAssert.Contains(NetMessages.ReleasesUrl, NetMessages.Thai("version_mismatch"));
            StringAssert.Contains(NetMessages.ReleasesUrl, NetMessages.English("version_mismatch"));
        }

        [Test]
        public void ReleasesUrl_ShowsPrereleases()
        {
            // GitHub's /releases/latest skips pre-releases, so an alpha-only repo would show a 404 there.
            Assert.AreEqual("https://github.com/LumielX/GameByClaude/releases", NetMessages.ReleasesUrl);
            StringAssert.DoesNotEndWith("/latest", NetMessages.ReleasesUrl);
        }

        [Test]
        public void UnknownKey_FallsBack()
        {
            Assert.AreEqual(NetMessages.Thai("connect_failed"), NetMessages.Thai("no_such_key"));
            Assert.AreEqual(NetMessages.English("connect_failed"), NetMessages.English(null));
        }
    }
}
