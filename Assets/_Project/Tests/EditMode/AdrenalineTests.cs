using BooTrayal.Core.Fear;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class AdrenalineTests
    {
        static void Run(AdrenalineModel m, float seconds, float distance, float step = 0.1f)
        {
            for (float t = 0; t < seconds - 1e-4f; t += step) m.Tick(step, distance);
        }

        [Test]
        public void Stamina_RunDrains_RegenAfterDelay()
        {
            var s = new Stamina();
            s.Tick(1f, true, 1f);
            Assert.AreEqual(80f, s.Value, 0.01f);
            s.Tick(0.5f, false, 1f);
            Assert.AreEqual(80f, s.Value, 0.01f);
            s.Tick(1f, false, 1f);
            Assert.That(s.Value, Is.GreaterThan(80f));
        }

        [Test]
        public void Stamina_Exhausted_NeedsHysteresis()
        {
            var s = new Stamina();
            for (int i = 0; i < 55; i++) s.Tick(0.1f, true, 1f);
            Assert.AreEqual(0f, s.Value, 0.01f);
            Assert.IsFalse(s.CanRun);
            for (int i = 0; i < 20; i++) s.Tick(0.1f, false, 1f);
            Assert.That(s.Value, Is.GreaterThan(0f).And.LessThan(25f));
            Assert.IsFalse(s.CanRun);
            for (int i = 0; i < 30; i++) s.Tick(0.1f, false, 1f);
            Assert.IsTrue(s.CanRun);
        }

        [Test]
        public void Stamina_HoldingSprintWhileExhausted_StillRecovers()
        {
            var s = new Stamina();
            for (int i = 0; i < 55; i++) s.Tick(0.1f, true, 1f);
            Assert.IsFalse(s.CanRun);
            bool recovered = false;
            for (int i = 0; i < 40; i++)
            {
                s.Tick(0.1f, true, 1f);
                recovered |= s.CanRun;
            }
            Assert.IsTrue(recovered);
        }

        [Test]
        public void Stamina_DrainMultiplier_Applies()
        {
            var s = new Stamina();
            s.Tick(1f, true, 1.5f);
            Assert.AreEqual(70f, s.Value, 0.01f);
        }

        [Test]
        public void Adrenaline_At15m_Plus25()
        {
            var m = new AdrenalineModel();
            m.Tick(0.1f, 15f);
            Assert.AreEqual(AdrenalineState.Adrenaline, m.State);
            Assert.AreEqual(1.25f, m.SpeedMultiplier, 0.001f);
        }

        [Test]
        public void Adrenaline_At5m_Plus40()
        {
            var m = new AdrenalineModel();
            m.Tick(0.1f, 5f);
            Assert.AreEqual(1.40f, m.SpeedMultiplier, 0.001f);
            m.Tick(0.1f, 1f);
            Assert.AreEqual(1.40f, m.SpeedMultiplier, 0.001f);
        }

        [Test]
        public void Adrenaline_Far_Normal()
        {
            var m = new AdrenalineModel();
            m.Tick(0.1f, 16f);
            Assert.AreEqual(AdrenalineState.Normal, m.State);
            Assert.AreEqual(1f, m.SpeedMultiplier);
            Assert.AreEqual(1f, m.StaminaDrainMultiplier);
        }

        [Test]
        public void Adrenaline_DrainX1_5()
        {
            var m = new AdrenalineModel();
            m.Tick(0.1f, 10f);
            Assert.AreEqual(1.5f, m.StaminaDrainMultiplier, 0.001f);
        }

        [Test]
        public void Winded_AfterGhostLeaves3s_Lasts4s()
        {
            var m = new AdrenalineModel();
            m.Tick(0.1f, 10f);
            Run(m, 2.9f, 30f);
            Assert.AreNotEqual(AdrenalineState.Winded, m.State);
            Run(m, 0.2f, 30f);
            Assert.AreEqual(AdrenalineState.Winded, m.State);
            Assert.AreEqual(0.8f, m.SpeedMultiplier, 0.001f);
            Run(m, 3.8f, 30f);
            Assert.AreEqual(AdrenalineState.Winded, m.State);
            Run(m, 0.3f, 30f);
            Assert.AreEqual(AdrenalineState.Normal, m.State);
            Assert.AreEqual(1f, m.SpeedMultiplier);
        }

        [Test]
        public void Winded_GhostReturns_BackToAdrenaline()
        {
            var m = new AdrenalineModel();
            m.Tick(0.1f, 10f);
            Run(m, 3.2f, 30f);
            Assert.AreEqual(AdrenalineState.Winded, m.State);
            m.Tick(0.1f, 12f);
            Assert.AreEqual(AdrenalineState.Adrenaline, m.State);
        }
    
        [Test]
        public void Adrenaline_Reset_BackToNormal()
        {
            var m = new AdrenalineModel();
            m.Tick(0.1f, 3f);
            Assert.AreEqual(AdrenalineState.Adrenaline, m.State);
            m.Reset();
            Assert.AreEqual(AdrenalineState.Normal, m.State);
            Assert.AreEqual(1f, m.SpeedMultiplier);
            Assert.AreEqual(1f, m.StaminaDrainMultiplier);
            m.Tick(0.1f, 100f);
            Assert.AreEqual(AdrenalineState.Normal, m.State, "no leftover winded timer");
        }
    }
}
