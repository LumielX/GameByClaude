using BooTrayal.Core.Player;
using BooTrayal.Game.Items;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>Sounds a player makes for the ghost: running footsteps (server side) and the Z scream (owner asks, server checks).</summary>
    public class PlayerNoise : NetworkBehaviour
    {
        public const float FootstepInterval = 0.5f;
        public const float ScreamCooldown = 3f;
        const float HeadHeight = 1.5f;

        PlayerMotor _motor;
        PlayerLife _life;
        float _footTimer;
        float _ownerCooldown;
        double _lastScream = double.NegativeInfinity;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _life = GetComponent<PlayerLife>();
        }

        void Update()
        {
            if (!IsSpawned) return;
            float dt = Time.deltaTime;
            if (IsOwner)
            {
                _ownerCooldown -= dt;
                if (PlayerInputSource.Get(gameObject).ScreamPressed && _life.IsAlive && _ownerCooldown <= 0f)
                {
                    _ownerCooldown = ScreamCooldown;
                    ScreamRpc();
                }
            }
            if (!IsServer) return;

            bool running = _life.IsAlive && _motor.IsRunningNet.Value && !_motor.IsCrouchingNet.Value;
            if (!running) { _footTimer = 0f; return; }
            _footTimer -= dt;
            if (_footTimer > 0f) return;
            _footTimer = FootstepInterval;
            NoiseBus.Raise(transform.position, NoiseRules.Footstep(true, false));
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ScreamRpc()
        {
            double now = NetworkManager.ServerTime.Time;
            // Small tolerance so an honest client's 3 s timer is not rejected by tick jitter.
            if (!_life.IsAlive || now - _lastScream < ScreamCooldown - 0.2)
            {
                Debug.Log($"SCREAM_DENIED by={OwnerClientId}");
                return;
            }
            _lastScream = now;
            Debug.Log($"SCREAM by={OwnerClientId}");
            NoiseBus.Raise(transform.position + Vector3.up * HeadHeight, NoiseRules.FromVoice(1f));
        }
    }
}
