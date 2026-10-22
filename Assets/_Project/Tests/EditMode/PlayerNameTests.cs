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
        public void Sanitize_StackedMarks_CappedAtTwo()
        {
            Assert.AreEqual("ก่่", PlayerName.Sanitize("ก" + new string('่', 10), 1));
        }

        [Test]
        public void Sanitize_ByteCap_PayloadFits()
        {
            var evil = string.Concat(Enumerable.Repeat("กั้", 40)) + new string('่', 500);
            var name = PlayerName.Sanitize(evil, 1);
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(name), Is.LessThanOrEqualTo(PlayerName.MaxBytes));
            Assert.That(ConnectionPayload.Encode("0.1.0", name).Length, Is.LessThanOrEqualTo(ConnectionPayload.MaxBytes));
            Assert.IsNotEmpty(name);
        }

        [Test]
        public void Sanitize_FormatChars_Removed()
        {
            Assert.AreEqual("AB", PlayerName.Sanitize("A​B‮", 1));
        }

        [Test]
        public void Sanitize_ControlChars_Removed()
        {
            Assert.AreEqual("AB", PlayerName.Sanitize("A\u0000\u0007B\n", 1));
        }
    }
}
