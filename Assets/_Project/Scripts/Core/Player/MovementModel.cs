using System;

namespace BooTrayal.Core.Player
{
    public struct MoveInput
    {
        public float X;
        public float Y;
        public bool Run;
        public bool Jump;
        public bool Crouch;
    }

    /// <summary>First-person movement numbers (GDD 5). Speeds in m/s.</summary>
    public static class MovementModel
    {
        public const float WalkSpeed = 3.2f;
        public const float RunSpeed = 5.6f;
        public const float CrouchSpeed = 1.6f;
        public const float JumpVelocity = 4.8f;
        public const float Gravity = -18f;
        /// <summary>Keeps the CharacterController pressed onto slopes and steps.</summary>
        public const float GroundSnapVelocity = -2f;

        public static float InputMagnitude(float x, float y)
        {
            if (!IsFinite(x) || !IsFinite(y)) return 0f;
            return Math.Min(1f, (float)Math.Sqrt(x * x + y * y));
        }

        public static bool IsRunning(MoveInput input, bool canRun, bool knockedDown)
        {
            return input.Run && canRun && !input.Crouch && !knockedDown && InputMagnitude(input.X, input.Y) > 0f;
        }

        /// <summary>Horizontal speed; <paramref name="speedMultiplier"/> is the adrenaline/winded factor.</summary>
        public static float TargetSpeed(MoveInput input, bool canRun, float speedMultiplier, CarryLoad load, bool knockedDown)
        {
            float magnitude = InputMagnitude(input.X, input.Y);
            if (knockedDown || magnitude <= 0f) return 0f;
            float baseSpeed = input.Crouch ? CrouchSpeed : IsRunning(input, canRun, false) ? RunSpeed : WalkSpeed;
            float multiplier = IsFinite(speedMultiplier) && speedMultiplier > 0f ? speedMultiplier : 1f;
            return baseSpeed * magnitude * multiplier * CarryRules.SpeedFactor(load);
        }

        public static float NextVerticalVelocity(float vy, bool grounded, bool jumpPressed, bool crouching, CarryLoad load, bool knockedDown, float dt)
        {
            if (grounded && vy <= 0f)
            {
                bool canJump = jumpPressed && !crouching && !knockedDown && CarryRules.CanJump(load);
                return canJump ? JumpVelocity : GroundSnapVelocity;
            }
            return vy + Gravity * dt;
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
