namespace BooTrayal.Core.Ghosts
{
    /// <summary>Krasue numbers. GDD 6.1 fixes the timings; speeds and ranges are tuning calls (playtest later).</summary>
    public static class GhostTuning
    {
        // GDD 6.1
        public const float AlertSeconds = 1f;
        public const float GrabSeconds = 2f;
        public const float CooldownSeconds = 10f;
        public const float LoseSightSeconds = 6f;
        public const float FlashlightScareSeconds = 2f;

        // Speeds (m/s). Chase beats a plain run (5.6) but loses to run + adrenaline (7.0+).
        public const float WanderSpeed = 2.2f;
        public const float InvestigateSpeed = 3.6f;
        public const float ChaseSpeed = 6.0f;
        public const float CooldownSpeed = 1.6f;
        public const float FleeSpeed = 5.0f;

        // Senses
        public const float SightRange = 14f;
        public const float FieldOfView = 120f;
        public const float GrabRange = 1.4f;
        public const float KrasueHearing = 1.5f;
        public const float FlashlightReach = 15f;
        /// <summary>Half-angle of the beam that counts as "in the face".</summary>
        public const float FlashlightCone = 12f;

        // Behaviour
        public const float FleeSeconds = 3f;
        public const float LightImmunitySeconds = 8f;
        public const float LightDecayRate = 2f;
        public const float InvestigateTimeout = 15f;
        public const float LingerSeconds = 2f;
        public const float SpareSeconds = 8f;
    }
}
