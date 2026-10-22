using BooTrayal.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Items
{
    /// <summary>
    /// Something players can pick up. Always server-authoritative: the server moves held items to the holders'
    /// hold points (light: one holder, snaps to the hand; heavy: two lift it, one drags it slowly and noisily).
    /// The holding client also draws a light item at its own hand so it never looks laggy.
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
            var col = GetComponentInChildren<Collider>();
            var extents = col != null ? col.bounds.extents : Vector3.one * 0.25f;
            _radius = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z)) * 0.9f;
            _halfHeight = extents.y;
        }

        float _radius;
        float _halfHeight;

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
            _rb.isKinematic = true;
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
                _rb.isKinematic = false;
                float speed = CarryRules.ClampThrow(velocity.magnitude);
                _rb.linearVelocity = speed > 0f ? velocity.normalized * speed : Vector3.zero;
            }
            else if (Holders.Count == 0)
            {
                _rb.isKinematic = false;
            }
            Debug.Log($"ITEM_RELEASED item={ItemId} by={clientId} reason={reason}");
            if (Debug.isDebugBuild || Application.isBatchMode) StartCoroutine(LogStateAfterRelease());
        }

        System.Collections.IEnumerator LogStateAfterRelease()
        {
            foreach (var delay in new[] { 0.05f, 0.5f })
            {
                yield return new WaitForSeconds(delay);
                Debug.Log($"ITEM_STATE item={ItemId} kinematic={_rb.isKinematic} vel={_rb.linearVelocity} pos={transform.position} owner={OwnerClientId}");
            }
        }

        void OnClientDisconnect(ulong clientId)
        {
            if (Holders.Contains(clientId)) Release(clientId, Vector3.zero, "disconnect");
        }

        void LateUpdate()
        {
            // Purely visual for the holding client: draw the light item at our own hand (server copy lags by RTT).
            if (!IsSpawned || Heavy || Holders.Count == 0 || Holders[0] != NetworkManager.LocalClientId) return;
            var hold = HoldPointOf(NetworkManager.LocalClientId);
            if (hold != null) transform.SetPositionAndRotation(ClearHandPosition(hold), hold.rotation);
        }

        /// <summary>The hold point, pulled back toward the holder's head if a wall is in between (no carrying through walls).</summary>
        Vector3 ClearHandPosition(Transform hold)
        {
            var from = hold.parent != null ? hold.parent.position : hold.position;
            var to = hold.position;
            var path = to - from;
            float dist = path.magnitude;
            if (dist < 1e-4f) return to;
            var dir = path / dist;
            float best = dist;
            foreach (var hit in Physics.SphereCastAll(from, ItemRadius, dir, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsStatic(hit.collider) || hit.distance >= best) continue;
                best = hit.distance;
            }
            return from + dir * best;
        }

        /// <summary>Puts a dragged item back on whatever floor is under it (ramps, steps).</summary>
        Vector3 OnGround(Vector3 target)
        {
            float bestY = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(target + Vector3.up * 1.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (IsStatic(hit.collider) && hit.point.y > bestY && hit.point.y <= target.y + 1.5f) bestY = hit.point.y;
            if (float.IsNegativeInfinity(bestY)) return target;
            target.y = bestY + _halfHeight;
            return target;
        }

        /// <summary>World geometry only: not players (CharacterController) and not other physics bodies.</summary>
        static bool IsStatic(Collider c) => c.attachedRigidbody == null && !(c is CharacterController);

        float ItemRadius => _radius;

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            _impactCooldown -= Time.fixedDeltaTime;
            if (Holders.Count == 0) return;
            if (!Heavy)
            {
                var hand = HoldPointOf(Holders[0]);
                if (hand == null) Release(Holders[0], Vector3.zero, "stretch");
                else _rb.MovePosition(ClearHandPosition(hand));
                return;
            }

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
                sum += ClearHandPosition(hold);
                count++;
            }
            if (count == 0) return;

            var target = sum / count;
            bool lifted = CarryRules.IsLifted(true, count);
            if (!lifted) target = OnGround(target); // dragged along the floor, following ramps and steps
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
