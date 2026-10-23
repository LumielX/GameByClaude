using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BooTrayal.Game.Player
{
    /// <summary>Yaw on the body (synced by NetworkTransform), pitch on HeadPivot (synced here), camera on the owner only.</summary>
    public class PlayerLook : NetworkBehaviour
    {
        public const float MaxPitch = 85f;
        const float PitchSyncThreshold = 1f;
        const float KnockdownRoll = 25f;

        readonly NetworkVariable<float> _pitch =
            new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Transform _head;
        PlayerMotor _motor;
        PlayerLife _life;
        float _yaw;
        float _localPitch;
        float _roll;

        public Camera Camera { get; private set; }
        /// <summary>Camera shake offset (local metres), set by FearDirector each frame.</summary>
        public Vector3 ShakeOffset { get; set; }

        void Awake()
        {
            _head = transform.Find("HeadPivot");
            _motor = GetComponent<PlayerMotor>();
            _life = GetComponent<PlayerLife>();
        }

        public override void OnNetworkSpawn()
        {
            _yaw = transform.eulerAngles.y;
            if (!IsOwner) return;
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(_head, false);
            Camera = camGo.AddComponent<Camera>();
            Camera.fieldOfView = 75f;
            Camera.nearClipPlane = 0.05f;
            Camera.backgroundColor = new Color(0.02f, 0.01f, 0.04f);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.tag = "MainCamera";
        }

        /// <summary>Owner-side pitch (test hook for smoke bots aiming the flashlight).</summary>
        public void SetPitch(float pitch) => _localPitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);

        public void SetYaw(float yaw)
        {
            _yaw = yaw;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        void Update()
        {
            if (!IsSpawned || _head == null) return;
            if (IsOwner)
            {
                var look = PlayerInputSource.Get(gameObject).Look;
                bool down = _motor != null && _motor.IsKnockedDown.Value;
                if (!down && (_life == null || _life.CanMove))
                {
                    _yaw += look.x;
                    _localPitch = Mathf.Clamp(_localPitch - look.y, -MaxPitch, MaxPitch);
                }
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                _roll = Mathf.MoveTowards(_roll, down ? KnockdownRoll : 0f, 120f * Time.deltaTime);
                _head.localRotation = Quaternion.Euler(_localPitch, 0f, _roll);
                if (Mathf.Abs(_localPitch - _pitch.Value) > PitchSyncThreshold) _pitch.Value = _localPitch;
                if (Camera != null) Camera.transform.localPosition = ShakeOffset;
            }
            else
            {
                var target = Quaternion.Euler(_pitch.Value, 0f, 0f);
                _head.localRotation = Quaternion.Slerp(_head.localRotation, target, 12f * Time.deltaTime);
            }
        }
    }
}
