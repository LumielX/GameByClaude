using BooTrayal.Game.Items;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Builds the soul orb a dead player leaves behind: a light, glowing grabbable spawned by the NightDirector.</summary>
    public static class SoulOrbPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Items/SoulOrb.prefab";
        static readonly Color Glow = new Color(0.6f, 0.85f, 1f);

        public static GameObject Build()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root;
            if (exists) root = PrefabUtility.LoadPrefabContents(PrefabPath);
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                root.name = "SoulOrb";
            }
            root.transform.localScale = Vector3.one * 0.36f;
            root.GetComponent<Renderer>().sharedMaterial = GhostPrefabBuilder.Glowing(Lit("SoulOrb", Glow, 0.7f));

            var rb = Ensure<Rigidbody>(root);
            rb.mass = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Ensure<NetworkObject>(root);
            var nt = Ensure<NetworkTransform>(root);
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            Ensure<NetworkRigidbody>(root);
            var item = Ensure<GrabbableItem>(root);
            item.Heavy = false;
            item.ItemId = "soul";
            Ensure<SoulOrb>(root);

            var light = Ensure<Light>(Child(root, "Glow", Vector3.zero));
            light.type = LightType.Point;
            light.color = Glow;
            light.range = 3f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
            return saved;
        }
    }
}
