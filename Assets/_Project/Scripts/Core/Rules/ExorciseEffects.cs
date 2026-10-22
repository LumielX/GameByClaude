namespace BooTrayal.Core.Rules
{
    public readonly struct GhostModifiers
    {
        public float SpeedMul { get; }
        public float HearingMul { get; }
        /// <summary>Duration; +infinity means the rest of the night.</summary>
        public double Seconds { get; }

        public GhostModifiers(float speedMul, float hearingMul, double seconds)
        {
            SpeedMul = speedMul;
            HearingMul = hearingMul;
            Seconds = seconds;
        }
    }

    /// <summary>Results of a meeting ejection (GDD 7.4).</summary>
    public static class ExorciseEffects
    {
        public static readonly GhostModifiers Correct = new GhostModifiers(0.8f, 1f, double.PositiveInfinity);
        public static readonly GhostModifiers Wrong = new GhostModifiers(1.2f, 1.5f, 30.0);
        public const double CorrectBonusSeconds = 60.0;
    }
}
