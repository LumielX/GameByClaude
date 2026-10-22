using UnityEngine;
using UnityEngine.Rendering;
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
        const float KnockdownTilt = 80f;

        Transform _visual;
        PlayerMotor _motor;
        float _tilt;

        void Awake()
        {
            _visual = transform.Find("Visual");
            _motor = GetComponent<PlayerMotor>();
        }

        public override void OnNetworkSpawn()
        {
            if (_visual == null) return;
            var color = Palette[(int)(OwnerClientId % (ulong)Palette.Length)];
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (var r in _visual.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Body") r.SetPropertyBlock(block);
                if (IsOwner) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        void Update()
        {
            if (_visual == null || _motor == null) return;
            float target = _motor.IsKnockedDown.Value ? KnockdownTilt : 0f;
            _tilt = Mathf.MoveTowards(_tilt, target, 400f * Time.deltaTime);
            _visual.localRotation = Quaternion.Euler(_tilt, 0f, 0f);
        }
    }
}
