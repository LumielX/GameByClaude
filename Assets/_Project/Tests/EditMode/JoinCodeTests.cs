using BooTrayal.Core.Net;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class JoinCodeTests
    {
        [Test]
        public void Normalize_LowerAndSpaces()
        {
            Assert.AreEqual("ABC123", JoinCode.Normalize(" ab c-12 3"));
        }

        [Test]
        public void Normalize_Null_Empty()
        {
            Assert.AreEqual("", JoinCode.Normalize(null));
        }

        [Test]
        public void IsValid_SixAlnum_True()
        {
            Assert.IsTrue(JoinCode.IsValid("ABC123"));
        }

        [Test]
        public void IsValid_WrongLength_False()
        {
            Assert.IsFalse(JoinCode.IsValid("ABC12"));
            Assert.IsFalse(JoinCode.IsValid("ABC1234"));
            Assert.IsFalse(JoinCode.IsValid(""));
        }

        [Test]
        public void IsValid_Symbols_False()
        {
            Assert.IsFalse(JoinCode.IsValid("ABC12!"));
            Assert.IsFalse(JoinCode.IsValid("abc123"));
        }
    }
}
