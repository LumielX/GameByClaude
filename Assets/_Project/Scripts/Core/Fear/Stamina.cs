using System;

namespace BooTrayal.Core.Fear
{
    public sealed class Stamina
    {
        public const float Max = 100f;
        public const float RunDrainPerSec = 20f;
        public const float RegenPerSec = 15f;
        public const float RegenDelay = 1f;
        /// <summary>After running dry, stamina must refill this far before running again.</summary>
        public const float RecoverThreshold = 25f;

        float _sinceRunInput = float.MaxValue;
        bool _exhausted;

        public float Value { get; private set; } = Max;
        public bool CanRun => !_exhausted && Value > 0f;

        public void Tick(float dt, bool running, float drainMultiplier)
        {
            // Holding sprint while exhausted does not block recovery.
            if (running && CanRun)
            {
                _sinceRunInput = 0f;
                Value = Math.Max(0f, Value - RunDrainPerSec * drainMultiplier * dt);
                if (Value <= 0f) _exhausted = true;
                return;
            }

            float before = _sinceRunInput;
            _sinceRunInput = before >= float.MaxValue - dt ? float.MaxValue : before + dt;
            float regenTime = Math.Max(0f, Math.Min(dt, _sinceRunInput - RegenDelay));
            Value = Math.Min(Max, Value + RegenPerSec * regenTime);
            if (_exhausted && Value >= RecoverThreshold) _exhausted = false;
        }
    }
}
