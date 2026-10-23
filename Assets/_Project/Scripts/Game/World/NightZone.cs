using UnityEngine;

namespace BooTrayal.Game.World
{
    /// <summary>A named area for the Spy's blackout (GDD 7.2). The first zone that contains a point wins.</summary>
    public class NightZone : MonoBehaviour
    {
        public int Index;
        public string ZoneName = "";
        public Vector3 Size = Vector3.one;

        public bool Contains(Vector3 p) => new Bounds(transform.position, Size).Contains(p);

        public static NightZone[] Ordered()
        {
            var zones = FindObjectsByType<NightZone>();
            System.Array.Sort(zones, (a, b) => a.Index.CompareTo(b.Index));
            return zones;
        }

        public static NightZone Find(Vector3 p)
        {
            foreach (var z in Ordered())
                if (z.Contains(p)) return z;
            return null;
        }
    }
}
