using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.World
{
    /// <summary>A station implemented on the server; players reach it through PlayerActions.InteractRpc.</summary>
    public interface IStation
    {
        void ServerInteract(ulong clientId, int part);
    }

    /// <summary>One pressable part of a station (keypad digit, lever, bell). Needs a (trigger) collider to be aimed at.</summary>
    public class Interactable : MonoBehaviour
    {
        public NetworkBehaviour Station;
        public int Part;
        public string Label = "";
        /// <summary>Where a player stands to use this part (on the NavMesh; smoke bots walk here).</summary>
        public Transform Approach;

        /// <summary>Closest point of this part's collider to <paramref name="from"/>.</summary>
        public Vector3 ClosestPoint(Vector3 from)
        {
            var col = GetComponent<Collider>();
            return col != null ? col.ClosestPoint(from) : transform.position;
        }
    }
}
