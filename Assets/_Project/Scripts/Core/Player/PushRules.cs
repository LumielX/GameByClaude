namespace BooTrayal.Core.Player
{
    public readonly struct PushOutcome
    {
        public bool Allowed { get; }
        public bool Knockdown { get; }
        /// <summary>"" | "self" | "range" | "cooldown" | "pusher_down" | "target_down"</summary>
        public string Reason { get; }

        public PushOutcome(bool allowed, bool knockdown, string reason)
        {
            Allowed = allowed;
            Knockdown = knockdown;
            Reason = reason;
        }

        public static PushOutcome Deny(string reason) => new PushOutcome(false, false, reason);
    }

    /// <summary>Pushing/slapping friends (GDD 1.7): a running push knocks them down.</summary>
    public static class PushRules
    {
        public const float Range = 2.5f;
        public const float Cooldown = 1f;
        public const float KnockbackSpeed = 4.5f;
        public const float KnockdownSeconds = 1.5f;
        public const float KnockdownSpeedRatio = 0.8f;

        public static PushOutcome Evaluate(ulong pusher, ulong target, float distance, double now, double lastPush,
            float pusherSpeed, bool pusherDown, bool targetDown)
        {
            if (pusher == target) return PushOutcome.Deny("self");
            if (pusherDown) return PushOutcome.Deny("pusher_down");
            if (targetDown) return PushOutcome.Deny("target_down");
            if (float.IsNaN(distance) || distance > Range) return PushOutcome.Deny("range");
            if (now - lastPush < Cooldown) return PushOutcome.Deny("cooldown");
            bool knockdown = pusherSpeed >= MovementModel.RunSpeed * KnockdownSpeedRatio;
            return new PushOutcome(true, knockdown, "");
        }
    }
}
