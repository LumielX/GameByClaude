using BooTrayal.Core.Fear;
using BooTrayal.Game.Audio;
using BooTrayal.Game.Ghosts;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BooTrayal.Game.Fear
{
    /// <summary>
    /// Owner-only Fear Proximity (GDD 6.2) and Adrenaline input (6.3), from the synced ghost position:
    /// vignette + chromatic aberration, camera shake, cross-faded procedural sound layers, lamp flicker.
    /// </summary>
    public class FearDirector : NetworkBehaviour
    {
        const float VignetteMax = 0.45f;
        const float ShakeMax = 0.08f;
        const float FadePerSecond = 1.5f;

        /// <summary>The local player's current levels (read by FlickerLight).</summary>
        public static FearLevels Levels { get; private set; }
        public static Vector3 LocalPosition { get; private set; }
        public static bool HasLocal { get; private set; }

        PlayerMotor _motor;
        PlayerLook _look;
        Transform _head;
        GameObject _volumeGo;
        Vignette _vignette;
        ChromaticAberration _chromatic;
        AudioSource _wind, _heart, _drone;
        string _band = "none";
        AdrenalineState _adrenaline = AdrenalineState.Normal;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _look = GetComponent<PlayerLook>();
            _head = transform.Find("HeadPivot");
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _volumeGo = new GameObject("FearVolume");
            var volume = _volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.value = 0f;
            _vignette.smoothness.value = 0.6f;
            _chromatic = profile.Add<ChromaticAberration>(true);
            _chromatic.intensity.value = 0f;
            volume.sharedProfile = profile;

            _wind = Layer("Wind", ProceduralClips.Wind());
            _heart = Layer("Heartbeat", ProceduralClips.Heartbeat());
            _drone = Layer("Drone", ProceduralClips.Drone());
            HasLocal = true;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            if (_volumeGo != null) Destroy(_volumeGo);
            Levels = default;
            HasLocal = false;
        }

        AudioSource Layer(string name, AudioClip clip)
        {
            var go = new GameObject("Fear" + name);
            go.transform.SetParent(_head, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = 0f;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
            return src;
        }

        void Update()
        {
            if (!IsSpawned) return;
            float d = GhostRegistry.NearestDistance(_head.position);
            _motor.GhostDistance = d;
            var levels = FearModel.Evaluate(d);
            Levels = levels;
            LocalPosition = transform.position;

            _vignette.intensity.value = VignetteMax * levels.Vignette;
            _chromatic.intensity.value = levels.Chromatic;
            _look.ShakeOffset = Random.insideUnitSphere * (ShakeMax * levels.Shake);
            Fade(_wind, 0.6f * levels.Whisper);
            Fade(_heart, levels.Heartbeat);
            Fade(_drone, 0.8f * levels.Shake);

            if (!Application.isBatchMode) return;
            var band = d <= FearModel.NearRadius ? "near" : d <= FearModel.MidRadius ? "mid" : d <= FearModel.FarRadius ? "far" : "none";
            if (band != _band)
            {
                _band = band;
                Debug.Log($"FEAR band={band} d={d:0.0}");
            }
            var adr = _motor.Adrenaline.State;
            if (adr != _adrenaline)
            {
                _adrenaline = adr;
                Debug.Log($"PLAYER_ADRENALINE state={adr} mult={_motor.Adrenaline.SpeedMultiplier:0.00}");
            }
        }

        static void Fade(AudioSource src, float target)
        {
            src.volume = Mathf.MoveTowards(src.volume, target, FadePerSecond * Time.deltaTime);
        }
    }
}
