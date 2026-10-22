using System.Text;
using BooTrayal.Core.Net;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class ConnectionPayloadTests
    {
        [Test]
        public void Payload_RoundTrip()
        {
            var bytes = ConnectionPayload.Encode("0.1.0", "พิม");
            Assert.IsTrue(ConnectionPayload.TryDecode(bytes, out var p));
            Assert.AreEqual("0.1.0", p.Version);
            Assert.AreEqual("พิม", p.PlayerName);
        }

        [Test]
        public void Payload_Garbage_False()
        {
            Assert.IsFalse(ConnectionPayload.TryDecode(Encoding.UTF8.GetBytes("hello"), out _));
            Assert.IsFalse(ConnectionPayload.TryDecode(Encoding.UTF8.GetBytes("BT1\n0.1.0"), out _));
            Assert.IsFalse(ConnectionPayload.TryDecode(Encoding.UTF8.GetBytes("XX1\n0.1.0\nname"), out _));
        }

        [Test]
        public void Payload_Null_False()
        {
            Assert.IsFalse(ConnectionPayload.TryDecode(null, out _));
        }

        [Test]
        public void Payload_TooBig_False()
        {
            var big = Encoding.UTF8.GetBytes("BT1\n0.1.0\n" + new string('x', 300));
            Assert.IsFalse(ConnectionPayload.TryDecode(big, out _));
        }
    }
}
