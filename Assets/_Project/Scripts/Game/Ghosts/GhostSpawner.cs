using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Ghosts
{
    /// <summary>In-scene marker: the server spawns <see cref="Prefab"/> here when the scene's network objects spawn.</summary>
    public class GhostSpawner : NetworkBehaviour
    {
        public GameObject Prefab;

        public override void OnNetworkSpawn()
        {
            if (!IsServer || Prefab == null) return;
            var ghost = Instantiate(Prefab, transform.position, transform.rotation);
            ghost.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
        }
    }
}
