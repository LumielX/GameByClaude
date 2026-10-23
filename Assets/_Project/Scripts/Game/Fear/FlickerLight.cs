using UnityEngine;

namespace BooTrayal.Game.Fear
{
    /// <summary>A room lamp that flickers when the ghost is near the local player (GDD 6.2, ≤ 15 m).</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        const float Range = 15f;

        public float BaseIntensity = 2f;

        Light _light;
        float _seed;

        void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.value * 100f;
        }

        void Update()
        {
            float amount = FearDirector.HasLocal && Vector3.Distance(transform.position, FearDirector.LocalPosition) <= Range
                ? FearDirector.Levels.Flicker
                : 0f;
            if (amount <= 0f)
            {
                _light.intensity = BaseIntensity;
                return;
            }
            // Mostly on, with sudden dips that get deeper and more frequent as fear rises.
            float n = Mathf.PerlinNoise(_seed, Time.time * 9f);
            float dip = n < 0.25f + 0.35f * amount ? amount * 0.85f : 0f;
            _light.intensity = BaseIntensity * (1f - dip);
        }
    }
}
