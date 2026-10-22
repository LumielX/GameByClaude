using BooTrayal.Core.Player;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class CarryRulesTests
    {
        [Test]
        public void LoadFor_Cases()
        {
            Assert.AreEqual(CarryLoad.None, CarryRules.LoadFor(false, true, 2));
            Assert.AreEqual(CarryLoad.Light, CarryRules.LoadFor(true, false, 1));
            Assert.AreEqual(CarryLoad.HeavyAlone, CarryRules.LoadFor(true, true, 1));
            Assert.AreEqual(CarryLoad.HeavyShared, CarryRules.LoadFor(true, true, 2));
        }

        [Test]
        public void SpeedFactor_Values()
        {
            Assert.AreEqual(1f, CarryRules.SpeedFactor(CarryLoad.None));
            Assert.AreEqual(0.9f, CarryRules.SpeedFactor(CarryLoad.Light), 1e-5f);
            Assert.AreEqual(0.65f, CarryRules.SpeedFactor(CarryLoad.HeavyShared), 1e-5f);
            Assert.AreEqual(0.35f, CarryRules.SpeedFactor(CarryLoad.HeavyAlone), 1e-5f);
        }

        [Test]
        public void CanGrab_LightOnlyOneHolder()
        {
            Assert.IsTrue(CarryRules.CanGrab(false, 0, false, 1f));
            Assert.IsFalse(CarryRules.CanGrab(false, 1, false, 1f));
        }

        [Test]
        public void CanGrab_HeavyTwoHolders()
        {
            Assert.IsTrue(CarryRules.CanGrab(true, 1, false, 1f));
            Assert.IsFalse(CarryRules.CanGrab(true, 2, false, 1f));
        }

        [Test]
        public void CanGrab_OutOfRangeOrAlreadyHolding_False()
        {
            Assert.IsFalse(CarryRules.CanGrab(false, 0, false, 2.3f));
            Assert.IsFalse(CarryRules.CanGrab(false, 0, true, 1f));
            Assert.IsFalse(CarryRules.CanGrab(false, 0, false, float.NaN));
        }

        [Test]
        public void IsLifted_HeavyNeedsTwo()
        {
            Assert.IsFalse(CarryRules.IsLifted(true, 1));
            Assert.IsTrue(CarryRules.IsLifted(true, 2));
            Assert.IsTrue(CarryRules.IsLifted(false, 1));
        }

        [Test]
        public void ClampThrow_MaxAndNaN()
        {
            Assert.AreEqual(12f, CarryRules.ClampThrow(500f));
            Assert.AreEqual(0f, CarryRules.ClampThrow(float.NaN));
            Assert.AreEqual(0f, CarryRules.ClampThrow(-3f));
            Assert.AreEqual(6f, CarryRules.ClampThrow(6f));
        }
    }

    public class PushRulesTests
    {
        static PushOutcome Push(float distance = 1.5f, double now = 10, double last = 0, float speed = 3f, bool pusherDown = false, bool targetDown = false, ulong target = 2)
            => PushRules.Evaluate(1, target, distance, now, last, speed, pusherDown, targetDown);

        [Test]
        public void Allowed_InRange()
        {
            var o = Push();
            Assert.IsTrue(o.Allowed);
            Assert.IsFalse(o.Knockdown);
        }

        [Test]
        public void Running_Knockdown()
        {
            Assert.IsTrue(Push(speed: 5.6f * 0.8f).Knockdown);
        }

        [Test]
        public void Self_Denied() => Assert.AreEqual("self", Push(target: 1).Reason);

        [Test]
        public void OutOfRange_Denied() => Assert.AreEqual("range", Push(distance: 2.6f).Reason);

        [Test]
        public void Cooldown_Denied() => Assert.AreEqual("cooldown", Push(now: 10.5, last: 10).Reason);

        [Test]
        public void PusherDown_Denied() => Assert.AreEqual("pusher_down", Push(pusherDown: true).Reason);

        [Test]
        public void TargetDown_Denied() => Assert.AreEqual("target_down", Push(targetDown: true).Reason);

        [Test]
        public void NaNDistance_Denied() => Assert.AreEqual("range", Push(distance: float.NaN).Reason);
    }

    public class KnockdownTests
    {
        [Test]
        public void StartAndRecover()
        {
            var k = new Knockdown();
            k.Start(1.5f);
            Assert.IsTrue(k.IsDown);
            k.Tick(1.4f);
            Assert.IsTrue(k.IsDown);
            k.Tick(0.2f);
            Assert.IsFalse(k.IsDown);
            Assert.AreEqual(0f, k.Remaining);
        }

        [Test]
        public void Restart_KeepsLonger()
        {
            var k = new Knockdown();
            k.Start(1.5f);
            k.Tick(0.5f);
            k.Start(0.5f);
            Assert.AreEqual(1f, k.Remaining, 1e-5f);
            k.Start(2f);
            Assert.AreEqual(2f, k.Remaining, 1e-5f);
        }
    }

    public class NoiseRulesTests
    {
        [Test]
        public void SoftImpact_Silent()
        {
            Assert.AreEqual(0f, NoiseRules.FromImpact(1.9f, false).Loudness);
        }

        [Test]
        public void Impact_Scales()
        {
            var n = NoiseRules.FromImpact(7f, false);
            Assert.AreEqual(0.5f, n.Loudness, 1e-5f);
            Assert.AreEqual(12f, n.Radius, 1e-5f);
        }

        [Test]
        public void Heavy_Doubles_Clamped()
        {
            Assert.AreEqual(1f, NoiseRules.FromImpact(7f, true).Loudness, 1e-5f);
            Assert.AreEqual(1f, NoiseRules.FromImpact(100f, false).Loudness, 1e-5f);
            Assert.AreEqual(20f, NoiseRules.FromImpact(100f, false).Radius, 1e-5f);
        }

        [Test]
        public void NaN_Silent()
        {
            Assert.AreEqual(0f, NoiseRules.FromImpact(float.NaN, true).Loudness);
        }

        [Test]
        public void Footsteps()
        {
            Assert.AreEqual(0.25f, NoiseRules.Footstep(true, false).Loudness, 1e-5f);
            Assert.AreEqual(8f, NoiseRules.Footstep(true, false).Radius, 1e-5f);
            Assert.AreEqual(0f, NoiseRules.Footstep(false, false).Loudness);
            Assert.AreEqual(0f, NoiseRules.Footstep(true, true).Loudness);
        }
    }
}
