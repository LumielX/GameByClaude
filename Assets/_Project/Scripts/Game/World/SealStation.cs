using System;
using System.Collections.Generic;
using BooTrayal.Core;
using BooTrayal.Core.Puzzles;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.World
{
    /// <summary>Something a Spy can sabotage from up to 3 m away (seal stations, the ritual altar).</summary>
    public interface ISabotageable
    {
        bool CanSabotage { get; }
        float DistanceTo(Vector3 position);
        /// <summary>Server: true when progress was undone.</summary>
        bool Sabotage(IRandom rng, ulong saboteur);
    }

    /// <summary>
    /// One seal (GDD 4: close N seals per night). The puzzle itself is Core; this adapts it to the network:
    /// Active/Solved are synced, interaction arrives from PlayerActions, and the server raises <see cref="SolvedServer"/>.
    /// </summary>
    public abstract class SealStation : NetworkBehaviour, IStation, ISabotageable
    {
        public int StationIndex;

        public readonly NetworkVariable<bool> Active =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> Solved =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        protected readonly HashSet<ulong> ContributorSet = new HashSet<ulong>();

        /// <summary>Server: raised once when the puzzle is solved.</summary>
        public event Action<SealStation> SolvedServer;

        public abstract bool TwoPerson { get; }
        public abstract string Kind { get; }
        protected abstract IPuzzle Puzzle { get; }
        public IReadOnlyCollection<ulong> Contributors => ContributorSet;
        public float Progress01 => Puzzle != null ? Puzzle.Progress01 : 0f;
        public bool CanSabotage => Active.Value && !Solved.Value;

        public override void OnNetworkSpawn()
        {
            Active.OnValueChanged += (_, __) => RefreshMarks();
            Solved.OnValueChanged += (_, __) => RefreshMarks();
            RefreshMarks();
        }

        /// <summary>Server: new puzzle for tonight; inactive stations just sit there.</summary>
        public void ResetForNight(IRandom rng, bool active)
        {
            if (!IsServer) return;
            ContributorSet.Clear();
            OnReset(rng);
            Solved.Value = false;
            Active.Value = active;
        }

        protected abstract void OnReset(IRandom rng);
        protected abstract void Interact(ulong clientId, int part);
        protected virtual void OnSabotaged(IRandom rng) { }

        public void ServerInteract(ulong clientId, int part)
        {
            if (!IsServer || !Active.Value || Solved.Value || Puzzle == null) return;
            Interact(clientId, part);
            CheckSolved();
        }

        protected void CheckSolved()
        {
            if (Solved.Value || !Puzzle.Solved) return;
            Solved.Value = true;
            SolvedServer?.Invoke(this);
        }

        public bool Sabotage(IRandom rng, ulong saboteur)
        {
            if (!IsServer || !CanSabotage) return false;
            if (!Puzzle.TrySabotage(rng, saboteur)) return false;
            OnSabotaged(rng);
            return true;
        }

        public float DistanceTo(Vector3 position)
        {
            float best = float.PositiveInfinity;
            foreach (var part in GetComponentsInChildren<Interactable>())
                best = Mathf.Min(best, Vector3.Distance(position, part.ClosestPoint(position)));
            return best;
        }

        /// <summary>The glowing talismans (ยันต์) show where tonight's open seals are.</summary>
        void RefreshMarks()
        {
            bool show = Active.Value && !Solved.Value;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("SealMark")) t.gameObject.SetActive(show);
        }
    }
}
