using BooTrayal.Core.Net;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class NetErrorTextTests
    {
        [Test]
        public void InvalidCharacter_CodeInvalid()
        {
            Assert.AreEqual("code_invalid", NetErrorText.Classify("Lobby code contains an invalid character"));
        }

        [Test]
        public void Full_RoomFull()
        {
            Assert.AreEqual("room_full", NetErrorText.Classify("lobby is full"));
        }

        [Test]
        public void NotFound_RoomNotFound()
        {
            Assert.AreEqual("room_not_found", NetErrorText.Classify("Lobby not found"));
        }

        [Test]
        public void Unknown_Null()
        {
            Assert.IsNull(NetErrorText.Classify("something odd"));
            Assert.IsNull(NetErrorText.Classify(null));
        }
    }
}
