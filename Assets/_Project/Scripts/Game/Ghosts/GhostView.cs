using BooTrayal.Core.Ghosts;
using BooTrayal.Game.Audio;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Ghosts
{
    /// <summary>Client look of the Krasue: bobbing head, glowing entrails that flare when hunting, a screech on the 1 s warning.</summary>
    public class GhostView : NetworkBehaviour
    {
        const float BobHeight = 0.08f;
        const float BobSpeed = 2.2f;

        GhostController _ghost;
        Transform _visual;
        Light _glow;
        AudioSource _voice;
        float _baseGlow;

        void Awake()
        {
            _ghost = GetComponent<GhostController>();
            _visual = transform.Find("Visual");
            _glow = GetComponentInChildren<Light>(true);
            _voice = GetComponent<AudioSource>();
            if (_glow != null) _baseGlow = _glow.intensity;
        }

        public override void OnNetworkSpawn() => _ghost.StateNet.OnValueChanged += OnState;

        public override void OnNetworkDespawn() => _ghost.StateNet.OnValueChanged -= OnState;

        void OnState(GhostState previous, GhostState state)
        {
            if (state == GhostState.Alert && _voice != null) _voice.PlayOneShot(ProceduralClips.Screech());
        }

        void Update()
        {
            if (_visual != null)
                _visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * BobSpeed) * BobHeight, 0f);
            if (_glow == null) return;
            var state = _ghost.StateNet.Value;
            bool hunting = state == GhostState.Alert || state == GhostState.Chase || state == GhostState.Grab;
            float target = hunting ? _baseGlow * (2f + 0.6f * Mathf.Sin(Time.time * 25f)) : _baseGlow;
            _glow.intensity = Mathf.MoveTowards(_glow.intensity, target, 20f * Time.deltaTime);
        }
    }
}
