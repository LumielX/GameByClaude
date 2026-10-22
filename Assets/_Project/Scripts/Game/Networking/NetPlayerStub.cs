using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Networking
{
    /// <summary>Placeholder player object until the real player prefab arrives in M3.</summary>
    public class NetPlayerStub : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            Debug.Log($"NET_PLAYER_SPAWNED owner={OwnerClientId} local={IsOwner}");
        }
    }
}
