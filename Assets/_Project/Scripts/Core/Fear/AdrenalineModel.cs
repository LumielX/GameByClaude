using System;

namespace BooTrayal.Core.Fear
{
    public enum AdrenalineState { Normal, Adrenaline, Winded }

    /// <summary>GDD 6.3: ghost within 15 m = faster (+25%..+40%), then winded (-20% for 4 s) once it is gone.</summary>
    public sealed class AdrenalineModel
    {
        public const float TriggerRadius = 15f;
        public const float MaxBonusRadius = 5f;
        public const float MinBonus = 0.25f;
        public const float MaxBonus = 0.40f;
        public const float DrainMultiplier = 1.5f;
        public const float SafeRadius = 25f;
        public const float SafeSecondsToWind = 3f;
        public const float WindedSeconds = 4f;
        public const float WindedSpeed = 0.8f;
        const float Epsilon = 1e-4f;

        float _farTime;
        float _windedTime;

        public AdrenalineState State { get; private set; } = AdrenalineState.Normal;
        public float SpeedMultiplier { get; private set; } = 1f;
        public float StaminaDrainMultiplier { get; private set; } = 1f;

        public void Tick(float dt, float ghostDistance)
        {
            float d = float.IsNaN(ghostDistance) || ghostDistance < 0f ? float.PositiveInfinity : ghostDistance;

            if (d <= TriggerRadius)
            {
                State = AdrenalineState.Adrenaline;
                _farTime = 0f;
                float t = Math.Max(0f, Math.Min(1f, (TriggerRadius - d) / (TriggerRadius - MaxBonusRadius)));
                SpeedMultiplier = 1f + MinBonus + (MaxBonus - MinBonus) * t;
                StaminaDrainMultiplier = DrainMultiplier;
                return;
            }

            switch (State)
            {
                case AdrenalineState.Adrenaline:
                    SpeedMultiplier = 1f;
                    StaminaDrainMultiplier = DrainMultiplier;
                    _farTime = d > SafeRadius ? _farTime + dt : 0f;
                    if (_farTime >= SafeSecondsToWind - Epsilon)
                    {
                        State = AdrenalineState.Winded;
                        _windedTime = 0f;
                        SpeedMultiplier = WindedSpeed;
                        StaminaDrainMultiplier = 1f;
                    }
                    break;
                case AdrenalineState.Winded:
                    _windedTime += dt;
                    if (_windedTime >= WindedSeconds - Epsilon)
                    {
                        State = AdrenalineState.Normal;
                        SpeedMultiplier = 1f;
                    }
                    break;
                default:
                    SpeedMultiplier = 1f;
                    StaminaDrainMultiplier = 1f;
                    break;
            }
        }
    }
}
