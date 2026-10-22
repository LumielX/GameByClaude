using BooTrayal.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>RMB pushes/slaps the player in front of you; the server validates and tells the target's owner.</summary>
    public class PlayerPush : NetworkBehaviour
    {
        const float ClientProbeRange = 2.0f;

        Transform _head;
        PlayerMotor _motor;
        double _lastPush = double.NegativeInfinity;
        Vector3 _lastServerPos;
        float _serverSpeed;

        void Awake()
        {
            _head = transform.Find("HeadPivot");
            _motor = GetComponent<PlayerMotor>();
        }

        public override void OnNetworkSpawn() => _lastServerPos = transform.position;

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer && Time.deltaTime > 0f)
            {
                // Server-side speed estimate from the synced transform (used to decide knockdowns).
                var delta = transform.position - _lastServerPos;
                delta.y = 0f;
                _serverSpeed = Mathf.Lerp(_serverSpeed, delta.magnitude / Time.deltaTime, 0.2f);
                _lastServerPos = transform.position;
            }
            if (!IsOwner || !PlayerInputSource.Get(gameObject).PushPressed) return;
            if (Physics.SphereCast(_head.position, 0.3f, _head.forward, out var hit, ClientProbeRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<PlayerMotor>();
                if (target != null && target != _motor) PushRpc(target.NetworkObject);
            }
        }

        /// <summary>Test hook used by SmokeProbe.</summary>
        public void RequestPush(PlayerMotor target)
        {
            if (IsOwner && target != null) PushRpc(target.NetworkObject);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void PushRpc(NetworkObjectReference targetRef)
        {
            if (!targetRef.TryGet(out var no)) return;
            var target = no.GetComponent<PlayerMotor>();
            if (target == null) return;
            var offset = no.transform.position - transform.position;
            offset.y = 0f;
            double now = NetworkManager.ServerTime.Time;
            var outcome = PushRules.Evaluate(OwnerClientId, no.OwnerClientId, offset.magnitude, now, _lastPush, _serverSpeed,
                _motor.IsKnockedDown.Value, target.IsKnockedDown.Value);
            if (!outcome.Allowed)
            {
                Debug.Log($"PUSH_DENIED by={OwnerClientId} reason={outcome.Reason}");
                return;
            }
            _lastPush = now;
            var dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : transform.forward;
            target.KnockbackRpc(dir * PushRules.KnockbackSpeed, outcome.Knockdown);
            Debug.Log($"PUSH by={OwnerClientId} target={no.OwnerClientId} knockdown={outcome.Knockdown}");
        }
    }
}
