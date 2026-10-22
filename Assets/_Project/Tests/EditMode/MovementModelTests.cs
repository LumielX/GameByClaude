using BooTrayal.Core.Player;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class MovementModelTests
    {
        static MoveInput Forward(bool run = false, bool crouch = false) => new MoveInput { Y = 1f, Run = run, Crouch = crouch };

        [Test]
        public void Walk_3_2()
        {
            Assert.AreEqual(3.2f, MovementModel.TargetSpeed(Forward(), true, 1f, CarryLoad.None, false), 1e-4f);
        }

        [Test]
        public void Run_5_6_TimesMultiplier()
        {
            Assert.AreEqual(5.6f, MovementModel.TargetSpeed(Forward(run: true), true, 1f, CarryLoad.None, false), 1e-4f);
            Assert.AreEqual(5.6f * 1.4f, MovementModel.TargetSpeed(Forward(run: true), true, 1.4f, CarryLoad.None, false), 1e-4f);
        }

        [Test]
        public void Run_WithoutStamina_Walks()
        {
            Assert.AreEqual(3.2f, MovementModel.TargetSpeed(Forward(run: true), false, 1f, CarryLoad.None, false), 1e-4f);
            Assert.IsFalse(MovementModel.IsRunning(Forward(run: true), false, false));
        }

        [Test]
        public void Crouch_1_6_NoRun()
        {
            Assert.AreEqual(1.6f, MovementModel.TargetSpeed(Forward(run: true, crouch: true), true, 1f, CarryLoad.None, false), 1e-4f);
            Assert.IsFalse(MovementModel.IsRunning(Forward(run: true, crouch: true), true, false));
        }

        [Test]
        public void Diagonal_NotFaster()
        {
            Assert.AreEqual(1f, MovementModel.InputMagnitude(1f, 1f), 1e-4f);
            Assert.AreEqual(0.5f, MovementModel.InputMagnitude(0.5f, 0f), 1e-4f);
        }

        [Test]
        public void NaN_Input_Zero()
        {
            Assert.AreEqual(0f, MovementModel.InputMagnitude(float.NaN, 1f));
            var input = new MoveInput { X = float.NaN, Y = float.PositiveInfinity };
            Assert.AreEqual(0f, MovementModel.TargetSpeed(input, true, 1f, CarryLoad.None, false));
        }

        [Test]
        public void NoInput_Zero()
        {
            Assert.AreEqual(0f, MovementModel.TargetSpeed(new MoveInput { Run = true }, true, 1f, CarryLoad.None, false));
            Assert.IsFalse(MovementModel.IsRunning(new MoveInput { Run = true }, true, false));
        }

        [Test]
        public void Knocked_Zero()
        {
            Assert.AreEqual(0f, MovementModel.TargetSpeed(Forward(run: true), true, 1f, CarryLoad.None, true));
        }

        [Test]
        public void HeavyAlone_Slow()
        {
            Assert.AreEqual(3.2f * 0.35f, MovementModel.TargetSpeed(Forward(), true, 1f, CarryLoad.HeavyAlone, false), 1e-4f);
            Assert.AreEqual(5.6f * 0.65f, MovementModel.TargetSpeed(Forward(run: true), true, 1f, CarryLoad.HeavyShared, false), 1e-4f);
        }

        [Test]
        public void Jump_FromGround()
        {
            Assert.AreEqual(4.8f, MovementModel.NextVerticalVelocity(0f, true, true, false, CarryLoad.None, false, 0.02f), 1e-4f);
            Assert.AreEqual(4.8f, MovementModel.NextVerticalVelocity(0f, true, true, false, CarryLoad.Light, false, 0.02f), 1e-4f);
        }

        [Test]
        public void NoJump_Crouched_HeavyOrKnocked()
        {
            Assert.That(MovementModel.NextVerticalVelocity(0f, true, true, true, CarryLoad.None, false, 0.02f), Is.LessThanOrEqualTo(0f));
            Assert.That(MovementModel.NextVerticalVelocity(0f, true, true, false, CarryLoad.HeavyShared, false, 0.02f), Is.LessThanOrEqualTo(0f));
            Assert.That(MovementModel.NextVerticalVelocity(0f, true, true, false, CarryLoad.None, true, 0.02f), Is.LessThanOrEqualTo(0f));
            Assert.That(MovementModel.NextVerticalVelocity(0f, false, true, false, CarryLoad.None, false, 0.02f), Is.LessThan(0f));
        }

        [Test]
        public void Gravity_Accumulates()
        {
            Assert.AreEqual(2f - 18f * 0.1f, MovementModel.NextVerticalVelocity(2f, false, false, false, CarryLoad.None, false, 0.1f), 1e-4f);
        }

        [Test]
        public void Grounded_SnapsSmallNegative()
        {
            Assert.AreEqual(MovementModel.GroundSnapVelocity, MovementModel.NextVerticalVelocity(-7f, true, false, false, CarryLoad.None, false, 0.02f), 1e-4f);
        }
    }
}
