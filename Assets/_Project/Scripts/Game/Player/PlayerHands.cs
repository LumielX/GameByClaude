using BooTrayal.Core.Player;
using BooTrayal.Game.Items;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>E grabs/drops, LMB throws, G drops. The server decides who holds what.</summary>
    public class PlayerHands : NetworkBehaviour
    {
        public const float ThrowSpeed = 8f;
        const float ProbeRadius = 0.25f;

        public readonly NetworkVariable<NetworkObjectReference> Held =
            new NetworkVariable<NetworkObjectReference>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        Transform _head;
        Transform _hold;
        PlayerMotor _motor;
        PlayerLife _life;
        PlayerActions _actions;

        void Awake()
        {
            _head = transform.Find("HeadPivot");
            _hold = transform.Find("HeadPivot/HoldPoint");
            _motor = GetComponent<PlayerMotor>();
            _life = GetComponent<PlayerLife>();
            _actions = GetComponent<PlayerActions>();
        }

        public GrabbableItem HeldItem => Held.Value.TryGet(out var no) ? no.GetComponent<GrabbableItem>() : null;

        void Update()
        {
            if (!IsSpawned) return;
            var item = HeldItem;
            if (IsServer && item != null && !item.Holders.Contains(OwnerClientId))
                Held.Value = default; // released by the server (disconnect/stretch)
            if (!IsOwner) return;

            _motor.Load = item != null ? CarryRules.LoadFor(true, item.Heavy, item.Holders.Count) : CarryLoad.None;
            if (_life != null && !_life.CanAct) return;
            var input = PlayerInputSource.Get(gameObject);
            if (input.InteractPressed)
            {
                if (item != null) DropRpc();
                else if (_actions != null && _actions.TryInteractAimed()) { }
                else if (FindGrabbable(out var target)) GrabRpc(target.NetworkObject);
            }
            else if (input.ThrowPressed && item != null) ThrowRpc(_head.forward);
            else if (input.DropPressed && item != null) DropRpc();
        }

        bool FindGrabbable(out GrabbableItem target)
        {
            target = null;
            var hits = Physics.SphereCastAll(_head.position, ProbeRadius, _head.forward, CarryRules.GrabRange, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var hit in hits)
            {
                var g = hit.collider.GetComponentInParent<GrabbableItem>();
                if (g == null || hit.distance >= best) continue;
                best = hit.distance;
                target = g;
            }
            return target != null;
        }

        /// <summary>Test hook: nearest grabbable to the hands within range, ignoring view direction.</summary>
        public GrabbableItem NearestGrabbable(bool heavy)
        {
            GrabbableItem best = null;
            float bestDist = float.MaxValue;
            foreach (var g in FindObjectsByType<GrabbableItem>())
            {
                if (g.Heavy != heavy) continue;
                float d = Vector3.Distance(_hold.position, g.ClosestPoint(_hold.position));
                if (d < bestDist) { bestDist = d; best = g; }
            }
            return best;
        }

        public void RequestGrab(GrabbableItem item)
        {
            if (IsOwner && item != null) GrabRpc(item.NetworkObject);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void GrabRpc(NetworkObjectReference itemRef)
        {
            if ((_life != null && !_life.CanAct) || HeldItem != null || !itemRef.TryGet(out var no)) return;
            var item = no.GetComponent<GrabbableItem>();
            if (item == null) return;
            float distance = Vector3.Distance(_hold.position, item.ClosestPoint(_hold.position));
            if (item.TryGrab(OwnerClientId, distance)) Held.Value = itemRef;
            else Debug.Log($"GRAB_DENIED by={OwnerClientId} item={item.ItemId} dist={distance:0.00}");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ThrowRpc(Vector3 direction)
        {
            if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z)) direction = Vector3.zero;
            ReleaseHeld(direction.normalized * ThrowSpeed, "throw");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void DropRpc() => ReleaseHeld(Vector3.zero, "drop");

        /// <summary>Server: drop whatever is held (death, grabbed by a ghost).</summary>
        public void ForceRelease(string reason)
        {
            if (IsServer) ReleaseHeld(Vector3.zero, reason);
        }

        void ReleaseHeld(Vector3 velocity, string reason)
        {
            var item = HeldItem;
            Held.Value = default;
            if (item != null) item.Release(OwnerClientId, velocity, reason);
        }
    }
}
