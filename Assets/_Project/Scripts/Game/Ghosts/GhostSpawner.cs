using BooTrayal.Game.Night;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Ghosts
{
    /// <summary>
    /// In-scene marker for the ghost's start point. With a NightDirector the ghost spawns at night start and despawns
    /// at night end; without one (test scenes) it spawns as soon as the scene's network objects spawn.
    /// </summary>
    public class GhostSpawner : NetworkBehaviour
    {
        public GameObject Prefab;

        public GhostController Current { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsServer && FindAnyObjectByType<NightDirector>() == null) SpawnGhost();
        }

        public GhostController SpawnGhost()
        {
            if (!IsServer || Prefab == null) return null;
            if (Current != null && Current.IsSpawned) return Current;
            var ghost = Instantiate(Prefab, transform.position, transform.rotation);
            ghost.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            Current = ghost.GetComponent<GhostController>();
            return Current;
        }

        public void DespawnGhost()
        {
            if (IsServer && Current != null && Current.IsSpawned) Current.NetworkObject.Despawn(true);
            Current = null;
        }
    }
}
