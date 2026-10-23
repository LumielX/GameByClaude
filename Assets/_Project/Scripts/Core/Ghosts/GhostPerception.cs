using System;

namespace BooTrayal.Core.Ghosts
{
    /// <summary>Pure geometry checks for sight, flashlight and hearing. Raycasts happen in the game layer.</summary>
    public static class GhostPerception
    {
        public static bool CanSee(float distance, float angleFromForwardDeg, bool clear) =>
            clear && distance <= GhostTuning.SightRange && angleFromForwardDeg <= GhostTuning.FieldOfView * 0.5f;

        public static bool IsLit(float distance, float angleFromBeamDeg, bool clear) =>
            clear && distance <= GhostTuning.FlashlightReach && angleFromBeamDeg <= GhostTuning.FlashlightCone;

        public static bool Hears(float distance, float radius, float hearingMultiplier) =>
            radius > 0f && distance <= radius * hearingMultiplier;

        /// <summary>Angle between two vectors in degrees; a zero or NaN vector gives 180 (never "in front").</summary>
        public static float AngleDeg(float ax, float ay, float az, float bx, float by, float bz)
        {
            double la = Math.Sqrt(ax * ax + ay * ay + az * az);
            double lb = Math.Sqrt(bx * bx + by * by + bz * bz);
            if (!(la > 1e-6) || !(lb > 1e-6)) return 180f;
            double cos = (ax * bx + ay * by + az * bz) / (la * lb);
            cos = Math.Max(-1.0, Math.Min(1.0, cos));
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }
    }
}
