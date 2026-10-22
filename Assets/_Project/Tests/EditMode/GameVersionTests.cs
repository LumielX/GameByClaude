using System.Text.RegularExpressions;
using BooTrayal.Core;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class GameVersionTests
    {
        [Test]
        public void IsCompatible_SameVersion_True()
        {
            Assert.IsTrue(GameVersion.IsCompatible("0.1.0", "0.1.0"));
        }

        [Test]
        public void IsCompatible_DifferentPatch_False()
        {
            Assert.IsFalse(GameVersion.IsCompatible("0.1.0", "0.1.1"));
        }

        [Test]
        public void IsCompatible_Whitespace_Trimmed()
        {
            Assert.IsTrue(GameVersion.IsCompatible(" 0.1.0", "0.1.0 "));
        }

        [Test]
        public void IsCompatible_NullOrEmpty_False()
        {
            Assert.IsFalse(GameVersion.IsCompatible(null, "0.1.0"));
            Assert.IsFalse(GameVersion.IsCompatible("", ""));
        }

        [Test]
        public void Current_IsSemver()
        {
            Assert.IsTrue(Regex.IsMatch(GameVersion.Current, @"^\d+\.\d+\.\d+$"), GameVersion.Current);
        }
    }
}
