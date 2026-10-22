using System;
using BooTrayal.Core.Fear;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class FearModelTests
    {
        static void AssertNear(FearLevels a, FearLevels b, float tol)
        {
            Assert.AreEqual(a.Whisper, b.Whisper, tol);
            Assert.AreEqual(a.Heartbeat, b.Heartbeat, tol);
            Assert.AreEqual(a.Vignette, b.Vignette, tol);
            Assert.AreEqual(a.Flicker, b.Flicker, tol);
            Assert.AreEqual(a.Shake, b.Shake, tol);
            Assert.AreEqual(a.Chromatic, b.Chromatic, tol);
        }

        [Test]
        public void Fear_Far_AllZero()
        {
            AssertNear(default, FearModel.Evaluate(30f), 0f);
        }

        [Test]
        public void Fear_20m_WhisperOnly()
        {
            var f = FearModel.Evaluate(20f);
            Assert.That(f.Whisper, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.AreEqual(0f, f.Heartbeat);
            Assert.AreEqual(0f, f.Shake);
        }

        [Test]
        public void Fear_10m_HeartbeatPartial_NoShake()
        {
            var f = FearModel.Evaluate(10f);
            Assert.AreEqual(1f, f.Whisper);
            Assert.That(f.Heartbeat, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.AreEqual(f.Heartbeat, f.Vignette);
            Assert.AreEqual(f.Heartbeat, f.Flicker);
            Assert.AreEqual(0f, f.Shake);
            Assert.AreEqual(0f, f.Chromatic);
        }

        [Test]
        public void Fear_3m_ShakeHigh()
        {
            var f = FearModel.Evaluate(3f);
            Assert.AreEqual(1f, f.Heartbeat);
            Assert.That(f.Shake, Is.GreaterThan(0.8f));
            Assert.AreEqual(f.Shake, f.Chromatic);
        }

        [TestCase(25f)]
        [TestCase(15f)]
        [TestCase(8f)]
        public void Fear_Continuous_AtBoundaries(float d)
        {
            AssertNear(FearModel.Evaluate(d - 0.001f), FearModel.Evaluate(d + 0.001f), 0.01f);
        }

        [Test]
        public void Fear_NaN_NoFear()
        {
            AssertNear(default, FearModel.Evaluate(float.NaN), 0f);
            AssertNear(default, FearModel.Evaluate(-1f), 0f);
        }
    }
}
