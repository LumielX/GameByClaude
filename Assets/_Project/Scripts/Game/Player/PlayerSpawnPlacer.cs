using System.Linq;
using BooTrayal.Game.Flow;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>The owner moves itself to its spawn point after spawning and after every scene load or late-join sync.</summary>
    public class PlayerSpawnPlacer : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            TryPlace();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }

        void OnSceneEvent(SceneEvent e)
        {
            if (e.ClientId != NetworkManager.LocalClientId) return;
            if (e.SceneEventType == SceneEventType.LoadComplete || e.SceneEventType == SceneEventType.SynchronizeComplete)
                TryPlace();
        }

        void TryPlace()
        {
            var point = PointFor(OwnerClientId);
            if (point == null) return;
            GetComponent<PlayerMotor>().Teleport(point.position, point.rotation);
            Debug.Log($"PLAYER_PLACED owner={OwnerClientId} spawn={point.GetComponent<SpawnPoint>().Index}");
        }

        /// <summary>The spawn point for a client in the loaded scene, or null if there is none.</summary>
        public static Transform PointFor(ulong clientId)
        {
            var points = FindObjectsByType<SpawnPoint>().OrderBy(p => p.Index).ToArray();
            if (points.Length == 0) return null;
            return points[(int)(clientId % (ulong)points.Length)].transform;
        }
    }
}
