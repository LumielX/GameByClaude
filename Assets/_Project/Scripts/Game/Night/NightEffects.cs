using System.Collections;
using System.Collections.Generic;
using BooTrayal.Game.Ghosts;
using BooTrayal.Game.Player;
using BooTrayal.Game.World;
using UnityEngine;

namespace BooTrayal.Game.Night
{
    /// <summary>Client-side visuals for Spy powers: the cold-breath clue (GDD 7.3) and a zone blackout (7.2).</summary>
    public static class NightEffects
    {
        static Material _breath;

        /// <summary>A pale puff in front of the Spy's mouth that swells and fades.</summary>
        public static void ShowColdBreath(Transform player, float seconds)
        {
            if (Application.isBatchMode) return;
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(puff.GetComponent<Collider>());
            puff.name = "ColdBreath";
            var head = player.Find("HeadPivot");
            puff.transform.SetParent(head != null ? head : player, false);
            puff.transform.localPosition = new Vector3(0f, -0.08f, 0.38f);
            if (_breath == null)
            {
                _breath = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _breath.SetColor("_BaseColor", new Color(0.85f, 0.93f, 1f));
            }
            puff.GetComponent<Renderer>().sharedMaterial = _breath;
            puff.AddComponent<Puff>().Seconds = seconds;
        }

        /// <summary>Turns off every scene lamp inside the zone for a while (player flashlights and ghosts keep glowing).</summary>
        public static IEnumerator Blackout(NightZone zone, float seconds)
        {
            var off = new List<Light>();
            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (!light.enabled || light.type == LightType.Directional || !zone.Contains(light.transform.position)) continue;
                if (light.GetComponentInParent<PlayerMotor>() != null || light.GetComponentInParent<GhostController>() != null) continue;
                light.enabled = false;
                off.Add(light);
            }
            yield return new WaitForSeconds(seconds);
            foreach (var light in off) if (light != null) light.enabled = true;
        }

        class Puff : MonoBehaviour
        {
            public float Seconds = 3f;
            float _t;

            void Update()
            {
                _t += Time.deltaTime;
                float k = _t / Seconds;
                transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.22f, Mathf.Sqrt(Mathf.Clamp01(k))) * (1f + 0.15f * Mathf.Sin(_t * 9f));
                if (k >= 1f) Destroy(gameObject);
            }
        }
    }
}
