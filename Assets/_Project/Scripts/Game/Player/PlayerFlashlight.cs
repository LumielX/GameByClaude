using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>F toggles a spot light on the head; everyone sees it.</summary>
    public class PlayerFlashlight : NetworkBehaviour
    {
        public readonly NetworkVariable<bool> On =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Light _light;
        PlayerLife _life;

        void Awake()
        {
            _light = GetComponentInChildren<Light>(true);
            _life = GetComponent<PlayerLife>();
        }

        public override void OnNetworkSpawn()
        {
            On.OnValueChanged += (_, on) => Apply(on);
            Apply(On.Value);
        }

        void Update()
        {
            if (IsSpawned && IsOwner && (_life == null || _life.IsAlive) && PlayerInputSource.Get(gameObject).FlashlightPressed)
                On.Value = !On.Value;
        }

        /// <summary>The beam shines only while switched on and alive.</summary>
        public bool Shining => On.Value && (_life == null || _life.IsAlive);

        public void Refresh() => Apply(On.Value);

        void Apply(bool on)
        {
            if (_light != null) _light.enabled = on && (_life == null || _life.IsAlive);
        }
    }
}
