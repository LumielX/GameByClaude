using System.Collections.Generic;
using System.IO;
using System.Linq;
using BooTrayal.Core.Localization;
using BooTrayal.Core.Net;
using BooTrayal.Core.Rules;
using NUnit.Framework;
using UnityEngine;

namespace BooTrayal.Tests
{
    public class LocalizationTests
    {
        static string Table(string lang) => File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/Lang", lang + ".txt"));

        [Test]
        public void Parse_KeyValue_CommentsBlankNewline()
        {
            var t = Localizer.Parse("# comment\n\nmenu.play=เล่น\nmulti=บรรทัด 1\\nบรรทัด 2\n  spaced  =  a=b  \nbroken line\n");
            Assert.AreEqual("เล่น", t["menu.play"]);
            Assert.AreEqual("บรรทัด 1\nบรรทัด 2", t["multi"]);
            Assert.AreEqual("a=b", t["spaced"], "first = splits; both sides trimmed");
            Assert.AreEqual(3, t.Count, "comments, blanks and lines without = are skipped");
        }

        [Test]
        public void Get_FallsBackToOtherLanguageThenKey()
        {
            var l = new Localizer();
            l.Load(Language.Thai, new Dictionary<string, string> { { "a", "ก" } });
            l.Load(Language.English, new Dictionary<string, string> { { "a", "A" }, { "b", "B" } });
            Assert.AreEqual(Language.Thai, l.Current);
            Assert.AreEqual("ก", l.Get("a"));
            Assert.AreEqual("B", l.Get("b"), "missing in Thai → English");
            Assert.AreEqual("zzz", l.Get("zzz"), "missing everywhere → the key");
            l.Set(Language.English);
            Assert.AreEqual("A", l.Get("a"));
        }

        [Test]
        public void Set_RaisesChanged()
        {
            var l = new Localizer();
            int n = 0;
            l.Changed += () => n++;
            l.Set(Language.English);
            l.Set(Language.English);
            Assert.AreEqual(1, n, "only on an actual change");
        }

        [Test]
        public void Format_BadPattern_ReturnsRaw()
        {
            var l = new Localizer();
            l.Load(Language.Thai, new Dictionary<string, string> { { "t", "คืนที่ {0}/{1}" }, { "bad", "x {0" } });
            Assert.AreEqual("คืนที่ 1/3", l.Format("t", 1, 3));
            Assert.AreEqual("x {0", l.Format("bad", 1));
        }

        [Test]
        public void Tables_SameKeys()
        {
            var th = Localizer.Parse(Table("th"));
            var en = Localizer.Parse(Table("en"));
            CollectionAssert.IsEmpty(th.Keys.Except(en.Keys).ToList(), "keys only in th.txt");
            CollectionAssert.IsEmpty(en.Keys.Except(th.Keys).ToList(), "keys only in en.txt");
            Assert.Greater(th.Count, 50);
        }

        [Test]
        public void Tables_NoEmptyValues()
        {
            foreach (var lang in new[] { "th", "en" })
                foreach (var kv in Localizer.Parse(Table(lang)))
                    Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key} is empty");
        }

        [Test]
        public void Tables_CoverNetMessages()
        {
            var th = Localizer.Parse(Table("th"));
            foreach (var key in BooTrayal.Game.Networking.NetMessages.Keys)
                Assert.IsTrue(th.ContainsKey("net." + key), $"net.{key} missing");
        }
    }

    public class ChatAndLobbyTests
    {
        [Test]
        public void Sanitize_TrimsAndCaps120()
        {
            Assert.AreEqual("สวัสดี", ChatRules.Sanitize("  สวัสดี  "));
            Assert.AreEqual(ChatRules.MaxLength, ChatRules.Sanitize(new string('a', 500)).Length);
            Assert.AreEqual("a b", ChatRules.Sanitize("a \t\n b"), "runs of whitespace collapse");
        }

        [Test]
        public void Sanitize_DropsControlAndFormatChars()
        {
            Assert.AreEqual("abc", ChatRules.Sanitize("a\u0007b‮c​"));
        }

        [Test]
        public void Sanitize_EmptyOrWhitespace_Null()
        {
            Assert.IsNull(ChatRules.Sanitize(null));
            Assert.IsNull(ChatRules.Sanitize("   "));
            Assert.IsNull(ChatRules.Sanitize("​\u0007"));
        }

        [Test]
        public void CanStart_AllReady() => Assert.IsTrue(LobbyRules.CanStart(3, 3));

        [Test]
        public void CanStart_NotAllReady_False() => Assert.IsFalse(LobbyRules.CanStart(3, 2));

        [Test]
        public void CanStart_Zero_False() => Assert.IsFalse(LobbyRules.CanStart(0, 0));
    }
}
