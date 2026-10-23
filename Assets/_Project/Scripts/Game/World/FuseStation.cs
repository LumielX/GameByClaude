using BooTrayal.Core;
using BooTrayal.Core.Puzzles;
using BooTrayal.Game.Items;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.World
{
    /// <summary>
    /// Puzzle #2 (GDD 8): two levers in two rooms (parts 0 and 1) pulled within 1 s of each other. Solo fallback: an
    /// item left resting against a lever (within <see cref="PropRadius"/>) holds it down.
    /// </summary>
    public class FuseStation : SealStation
    {
        public const float PropRadius = 0.6f;
        const float PropCheckInterval = 0.25f;
        const double PulledShowSeconds = 1.0;

        public Transform[] Handles = new Transform[2];

        /// <summary>Bit 0/1: lever 0/1 shown down (recently pulled or propped).</summary>
        public readonly NetworkVariable<byte> LeverState =
            new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        FuseBoxPuzzle _puzzle;
        readonly double[] _pulledAt = { double.NegativeInfinity, double.NegativeInfinity };
        readonly bool[] _propped = new bool[2];
        float _propTimer;

        public override bool TwoPerson => true;
        public override string Kind => "fuse";
        protected override IPuzzle Puzzle => _puzzle;

        public Interactable Lever(int index)
        {
            foreach (var p in GetComponentsInChildren<Interactable>())
                if (p.Part == index) return p;
            return null;
        }

        protected override void OnReset(IRandom rng)
        {
            _puzzle = new FuseBoxPuzzle();
            _pulledAt[0] = _pulledAt[1] = double.NegativeInfinity;
            _propped[0] = _propped[1] = false;
            LeverState.Value = 0;
        }

        protected override void Interact(ulong clientId, int part)
        {
            if (part != 0 && part != 1) return;
            double now = NetworkManager.ServerTime.Time;
            _pulledAt[part] = now;
            _puzzle.Pull(part, now, clientId);
            ContributorSet.Add(clientId);
            Debug.Log($"FUSE_PULL station={StationIndex} lever={part} by={clientId} t={now:0.00}");
        }

        protected override void OnSabotaged(IRandom rng)
        {
            _pulledAt[0] = _pulledAt[1] = double.NegativeInfinity;
            _propped[0] = _propped[1] = false;
        }

        void Update()
        {
            if (IsServer && IsSpawned && _puzzle != null)
            {
                _propTimer -= Time.deltaTime;
                if (_propTimer <= 0f && Active.Value && !Solved.Value)
                {
                    _propTimer = PropCheckInterval;
                    for (int i = 0; i < 2; i++) CheckProp(i);
                }
                double now = NetworkManager.ServerTime.Time;
                byte state = 0;
                for (int i = 0; i < 2; i++)
                    if (Solved.Value || _propped[i] || now - _pulledAt[i] < PulledShowSeconds) state |= (byte)(1 << i);
                if (LeverState.Value != state) LeverState.Value = state;
            }
            for (int i = 0; i < Handles.Length; i++)
            {
                if (Handles[i] == null) continue;
                bool down = (LeverState.Value & (1 << i)) != 0;
                Handles[i].localRotation = Quaternion.Euler(down ? 50f : -50f, 0f, 0f);
            }
        }

        void CheckProp(int lever)
        {
            var part = Lever(lever);
            if (part == null) return;
            GrabbableItem prop = null;
            foreach (var item in GrabbableItem.All)
            {
                if (item.Holders.Count > 0 || item.Locked.Value) continue;
                if (Vector3.Distance(item.ClosestPoint(part.transform.position), part.transform.position) <= PropRadius)
                {
                    prop = item;
                    break;
                }
            }
            bool propped = prop != null;
            if (propped == _propped[lever]) return;
            _propped[lever] = propped;
            if (propped)
            {
                ulong by = 0;
                foreach (var id in prop.LastCarriers) { by = id; break; }
                _puzzle.Prop(lever, by);
                if (prop.LastCarriers.Count > 0) ContributorSet.Add(by);
                Debug.Log($"FUSE_PROP station={StationIndex} lever={lever} item={prop.ItemId}");
            }
            else _puzzle.Unprop(lever);
        }
    }
}
