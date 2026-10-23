using System;
using System.Collections.Generic;

namespace BooTrayal.Core.Ghosts
{
    /// <summary>
    /// Server-side ghost state machine (GDD 6.1). The game layer feeds senses each tick and moves the ghost at
    /// <see cref="Speed"/> toward whatever the state implies (waypoint, noise point, target, away from the light).
    /// </summary>
    public sealed class GhostBrain
    {
        const float Epsilon = 1e-4f;

        readonly Dictionary<ulong, float> _ignore = new Dictionary<ulong, float>();
        readonly List<ulong> _expired = new List<ulong>();
        float _exposure;
        float _lightImmunity;
        float _lostSight;
        float _linger = -1f;

        public GhostState State { get; private set; } = GhostState.Wander;
        public ulong TargetId { get; private set; }
        public bool HasTarget { get; private set; }
        /// <summary>Seconds spent in the current state.</summary>
        public float StateTime { get; private set; }
        /// <summary>Exorcise / rage effects (GDD 7.4) and modifiers.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float HearingMultiplier { get; set; } = GhostTuning.KrasueHearing;

        public float Speed => BaseSpeed(State) * SpeedMultiplier;

        static float BaseSpeed(GhostState s)
        {
            switch (s)
            {
                case GhostState.Wander: return GhostTuning.WanderSpeed;
                case GhostState.Investigate: return GhostTuning.InvestigateSpeed;
                case GhostState.Chase: return GhostTuning.ChaseSpeed;
                case GhostState.Cooldown: return GhostTuning.CooldownSpeed;
                case GhostState.Flee: return GhostTuning.FleeSpeed;
                default: return 0f; // Alert, Grab: the ghost stops
            }
        }

        public bool IsIgnoring(ulong id) => _ignore.ContainsKey(id);

        /// <summary>A noise <paramref name="distance"/> metres away with hearing <paramref name="radius"/>. True = go investigate it.</summary>
        public bool OnNoise(float distance, float radius)
        {
            if (State != GhostState.Wander && State != GhostState.Investigate) return false;
            if (!GhostPerception.Hears(distance, radius, HearingMultiplier)) return false;
            Enter(GhostState.Investigate);
            return true;
        }

        public GhostEvent Tick(float dt, in GhostSenses s)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            StateTime += dt;
            TickIgnores(dt);
            if (_lightImmunity > 0f) _lightImmunity = Math.Max(0f, _lightImmunity - dt);

            if (State != GhostState.Grab && State != GhostState.Flee && _lightImmunity <= 0f)
            {
                _exposure = s.Lit ? _exposure + dt : Math.Max(0f, _exposure - GhostTuning.LightDecayRate * dt);
                if (_exposure >= GhostTuning.FlashlightScareSeconds - Epsilon)
                {
                    Enter(GhostState.Flee);
                    _exposure = 0f;
                    return GhostEvent.Fled;
                }
            }

            switch (State)
            {
                case GhostState.Wander:
                    return TryAlert(s);

                case GhostState.Investigate:
                {
                    var e = TryAlert(s);
                    if (e != GhostEvent.None) return e;
                    if (s.ReachedPoint || _linger >= 0f) _linger = Math.Max(0f, _linger) + dt;
                    if (_linger >= GhostTuning.LingerSeconds - Epsilon || StateTime >= GhostTuning.InvestigateTimeout - Epsilon)
                        Enter(GhostState.Wander);
                    return GhostEvent.None;
                }

                case GhostState.Alert:
                    if (!s.TargetValid) { Enter(GhostState.Wander); return GhostEvent.GaveUp; }
                    if (StateTime < GhostTuning.AlertSeconds - Epsilon) return GhostEvent.None;
                    Enter(GhostState.Chase, TargetId);
                    return GhostEvent.ChaseStarted;

                case GhostState.Chase:
                    if (!s.TargetValid) { Enter(GhostState.Wander); return GhostEvent.GaveUp; }
                    if (s.TargetVisible && s.TargetDistance <= GhostTuning.GrabRange)
                    {
                        if (s.TargetHarmable)
                        {
                            Enter(GhostState.Grab, TargetId);
                            return GhostEvent.Grabbed;
                        }
                        _ignore[TargetId] = GhostTuning.SpareSeconds;
                        Enter(GhostState.Wander);
                        return GhostEvent.Spared;
                    }
                    _lostSight = s.TargetVisible ? 0f : _lostSight + dt;
                    if (_lostSight >= GhostTuning.LoseSightSeconds - Epsilon) { Enter(GhostState.Wander); return GhostEvent.GaveUp; }
                    return GhostEvent.None;

                case GhostState.Grab:
                {
                    if (!s.TargetValid) { Enter(GhostState.Cooldown); return GhostEvent.None; }
                    if (StateTime < GhostTuning.GrabSeconds - Epsilon) return GhostEvent.None;
                    var victim = TargetId;
                    Enter(GhostState.Cooldown);
                    TargetId = victim; // readable by the game layer on the Killed tick
                    return GhostEvent.Killed;
                }

                case GhostState.Cooldown:
                    if (StateTime >= GhostTuning.CooldownSeconds - Epsilon) Enter(GhostState.Wander);
                    return GhostEvent.None;

                case GhostState.Flee:
                    if (StateTime >= GhostTuning.FleeSeconds - Epsilon)
                    {
                        _lightImmunity = GhostTuning.LightImmunitySeconds;
                        Enter(GhostState.Wander);
                    }
                    return GhostEvent.None;
            }
            return GhostEvent.None;
        }

        GhostEvent TryAlert(in GhostSenses s)
        {
            if (!s.SeesPlayer || IsIgnoring(s.SeenId)) return GhostEvent.None;
            Enter(GhostState.Alert, s.SeenId);
            return GhostEvent.Alerted;
        }

        void Enter(GhostState state, ulong? target = null)
        {
            State = state;
            StateTime = 0f;
            _lostSight = 0f;
            _linger = -1f;
            HasTarget = target.HasValue;
            TargetId = target ?? 0;
        }

        void TickIgnores(float dt)
        {
            if (_ignore.Count == 0) return;
            _expired.Clear();
            foreach (var id in new List<ulong>(_ignore.Keys))
            {
                float left = _ignore[id] - dt;
                if (left <= Epsilon) _expired.Add(id);
                else _ignore[id] = left;
            }
            foreach (var id in _expired) _ignore.Remove(id);
        }
    }
}
