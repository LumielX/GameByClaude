using System;

namespace BooTrayal.Core.Player
{
    public enum CarryLoad { None, Light, HeavyShared, HeavyAlone }

    /// <summary>Picking up, carrying and throwing (GDD 5): heavy items need two people to lift.</summary>
    public static class CarryRules
    {
        public const float GrabRange = 2.2f;
        public const float MaxThrowSpeed = 12f;

        public static CarryLoad LoadFor(bool holding, bool heavy, int holders)
        {
            if (!holding) return CarryLoad.None;
            if (!heavy) return CarryLoad.Light;
            return holders >= 2 ? CarryLoad.HeavyShared : CarryLoad.HeavyAlone;
        }

        public static float SpeedFactor(CarryLoad load)
        {
            switch (load)
            {
                case CarryLoad.Light: return 0.9f;
                case CarryLoad.HeavyShared: return 0.65f;
                case CarryLoad.HeavyAlone: return 0.35f;
                default: return 1f;
            }
        }

        public static bool CanJump(CarryLoad load) => load == CarryLoad.None || load == CarryLoad.Light;

        public static int MaxHolders(bool heavy) => heavy ? 2 : 1;

        public static bool CanGrab(bool heavy, int currentHolders, bool alreadyHolding, float distance)
        {
            if (alreadyHolding || float.IsNaN(distance) || distance > GrabRange) return false;
            return currentHolders < MaxHolders(heavy);
        }

        public static bool IsLifted(bool heavy, int holders) => holders >= (heavy ? 2 : 1);

        public static float ClampThrow(float speed)
        {
            if (float.IsNaN(speed) || speed <= 0f) return 0f;
            return Math.Min(MaxThrowSpeed, speed);
        }
    }
}
