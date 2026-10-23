using UnityEngine;
using UnityEngine.Rendering;
using BooTrayal.Game.Networking;
using Unity.Collections;
using Unity.Netcode;

namespace BooTrayal.Game.Player
{
    /// <summary>Placeholder body: tinted per player, hidden (shadow only) for its owner, tips over when knocked down.</summary>
    public class PlayerAvatar : NetworkBehaviour
    {
        public static readonly Color[] Palette =
        {
            new Color(0.95f, 0.70f, 0.24f), new Color(0.48f, 0.25f, 0.89f), new Color(0.22f, 1.00f, 0.53f),
            new Color(1.00f, 0.31f, 0.64f), new Color(0.27f, 0.71f, 1.00f), new Color(1.00f, 0.45f, 0.20f),
            new Color(0.93f, 0.93f, 0.97f), new Color(0.60f, 0.40f, 0.25f)
        };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The name typed in the menu, set by the owner (shown in meetings and summaries).</summary>
        public readonly NetworkVariable<FixedString128Bytes> DisplayName =
            new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public string Name => DisplayName.Value.Length > 0 ? DisplayName.Value.ToString() : $"Player {OwnerClientId}";

        /// <summary>Display name of a connected player, or "Player N".</summary>
        public static string NameOf(ulong clientId)
        {
            foreach (var a in FindObjectsByType<PlayerAvatar>())
                if (a.OwnerClientId == clientId) return a.Name;
            return $"Player {clientId}";
        }

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int CarryId = Animator.StringToHash("Carry");
        static readonly int FallId = Animator.StringToHash("Fall");

        Transform _visual;
        PlayerMotor _motor;
        PlayerHands _hands;
        Animator _animator;
        Vector3 _lastPos;
        float _speed;

        void Awake()
        {
            _visual = transform.Find("Visual");
            _motor = GetComponent<PlayerMotor>();
            _hands = GetComponent<PlayerHands>();
            _animator = _visual != null ? _visual.GetComponentInChildren<Animator>() : null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner && ConnectionService.Instance != null && !string.IsNullOrEmpty(ConnectionService.Instance.PlayerDisplayName))
                DisplayName.Value = new FixedString128Bytes(ConnectionService.Instance.PlayerDisplayName); // names are capped at 96 UTF-8 bytes
            var mini = transform.Find("MiniGhostVisual");
            if (IsOwner && mini != null)
                foreach (var r in mini.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            if (_visual == null) return;
            var color = Palette[(int)(OwnerClientId % (ulong)Palette.Length)];
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (var r in _visual.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Body") r.SetPropertyBlock(block);
                // The Blender model: tint only its "Body" material slot (skin, eyes and shoes keep their colours).
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.EndsWith("_Body")) r.SetPropertyBlock(block, i);
                if (IsOwner) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        void Update()
        {
            if (_animator == null || _motor == null || !IsSpawned) return;
            float dt = Time.deltaTime;
            if (IsOwner) _speed = _motor.CurrentSpeed;
            else if (dt > 0f)
            {
                // Remote players: the motor's speed is not networked, so measure the synced transform.
                var d = transform.position - _lastPos;
                d.y = 0f;
                _speed = Mathf.Lerp(_speed, d.magnitude / dt, 0.2f);
            }
            _lastPos = transform.position;
            _animator.SetFloat(SpeedId, _speed);
            _animator.SetBool(CarryId, _hands != null && _hands.HeldItem != null);
            _animator.SetBool(FallId, _motor.IsKnockedDown.Value);
        }
    }
}
