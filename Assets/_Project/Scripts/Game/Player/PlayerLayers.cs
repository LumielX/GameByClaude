using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>Mini ghosts collide with walls but not with players or each other (GDD 5.1).</summary>
    public static class PlayerLayers
    {
        public const int Player = 8;
        public const int MiniGhost = 9;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            Physics.IgnoreLayerCollision(Player, MiniGhost, true);
            Physics.IgnoreLayerCollision(MiniGhost, MiniGhost, true);
        }
    }
}
