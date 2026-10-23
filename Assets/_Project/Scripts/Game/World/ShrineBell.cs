using BooTrayal.Game.Night;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.World
{
    /// <summary>The bell at the ศาลพระภูมิ (GDD 7.4): E rings it to call a meeting. Rules live in the NightDirector.</summary>
    public class ShrineBell : NetworkBehaviour, IStation
    {
        public void ServerInteract(ulong clientId, int part)
        {
            if (IsServer && part == 0) NightHooks.RingBell?.Invoke(clientId);
        }
    }
}
