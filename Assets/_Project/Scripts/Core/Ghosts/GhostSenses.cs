namespace BooTrayal.Core.Ghosts
{
    /// <summary>What the ghost perceives this tick, gathered by the game layer (raycasts, distances).</summary>
    public struct GhostSenses
    {
        /// <summary>Nearest visible living player the brain is not ignoring.</summary>
        public bool SeesPlayer;
        public ulong SeenId;

        /// <summary>The current target is still alive and connected.</summary>
        public bool TargetValid;
        public bool TargetVisible;
        public float TargetDistance;
        /// <summary>False for an unexposed Spy (GDD 6.1).</summary>
        public bool TargetHarmable;

        /// <summary>Arrived at the investigate point.</summary>
        public bool ReachedPoint;
        /// <summary>A flashlight beam is on the ghost's face.</summary>
        public bool Lit;
    }
}
