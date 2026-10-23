using System;
using System.Collections.Generic;
using System.Linq;
using BooTrayal.Core;
using BooTrayal.Core.Puzzles;
using BooTrayal.Game.Items;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.World
{
    /// <summary>
    /// Puzzle #3 (GDD 8): carry the ritual items (the heavy urn and the incense) here once the seals are closed.
    /// An item left resting in the altar area is delivered and locked in its slot; sabotage knocks one back home.
    /// </summary>
    public class RitualAltar : NetworkBehaviour, ISabotageable
    {
        const float CheckInterval = 0.25f;

        public string[] RequiredIds = { "urn", "incense" };
        public Vector3 AreaSize = new Vector3(3f, 2f, 3f);
        public Transform[] Slots = new Transform[0];

        public readonly NetworkVariable<int> Delivered =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Server sets this once every seal is closed (GDD 4: seals first, then the ritual items).</summary>
        public readonly NetworkVariable<bool> Accepting =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        RitualOfferingPuzzle _puzzle;
        float _timer;

        /// <summary>Server: (item, carriers) delivered.</summary>
        public event Action<GrabbableItem, IReadOnlyCollection<ulong>> DeliveredServer;
        /// <summary>Server: every item is on the altar.</summary>
        public event Action CompletedServer;

        public bool Complete => _puzzle != null && _puzzle.Solved;
        public bool CanSabotage => Accepting.Value && !Complete && Delivered.Value > 0;

        public bool InArea(Vector3 p) => new Bounds(transform.position + Vector3.up * AreaSize.y * 0.5f, AreaSize).Contains(p);

        public void ResetForNight()
        {
            if (!IsServer) return;
            _puzzle = new RitualOfferingPuzzle(RequiredIds);
            Delivered.Value = 0;
            Accepting.Value = false;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned || _puzzle == null || !Accepting.Value || Complete) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = CheckInterval;
            foreach (var item in GrabbableItem.All.ToList())
            {
                if (item.Holders.Count > 0 || item.Locked.Value || !RequiredIds.Contains(item.ItemId)) continue;
                if (!InArea(item.transform.position)) continue;
                var by = item.LastCarriers.ToList();
                if (!_puzzle.Deliver(item.ItemId, by.Count > 0 ? by[0] : 0)) continue;
                int slot = Array.IndexOf(RequiredIds, item.ItemId);
                var pose = slot >= 0 && slot < Slots.Length && Slots[slot] != null ? Slots[slot] : transform;
                item.Lock(pose.position, pose.rotation);
                Delivered.Value = _puzzle.Delivered.Count;
                Debug.Log($"RITUAL_DELIVERED item={item.ItemId} by={string.Join(",", by)}");
                DeliveredServer?.Invoke(item, by);
                if (_puzzle.Solved)
                {
                    CompletedServer?.Invoke();
                    break;
                }
            }
        }

        public float DistanceTo(Vector3 position) => Vector3.Distance(position, transform.position);

        public bool Sabotage(IRandom rng, ulong saboteur)
        {
            if (!IsServer || !CanSabotage || !_puzzle.TrySabotage(rng, saboteur)) return false;
            var id = _puzzle.LastSabotagedItem;
            var item = GrabbableItem.All.FirstOrDefault(i => i.ItemId == id);
            if (item != null) item.ResetHome();
            Delivered.Value = _puzzle.Delivered.Count;
            return true;
        }
    }
}
