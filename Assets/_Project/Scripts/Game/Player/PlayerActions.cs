using BooTrayal.Core.Night;
using BooTrayal.Game.Night;
using BooTrayal.Game.World;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>
    /// E on an aimed station part (checked before PlayerHands grabs), Q/X Spy powers (until M6's wheel) and voting.
    /// The server re-checks life state and distance before forwarding to the station or the NightDirector.
    /// </summary>
    public class PlayerActions : NetworkBehaviour
    {
        public const float InteractRange = 2.5f;
        const float RangeTolerance = 0.6f;

        Transform _head;
        PlayerLife _life;

        /// <summary>The station part under the crosshair (owner only), for the HUD hint.</summary>
        public Interactable Aimed { get; private set; }

        void Awake()
        {
            _head = transform.Find("HeadPivot");
            _life = GetComponent<PlayerLife>();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            Aimed = _life.CanAct ? FindAimed() : null;
            var input = PlayerInputSource.Get(gameObject);
            if (input.PowerSabotagePressed) PowerRpc(SpyPower.SabotagePuzzle);
            if (input.PowerBlackoutPressed) PowerRpc(SpyPower.Blackout);
        }

        /// <summary>Called by PlayerHands on E: true if E went to a station part instead of grabbing.</summary>
        public bool TryInteractAimed()
        {
            if (Aimed == null) return false;
            RequestInteract(Aimed);
            return true;
        }

        Interactable FindAimed()
        {
            var hits = Physics.RaycastAll(_head.position, _head.forward, InteractRange, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                var part = hit.collider.GetComponent<Interactable>();
                if (part != null) return part;
                if (!hit.collider.isTrigger) return null; // a wall or prop is in the way
            }
            return null;
        }

        public void RequestInteract(Interactable part)
        {
            if (IsOwner && part != null && part.Station != null) InteractRpc(part.Station.NetworkObject, part.Part);
        }

        public void RequestPower(SpyPower power)
        {
            if (IsOwner) PowerRpc(power);
        }

        /// <summary>Vote for <paramref name="target"/>, or skip with null.</summary>
        public void RequestVote(ulong? target)
        {
            if (IsOwner) VoteRpc(!target.HasValue, target ?? 0);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void InteractRpc(NetworkObjectReference stationRef, int part)
        {
            if (!_life.CanAct || !stationRef.TryGet(out var no)) return;
            Interactable target = null;
            foreach (var p in no.GetComponentsInChildren<Interactable>())
                if (p.Part == part) { target = p; break; }
            var station = no.GetComponent<IStation>();
            if (target == null || station == null) return;
            float d = Vector3.Distance(_head.position, target.ClosestPoint(_head.position));
            if (d > InteractRange + RangeTolerance)
            {
                Debug.Log($"INTERACT_DENIED by={OwnerClientId} station={no.name} part={part} dist={d:0.0}");
                return;
            }
            station.ServerInteract(OwnerClientId, part);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void PowerRpc(SpyPower power) => NightHooks.Power?.Invoke(OwnerClientId, power);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void VoteRpc(bool skip, ulong target) => NightHooks.Vote?.Invoke(OwnerClientId, skip ? (ulong?)null : target);
    }
}
