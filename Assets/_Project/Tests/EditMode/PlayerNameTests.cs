using System.Linq;
using BooTrayal.Core.Net;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class PlayerNameTests
    {
        [Test]
        public void Sanitize_Trims()
        {
            Assert.AreEqual("Pim", PlayerName.Sanitize("   Pim  ", 1));
        }

        [Test]
        public void Sanitize_Empty_Fallback()
        {
            Assert.AreEqual("Player42", PlayerName.Sanitize("   ", 42));
            Assert.AreEqual("Player42", PlayerName.Sanitize(null, 42));
        }

        [Test]
        public void Sanitize_CollapsesWhitespace()
        {
            Assert.AreEqual("a b", PlayerName.Sanitize("a  \t  b", 1));
        }

        [Test]
        public void Sanitize_TooLong_Cut16()
        {
            Assert.AreEqual(new string('x', 16), PlayerName.Sanitize(new string('x', 40), 1));
        }

        [Test]
        public void Sanitize_ThaiCombining_NotSplit()
        {
            var raw = string.Concat(Enumerable.Repeat("ก่", 20));
            var name = PlayerName.Sanitize(raw, 1);
            Assert.AreEqual(32, name.Length);
            Assert.AreEqual('่', name[name.Length - 1]);
            Assert.AreEqual('ก', name[name.Length - 2]);
        }

        [Test]
        public void Sanitize_ControlChars_Removed()
        {
            Assert.AreEqual("AB", PlayerName.Sanitize("A\u0000\u0007B\n", 1));
        }
    }
}
