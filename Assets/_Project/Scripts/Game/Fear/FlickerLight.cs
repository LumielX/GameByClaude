using UnityEngine;

namespace BooTrayal.Game.Fear
{
    /// <summary>A room lamp that flickers when the ghost is near the local player (GDD 6.2, ≤ 15 m).</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float BaseIntensity = 2f;
    }
}
