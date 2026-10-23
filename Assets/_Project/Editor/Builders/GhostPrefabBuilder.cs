using BooTrayal.Game.Ghosts;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Builds the networked Krasue prefab: a floating head trailing glowing entrails (placeholder until M7 art).</summary>
    public static class GhostPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Ghosts/Krasue.prefab";
        static readonly Color Neon = new Color(0.224f, 1f, 0.533f); // #39FF88

        public static GameObject Build()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Krasue");

            Ensure<NetworkObject>(root);
            var nt = Ensure<NetworkTransform>(root);
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.Interpolate = true;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            var agent = Ensure<NavMeshAgent>(root);
            agent.agentTypeID = 0;
            agent.radius = 0.4f;
            agent.height = 1.6f;
            agent.baseOffset = 0f;
            agent.speed = 2.2f;
            agent.acceleration = 30f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;

            Ensure<GhostController>(root);
            Ensure<GhostView>(root);
            var trigger = Ensure<SphereCollider>(root);
            trigger.isTrigger = true;
            trigger.radius = 0.5f;
            trigger.center = new Vector3(0f, GhostController.HeadHeight, 0f);
            var voice = Ensure<AudioSource>(root);
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.minDistance = 2f;
            voice.maxDistance = 30f;


            var visual = Child(root, "Visual", Vector3.zero);
            float h = GhostController.HeadHeight;
            foreach (var old in new[] { "Head", "Hair", "HairTail", "EyeL", "EyeR", "Entrail0", "Entrail1", "Entrail2" })
            {
                var t = visual.transform.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            if (visual.transform.Find("Model") == null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(ModelImportBuilder.Model("Krasue"));
                model.name = "Model";
                model.transform.SetParent(visual.transform, false);
            }
            var light = Ensure<Light>(Child(visual, "Glow", new Vector3(0f, h - 0.5f, 0f)));
            light.type = LightType.Point;
            light.color = Neon;
            light.range = 4f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
            Debug.Log("[GhostPrefabBuilder] Krasue prefab ready");
            return saved;
        }

        /// <summary>Turns on URP Lit emission in the material's own colour (no change when already set).</summary>
        public static Material Glowing(Material mat)
        {
            var emission = mat.GetColor("_BaseColor") * 2f;
            if (mat.IsKeywordEnabled("_EMISSION") && mat.GetColor("_EmissionColor") == emission) return mat;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }
    }
}
