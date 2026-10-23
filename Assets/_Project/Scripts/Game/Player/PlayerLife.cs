using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>
    /// Server-decided alive / grabbed / dead state. Dead players vanish and (M4 stopgap) come back at their spawn
    /// point after <see cref="RespawnSeconds"/>; M5 replaces that with the mini ghost.
    /// </summary>
    public class PlayerLife : NetworkBehaviour
    {
        public const float RespawnSeconds = 5f;

        public readonly NetworkVariable<bool> Dead =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Held by a ghost: cannot move, look or use hands.</summary>
        public readonly NetworkVariable<bool> Grabbed =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsAlive => !Dead.Value;
        public bool CanMove => !Dead.Value && !Grabbed.Value;

        public override void OnNetworkSpawn()
        {
            Dead.OnValueChanged += OnDeadChanged;
            Apply(Dead.Value);
        }

        public override void OnNetworkDespawn() => Dead.OnValueChanged -= OnDeadChanged;

        void OnDeadChanged(bool previous, bool dead) => Apply(dead);

        void Apply(bool dead)
        {
            var visual = transform.Find("Visual");
            if (visual != null) visual.gameObject.SetActive(!dead);
            GetComponent<CharacterController>().enabled = !dead;
            GetComponent<PlayerFlashlight>()?.Refresh();
        }

        /// <summary>Server: a ghost starts or stops holding this player.</summary>
        public void SetGrabbed(bool grabbed)
        {
            if (IsServer && !Dead.Value) Grabbed.Value = grabbed;
        }

        /// <summary>Server: the player dies (drops what it holds) and respawns later.</summary>
        public void Kill()
        {
            if (!IsServer || Dead.Value) return;
            Grabbed.Value = false;
            Dead.Value = true;
            GetComponent<PlayerHands>()?.ForceRelease("death");
            Debug.Log($"PLAYER_KILLED id={OwnerClientId} t={Time.time:0.00}");
            StartCoroutine(RespawnLater());
        }

        IEnumerator RespawnLater()
        {
            yield return new WaitForSeconds(RespawnSeconds);
            if (!IsSpawned) yield break;
            var point = PlayerSpawnPlacer.PointFor(OwnerClientId);
            if (point != null) RespawnRpc(point.position, point.rotation);
            Dead.Value = false;
            Debug.Log($"PLAYER_RESPAWNED id={OwnerClientId}");
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void RespawnRpc(Vector3 position, Quaternion rotation)
        {
            GetComponent<PlayerMotor>().Teleport(position, rotation);
        }
    }
}
