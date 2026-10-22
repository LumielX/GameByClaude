using UnityEngine;

namespace BooTrayal.Game.Flow
{
    /// <summary>Where players appear in a map; players pick SpawnPoint[ownerId % count] by Index order.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        public int Index;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward);
        }
    }
}
