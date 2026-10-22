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

        void Awake()
        {
            _light = GetComponentInChildren<Light>(true);
        }

        public override void OnNetworkSpawn()
        {
            On.OnValueChanged += (_, on) => Apply(on);
            Apply(On.Value);
        }

        void Update()
        {
            if (IsSpawned && IsOwner && PlayerInputSource.Get(gameObject).FlashlightPressed)
                On.Value = !On.Value;
        }

        void Apply(bool on)
        {
            if (_light != null) _light.enabled = on;
        }
    }
}
