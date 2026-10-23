using BooTrayal.Core.Fear;
using BooTrayal.Core.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>
    /// Owner-simulated first-person movement (owner-authoritative NetworkTransform).
    /// Numbers come from Core MovementModel / Stamina / AdrenalineModel / Knockdown.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        public const float StandHeight = 1.7f;
        public const float CrouchHeight = 1.1f;
        public const float StandEyeHeight = 1.55f;
        public const float CrouchEyeHeight = 0.95f;
        const float KnockbackFriction = 10f;
        /// <summary>Mini ghost float speed up/down (GDD 5.1).</summary>
        public const float MiniGhostLiftSpeed = 2f;

        public readonly NetworkVariable<bool> IsKnockedDown =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> IsRunningNet =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> IsCrouchingNet =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        readonly Knockdown _knockdown = new Knockdown();
        CharacterController _cc;
        PlayerLife _life;
        Transform _head;
        Vector3 _external;
        float _vy;

        public Stamina Stamina { get; } = new Stamina();
        public AdrenalineModel Adrenaline { get; } = new AdrenalineModel();
        /// <summary>Set by PlayerHands when holding something.</summary>
        public CarryLoad Load { get; set; }
        /// <summary>Distance to the nearest ghost; set by the ghost system in M4.</summary>
        public float GhostDistance { get; set; } = float.PositiveInfinity;
        /// <summary>Horizontal speed this frame (owner only; server sees the synced transform).</summary>
        public float CurrentSpeed { get; private set; }
        public bool IsCrouching { get; private set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _life = GetComponent<PlayerLife>();
            _head = transform.Find("HeadPivot");
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                GetComponent<PlayerInputReader>()?.Activate();
                return;
            }
            IsCrouchingNet.OnValueChanged += OnRemoteCrouchChanged;
            if (IsCrouchingNet.Value) SetCrouchShape(true);
        }

        public override void OnNetworkDespawn()
        {
            IsCrouchingNet.OnValueChanged -= OnRemoteCrouchChanged;
        }

        void OnRemoteCrouchChanged(bool previous, bool crouch)
        {
            SetCrouchShape(crouch);
            if (IsServer && Application.isBatchMode)
                Debug.Log($"PLAYER_CROUCH id={OwnerClientId} crouch={crouch} head={_head.localPosition.y:0.00}");
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            var input = PlayerInputSource.Get(gameObject);
            float dt = Time.deltaTime;

            _knockdown.Tick(dt);
            bool down = _knockdown.IsDown;
            if (IsKnockedDown.Value != down) IsKnockedDown.Value = down;
            Adrenaline.Tick(dt, GhostDistance);

            var move = input.Move;
            var mi = new MoveInput { X = move.x, Y = move.y, Run = input.Run, Crouch = input.Crouch, Jump = input.JumpPressed };
            if (_life != null && !_life.CanMove) mi = default; // held by a ghost, or escaped
            bool flying = _life != null && _life.IsMiniGhost;
            if (flying) { mi.Run = false; mi.Crouch = false; mi.Jump = false; }
            bool running = MovementModel.IsRunning(mi, Stamina.CanRun, down);
            float speed = MovementModel.TargetSpeed(mi, Stamina.CanRun, Adrenaline.SpeedMultiplier, Load, down);
            Stamina.Tick(dt, running, Adrenaline.StaminaDrainMultiplier);

            var wish = transform.right * (float.IsNaN(mi.X) ? 0f : mi.X) + transform.forward * (float.IsNaN(mi.Y) ? 0f : mi.Y);
            wish.y = 0f;
            if (wish.sqrMagnitude > 1e-4f) wish.Normalize();
            var horizontal = wish * speed + _external;
            _external = Vector3.MoveTowards(_external, Vector3.zero, KnockbackFriction * dt);

            ApplyCrouch(mi.Crouch && !down);
            if (flying)
            {
                // Mini ghosts float: Space up, Ctrl down, no gravity.
                float lift = (input.JumpHeld ? 1f : 0f) - (input.Crouch ? 1f : 0f);
                _vy = _life.CanMove ? lift * MiniGhostLiftSpeed : 0f;
            }
            else _vy = MovementModel.NextVerticalVelocity(_vy, _cc.isGrounded, mi.Jump, IsCrouching, Load, down, dt);
            _cc.Move((horizontal + Vector3.up * _vy) * dt);

            CurrentSpeed = new Vector2(horizontal.x, horizontal.z).magnitude;
            if (IsRunningNet.Value != running) IsRunningNet.Value = running;
        }

        void ApplyCrouch(bool crouch)
        {
            if (crouch == IsCrouching) return;
            // Stand up only if there is room above the head.
            if (!crouch && Physics.SphereCast(transform.position + Vector3.up * CrouchHeight, _cc.radius * 0.9f, Vector3.up,
                    out _, StandHeight - CrouchHeight, ~0, QueryTriggerInteraction.Ignore))
                return;
            SetCrouchShape(crouch);
            IsCrouchingNet.Value = crouch;
        }

        /// <summary>Applied on every peer so the server's hold point and others' view match the owner.</summary>
        void SetCrouchShape(bool crouch)
        {
            IsCrouching = crouch;
            float h = crouch ? CrouchHeight : StandHeight;
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
            if (_head != null) _head.localPosition = new Vector3(0f, crouch ? CrouchEyeHeight : StandEyeHeight, 0f);
        }

        /// <summary>Server asks the owner to apply a push (see PlayerPush).</summary>
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void KnockbackRpc(Vector3 velocity, bool knockdown)
        {
            velocity.y = 0f;
            _external = Vector3.ClampMagnitude(velocity, PushRules.KnockbackSpeed * 2f);
            if (knockdown) _knockdown.Start(PushRules.KnockdownSeconds);
        }

        /// <summary>Server moves this player (night start, meeting, revive); <paramref name="refill"/> also resets stamina and adrenaline.</summary>
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void TeleportRpc(Vector3 position, Quaternion rotation, bool refill)
        {
            if (refill)
            {
                Stamina.Refill();
                Adrenaline.Reset();
            }
            Teleport(position, rotation);
        }

        /// <summary>Owner-side teleport that bypasses interpolation.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _cc.enabled = false;
            var nt = GetComponent<NetworkTransform>();
            if (nt != null && nt.CanCommitToTransform) nt.Teleport(position, rotation, transform.localScale);
            else transform.SetPositionAndRotation(position, rotation);
            _cc.enabled = true;
            _vy = 0f;
            _external = Vector3.zero;
            GetComponent<PlayerLook>()?.SetYaw(rotation.eulerAngles.y);
        }
    }
}
