using BooTrayal.Core.Fear;
using BooTrayal.Core.Ghosts;
using BooTrayal.Core.Player;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class GhostBrainTests
    {
        const ulong Victim = 7;

        static GhostSenses Nothing => new GhostSenses();

        static GhostSenses Sees(ulong id) => new GhostSenses { SeesPlayer = true, SeenId = id };

        static GhostSenses Target(float distance, bool visible = true, bool harmable = true) => new GhostSenses
        {
            TargetValid = true, TargetVisible = visible, TargetDistance = distance, TargetHarmable = harmable
        };

        static GhostSenses Lit => new GhostSenses { Lit = true };

        /// <summary>Ticks in 0.5 s steps (exact in binary floating point); returns the last non-None event.</summary>
        static GhostEvent Run(GhostBrain b, float seconds, GhostSenses s)
        {
            var last = GhostEvent.None;
            for (float t = 0f; t < seconds - 1e-4f; t += 0.5f)
            {
                var e = b.Tick(0.5f, s);
                if (e != GhostEvent.None) last = e;
            }
            return last;
        }

        static GhostBrain Chasing()
        {
            var b = new GhostBrain();
            b.Tick(0.5f, Sees(Victim));
            Run(b, 1f, Target(10f));
            Assert.AreEqual(GhostState.Chase, b.State);
            return b;
        }

        [Test]
        public void Starts_Wander()
        {
            var b = new GhostBrain();
            Assert.AreEqual(GhostState.Wander, b.State);
            Assert.IsFalse(b.HasTarget);
            Assert.AreEqual(GhostTuning.WanderSpeed, b.Speed, 1e-5f);
        }

        [Test]
        public void SeesPlayer_Alert_ThenChaseAfter1s()
        {
            var b = new GhostBrain();
            Assert.AreEqual(GhostEvent.Alerted, b.Tick(0.1f, Sees(Victim)));
            Assert.AreEqual(GhostState.Alert, b.State);
            Assert.AreEqual(Victim, b.TargetId);
            Assert.AreEqual(0f, b.Speed);
            b.Tick(0.99f, Target(10f));
            Assert.AreEqual(GhostState.Alert, b.State, "still warning at 0.99 s");
            Assert.AreEqual(GhostEvent.ChaseStarted, b.Tick(0.01f, Target(10f)));
            Assert.AreEqual(GhostState.Chase, b.State);
        }

        [Test]
        public void Chase_Speed_6()
        {
            Assert.AreEqual(6f, Chasing().Speed, 1e-5f);
        }

        [Test]
        public void Chase_InRange_Harmable_Grab()
        {
            var b = Chasing();
            Assert.AreEqual(GhostEvent.Grabbed, b.Tick(0.1f, Target(1.4f)));
            Assert.AreEqual(GhostState.Grab, b.State);
            Assert.AreEqual(0f, b.Speed);
        }

        [Test]
        public void Grab_2s_Killed_ThenCooldown10s_ThenWander()
        {
            var b = Chasing();
            b.Tick(0.1f, Target(1f));
            b.Tick(1.5f, Target(1f));
            Assert.AreEqual(GhostState.Grab, b.State);
            Assert.AreEqual(GhostEvent.Killed, b.Tick(0.5f, Target(1f)));
            Assert.AreEqual(GhostState.Cooldown, b.State);
            Assert.AreEqual(Victim, b.TargetId, "the killed id stays readable on the Killed tick");
            Assert.AreEqual(GhostTuning.CooldownSpeed, b.Speed, 1e-5f);

            Run(b, 9.5f, Sees(Victim));
            Assert.AreEqual(GhostState.Cooldown, b.State, "hunts nobody during cooldown");
            Run(b, 0.5f, Nothing);
            Assert.AreEqual(GhostState.Wander, b.State);
        }

        [Test]
        public void Chase_InRange_NotHarmable_Spared_IgnoredFor8s()
        {
            var b = Chasing();
            Assert.AreEqual(GhostEvent.Spared, b.Tick(0.1f, Target(1f, harmable: false)));
            Assert.AreEqual(GhostState.Wander, b.State);
            Assert.IsTrue(b.IsIgnoring(Victim));
            Run(b, 7.5f, Sees(Victim));
            Assert.AreEqual(GhostState.Wander, b.State, "spared player is ignored");
            Run(b, 0.5f, Nothing);
            Assert.IsFalse(b.IsIgnoring(Victim));
            Assert.AreEqual(GhostEvent.Alerted, b.Tick(0.1f, Sees(Victim)));
        }

        [Test]
        public void Chase_LostSight6s_GaveUp()
        {
            var b = Chasing();
            Run(b, 5.5f, Target(10f, visible: false));
            Assert.AreEqual(GhostState.Chase, b.State);
            Assert.AreEqual(GhostEvent.GaveUp, Run(b, 0.5f, Target(10f, visible: false)));
            Assert.AreEqual(GhostState.Wander, b.State);
            Assert.IsFalse(b.HasTarget);
        }

        [Test]
        public void Chase_SightReturns_ResetsLostTimer()
        {
            var b = Chasing();
            Run(b, 5.5f, Target(10f, visible: false));
            b.Tick(0.5f, Target(10f));
            Run(b, 5.5f, Target(10f, visible: false));
            Assert.AreEqual(GhostState.Chase, b.State);
        }

        [Test]
        public void Chase_TargetInvalid_Wander()
        {
            var b = Chasing();
            Assert.AreEqual(GhostEvent.GaveUp, b.Tick(0.1f, Nothing));
            Assert.AreEqual(GhostState.Wander, b.State);
        }

        [Test]
        public void Alert_TargetInvalid_Wander()
        {
            var b = new GhostBrain();
            b.Tick(0.1f, Sees(Victim));
            b.Tick(0.1f, Nothing);
            Assert.AreEqual(GhostState.Wander, b.State);
        }

        [Test]
        public void Grab_TargetInvalid_Cooldown()
        {
            var b = Chasing();
            b.Tick(0.1f, Target(1f));
            Assert.AreNotEqual(GhostEvent.Killed, b.Tick(0.5f, Nothing));
            Assert.AreEqual(GhostState.Cooldown, b.State);
        }

        [Test]
        public void Lit2s_Flee3s_ThenWander()
        {
            var b = Chasing();
            Run(b, 1.5f, new GhostSenses { Lit = true, TargetValid = true, TargetVisible = true, TargetDistance = 10f, TargetHarmable = true });
            Assert.AreEqual(GhostState.Chase, b.State);
            Assert.AreEqual(GhostEvent.Fled, b.Tick(0.5f, Lit));
            Assert.AreEqual(GhostState.Flee, b.State);
            Assert.AreEqual(GhostTuning.FleeSpeed, b.Speed, 1e-5f);
            Run(b, 2.5f, Nothing);
            Assert.AreEqual(GhostState.Flee, b.State);
            Run(b, 0.5f, Nothing);
            Assert.AreEqual(GhostState.Wander, b.State);
        }

        [Test]
        public void Lit_Interrupted_Decays()
        {
            var b = new GhostBrain();
            Run(b, 1.5f, Lit);
            Run(b, 0.5f, Nothing); // exposure 1.5 -> 0.5
            Run(b, 1f, Lit);       // 1.5
            Assert.AreEqual(GhostState.Wander, b.State);
            Run(b, 0.5f, Lit);
            Assert.AreEqual(GhostState.Flee, b.State);
        }

        [Test]
        public void Flee_ThenImmuneToLight()
        {
            var b = new GhostBrain();
            Run(b, 2f, Lit);
            Run(b, 3f, Lit);
            Assert.AreEqual(GhostState.Wander, b.State);
            Run(b, 7.5f, Lit);
            Assert.AreEqual(GhostState.Wander, b.State, "immune for 8 s after fleeing");
            Run(b, 2.5f, Lit);
            Assert.AreEqual(GhostState.Flee, b.State);
        }

        [Test]
        public void Lit_IgnoredDuringGrab()
        {
            var b = Chasing();
            b.Tick(0.1f, Target(1f));
            var s = Target(1f);
            s.Lit = true;
            Assert.AreEqual(GhostEvent.Killed, Run(b, 2f, s));
        }

        [Test]
        public void OnNoise_Wander_Investigate()
        {
            var b = new GhostBrain();
            Assert.IsTrue(b.OnNoise(10f, 8f), "8 m noise x1.5 Krasue hearing reaches 12 m");
            Assert.AreEqual(GhostState.Investigate, b.State);
            Assert.AreEqual(GhostTuning.InvestigateSpeed, b.Speed, 1e-5f);
            Assert.IsTrue(b.OnNoise(3f, 8f), "a new noise retargets the investigation");
        }

        [Test]
        public void OnNoise_IgnoredWhileChaseCooldownGrabFlee()
        {
            var chase = Chasing();
            Assert.IsFalse(chase.OnNoise(1f, 20f));
            Assert.AreEqual(GhostState.Chase, chase.State);

            chase.Tick(0.1f, Target(1f));
            Assert.IsFalse(chase.OnNoise(1f, 20f));
            Assert.AreEqual(GhostState.Grab, chase.State);

            Run(chase, 2f, Target(1f));
            Assert.IsFalse(chase.OnNoise(1f, 20f));
            Assert.AreEqual(GhostState.Cooldown, chase.State);

            var flee = new GhostBrain();
            Run(flee, 2f, Lit);
            Assert.IsFalse(flee.OnNoise(1f, 20f));
            Assert.AreEqual(GhostState.Flee, flee.State);
        }

        [Test]
        public void OnNoise_TooFar_NotHeard()
        {
            var b = new GhostBrain();
            Assert.IsFalse(b.OnNoise(12.1f, 8f));
            Assert.IsFalse(b.OnNoise(1f, 0f));
            Assert.AreEqual(GhostState.Wander, b.State);
        }

        [Test]
        public void Investigate_Reached_Linger2s_Wander()
        {
            var b = new GhostBrain();
            b.OnNoise(5f, 8f);
            Run(b, 1.5f, new GhostSenses { ReachedPoint = true });
            Assert.AreEqual(GhostState.Investigate, b.State);
            Run(b, 0.5f, new GhostSenses { ReachedPoint = true });
            Assert.AreEqual(GhostState.Wander, b.State);
        }

        [Test]
        public void Investigate_Timeout15s_Wander()
        {
            var b = new GhostBrain();
            b.OnNoise(5f, 8f);
            Run(b, 14.5f, Nothing);
            Assert.AreEqual(GhostState.Investigate, b.State);
            Run(b, 0.5f, Nothing);
            Assert.AreEqual(GhostState.Wander, b.State);
        }

        [Test]
        public void Investigate_SeesPlayer_Alert()
        {
            var b = new GhostBrain();
            b.OnNoise(5f, 8f);
            Assert.AreEqual(GhostEvent.Alerted, b.Tick(0.1f, Sees(Victim)));
        }

        [Test]
        public void SpeedMultiplier_Applies()
        {
            var b = Chasing();
            b.SpeedMultiplier = 1.2f;
            Assert.AreEqual(7.2f, b.Speed, 1e-4f);
        }

        [Test]
        public void Tuning_ChaseBetweenRunAndAdrenalineRun()
        {
            Assert.That(GhostTuning.ChaseSpeed, Is.GreaterThan(MovementModel.RunSpeed));
            Assert.That(GhostTuning.ChaseSpeed, Is.LessThan(MovementModel.RunSpeed * (1f + AdrenalineModel.MinBonus)));
            Assert.That(GhostTuning.WanderSpeed, Is.LessThan(MovementModel.WalkSpeed));
        }
    }

    public class GhostPerceptionTests
    {
        [Test]
        public void CanSee_InsideRangeAndFov()
        {
            Assert.IsTrue(GhostPerception.CanSee(14f, 60f, true));
            Assert.IsFalse(GhostPerception.CanSee(14.1f, 0f, true));
        }

        [Test]
        public void CanSee_Behind_False() => Assert.IsFalse(GhostPerception.CanSee(3f, 61f, true));

        [Test]
        public void CanSee_Blocked_False() => Assert.IsFalse(GhostPerception.CanSee(3f, 0f, false));

        [Test]
        public void IsLit_InCone() => Assert.IsTrue(GhostPerception.IsLit(15f, 12f, true));

        [Test]
        public void IsLit_OutsideCone_False() => Assert.IsFalse(GhostPerception.IsLit(5f, 12.5f, true));

        [Test]
        public void IsLit_TooFar_False()
        {
            Assert.IsFalse(GhostPerception.IsLit(15.1f, 0f, true));
            Assert.IsFalse(GhostPerception.IsLit(5f, 0f, false));
            Assert.IsFalse(GhostPerception.IsLit(float.NaN, 0f, true));
        }

        [Test]
        public void Hears_RadiusTimesMultiplier()
        {
            Assert.IsTrue(GhostPerception.Hears(15f, 10f, 1.5f));
            Assert.IsFalse(GhostPerception.Hears(15.1f, 10f, 1.5f));
            Assert.IsFalse(GhostPerception.Hears(float.NaN, 10f, 1.5f));
        }

        [Test]
        public void AngleDeg_Basics()
        {
            Assert.AreEqual(0f, GhostPerception.AngleDeg(0, 0, 1, 0, 0, 5), 1e-3f);
            Assert.AreEqual(90f, GhostPerception.AngleDeg(0, 0, 1, 1, 0, 0), 1e-3f);
            Assert.AreEqual(180f, GhostPerception.AngleDeg(0, 0, 1, 0, 0, -2), 1e-3f);
        }

        [Test]
        public void AngleDeg_ZeroVector_Returns180()
        {
            Assert.AreEqual(180f, GhostPerception.AngleDeg(0, 0, 0, 0, 0, 1));
            Assert.AreEqual(180f, GhostPerception.AngleDeg(0, 0, 1, float.NaN, 0, 1));
        }
    }

    public class VoiceNoiseTests
    {
        [Test]
        public void FromVoice_Scream_Loud1_Radius20()
        {
            var n = NoiseRules.FromVoice(1f);
            Assert.AreEqual(1f, n.Loudness, 1e-5f);
            Assert.AreEqual(20f, n.Radius, 1e-5f);
        }

        [Test]
        public void FromVoice_Quiet_Silent()
        {
            Assert.AreEqual(0f, NoiseRules.FromVoice(0.04f).Loudness);
            Assert.AreEqual(0.5f, NoiseRules.FromVoice(0.5f).Loudness, 1e-5f);
        }

        [Test]
        public void FromVoice_ClampsAndNaN()
        {
            Assert.AreEqual(1f, NoiseRules.FromVoice(50f).Loudness, 1e-5f);
            Assert.AreEqual(0f, NoiseRules.FromVoice(float.NaN).Loudness);
            Assert.AreEqual(0f, NoiseRules.FromVoice(-1f).Loudness);
        }
    }
}
