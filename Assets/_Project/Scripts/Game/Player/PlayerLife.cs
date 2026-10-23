using System.Collections;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Night;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>
    /// Server-decided life state: Alive, MiniGhost (GDD 5.1: floats, cannot act, revivable at the shrine) or Escaped
    /// (frozen outside the gate). Without a NightDirector (Sandbox) a killed player comes back after
    /// <see cref="RespawnSeconds"/> as before.
    /// </summary>
    public class PlayerLife : NetworkBehaviour
    {
        public const float RespawnSeconds = 5f;

        public readonly NetworkVariable<LifeState> State =
            new NetworkVariable<LifeState>(LifeState.Alive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Held by a ghost: cannot move, look or use hands.</summary>
        public readonly NetworkVariable<bool> Grabbed =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsAlive => State.Value == LifeState.Alive;
        public bool IsMiniGhost => State.Value == LifeState.MiniGhost;
        /// <summary>May walk or fly (alive or mini ghost, not held by a ghost).</summary>
        public bool CanMove => (IsAlive || IsMiniGhost) && !Grabbed.Value;
        /// <summary>May grab, push, use puzzles, ring the bell and vote.</summary>
        public bool CanAct => IsAlive && !Grabbed.Value;

        public override void OnNetworkSpawn()
        {
            State.OnValueChanged += OnStateChanged;
            Apply(State.Value);
        }

        public override void OnNetworkDespawn() => State.OnValueChanged -= OnStateChanged;

        void OnStateChanged(LifeState previous, LifeState state) => Apply(state);

        void Apply(LifeState state)
        {
            bool ghost = state == LifeState.MiniGhost;
            var visual = transform.Find("Visual");
            if (visual != null) visual.gameObject.SetActive(!ghost);
            var mini = transform.Find("MiniGhostVisual");
            if (mini != null) mini.gameObject.SetActive(ghost);
            gameObject.layer = ghost ? PlayerLayers.MiniGhost : PlayerLayers.Player;
            GetComponent<PlayerFlashlight>()?.Refresh();
        }

        /// <summary>Server: a ghost starts or stops holding this player.</summary>
        public void SetGrabbed(bool grabbed)
        {
            if (IsServer && IsAlive) Grabbed.Value = grabbed;
        }

        /// <summary>Server: set by the NightDirector (night start, revive, escape, vote).</summary>
        public void SetState(LifeState state)
        {
            if (!IsServer) return;
            Grabbed.Value = false;
            if (state != LifeState.Alive) GetComponent<PlayerHands>()?.ForceRelease(state == LifeState.MiniGhost ? "death" : "escape");
            State.Value = state;
        }

        /// <summary>Server: the ghost killed this player.</summary>
        public void Kill()
        {
            if (!IsServer || !IsAlive) return;
            Debug.Log($"PLAYER_KILLED id={OwnerClientId} t={Time.time:0.00}");
            if (NightHooks.PlayerKilled != null && NightHooks.PlayerKilled(OwnerClientId, transform.position)) return;
            SetState(LifeState.MiniGhost);
            StartCoroutine(RespawnLater());
        }

        IEnumerator RespawnLater()
        {
            yield return new WaitForSeconds(RespawnSeconds);
            if (!IsSpawned) yield break;
            var point = PlayerSpawnPlacer.PointFor(OwnerClientId);
            if (point != null) GetComponent<PlayerMotor>().TeleportRpc(point.position, point.rotation, true);
            SetState(LifeState.Alive);
            Debug.Log($"PLAYER_RESPAWNED id={OwnerClientId}");
        }
    }
}
