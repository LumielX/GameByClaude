using BooTrayal.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Items
{
    /// <summary>
    /// Something players can pick up. Light items: ownership moves to the holder, who carries them at its HoldPoint.
    /// Heavy items stay server-owned: two holders lift them, one holder drags them slowly and noisily.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableItem : NetworkBehaviour
    {
        public bool Heavy;
        public string ItemId = "item";

        const float HeavyMaxStretch = 3f;
        const float DragNoiseInterval = 0.5f;
        const float ImpactNoiseCooldown = 0.2f;
        const float DragImpulseEquivalent = 6f;

        public NetworkList<ulong> Holders;

        Rigidbody _rb;
        float _dragNoiseTimer;
        float _impactCooldown;

        void Awake()
        {
            Holders = new NetworkList<ulong>();
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnect;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        }

        public Vector3 ClosestPoint(Vector3 from)
        {
            var col = GetComponentInChildren<Collider>();
            return col != null ? col.ClosestPoint(from) : transform.position;
        }

        /// <summary>Server: tries to add a holder. <paramref name="distance"/> is from the holder's hands.</summary>
        public bool TryGrab(ulong clientId, float distance)
        {
            if (!IsServer) return false;
            if (!CarryRules.CanGrab(Heavy, Holders.Count, Holders.Contains(clientId), distance)) return false;
            Holders.Add(clientId);
            if (!Heavy) NetworkObject.ChangeOwnership(clientId);
            else _rb.isKinematic = true;
            Debug.Log($"ITEM_GRABBED item={ItemId} by={clientId} holders={Holders.Count}");
            if (Heavy && Holders.Count == 2) Debug.Log($"HEAVY_LIFTED item={ItemId}");
            return true;
        }

        /// <summary>Server: removes a holder; light items fly off with <paramref name="velocity"/>.</summary>
        public void Release(ulong clientId, Vector3 velocity, string reason)
        {
            if (!IsServer || !Holders.Contains(clientId)) return;
            Holders.Remove(clientId);
            if (!Heavy)
            {
                NetworkObject.RemoveOwnership();
                _rb.isKinematic = false;
                float speed = CarryRules.ClampThrow(velocity.magnitude);
                _rb.linearVelocity = speed > 0f ? velocity.normalized * speed : Vector3.zero;
            }
            else if (Holders.Count == 0)
            {
                _rb.isKinematic = false;
            }
            Debug.Log($"ITEM_RELEASED item={ItemId} by={clientId} reason={reason}");
        }

        void OnClientDisconnect(ulong clientId)
        {
            if (Holders.Contains(clientId)) Release(clientId, Vector3.zero, "disconnect");
        }

        void LateUpdate()
        {
            // Light item: the holder (owner) carries it in front of its face.
            if (!IsSpawned || Heavy || Holders.Count == 0 || !IsOwner) return;
            if (Holders[0] != NetworkManager.LocalClientId) return;
            var hold = HoldPointOf(NetworkManager.LocalClientId);
            if (hold == null) return;
            _rb.isKinematic = true;
            transform.SetPositionAndRotation(hold.position, hold.rotation);
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            _impactCooldown -= Time.fixedDeltaTime;
            if (!Heavy || Holders.Count == 0) return;

            // Heavy item: follow the average hold point; drop holders who stretched too far.
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = Holders.Count - 1; i >= 0; i--)
            {
                var hold = HoldPointOf(Holders[i]);
                if (hold == null || Vector3.Distance(hold.position, transform.position) > HeavyMaxStretch)
                {
                    var id = Holders[i];
                    Release(id, Vector3.zero, "stretch");
                    continue;
                }
                sum += hold.position;
                count++;
            }
            if (count == 0) return;

            var target = sum / count;
            bool lifted = CarryRules.IsLifted(true, count);
            if (!lifted) target.y = transform.position.y; // dragged along the floor
            float maxSpeed = MovementModel.RunSpeed * 1.5f * CarryRules.SpeedFactor(lifted ? CarryLoad.HeavyShared : CarryLoad.HeavyAlone);
            var next = Vector3.MoveTowards(_rb.position, target, maxSpeed * Time.fixedDeltaTime);
            float moved = Vector3.Distance(next, _rb.position);
            _rb.MovePosition(next);

            if (!lifted && moved > 0.2f * Time.fixedDeltaTime)
            {
                _dragNoiseTimer -= Time.fixedDeltaTime;
                if (_dragNoiseTimer <= 0f)
                {
                    _dragNoiseTimer = DragNoiseInterval;
                    NoiseBus.Raise(transform.position, NoiseRules.FromImpact(DragImpulseEquivalent, false));
                }
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsSpawned || !IsServer || Holders.Count > 0 || _impactCooldown > 0f) return;
            var noise = NoiseRules.FromImpact(collision.impulse.magnitude, Heavy);
            if (noise.Loudness <= 0f) return;
            _impactCooldown = ImpactNoiseCooldown;
            NoiseBus.Raise(transform.position, noise);
        }

        Transform HoldPointOf(ulong clientId)
        {
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            return player != null ? player.transform.Find("HeadPivot/HoldPoint") : null;
        }
    }
}
