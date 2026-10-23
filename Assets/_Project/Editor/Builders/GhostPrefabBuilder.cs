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

            var skin = Lit("KrasueSkin", new Color(0.93f, 0.86f, 0.80f));
            var hair = Lit("KrasueHair", new Color(0.04f, 0.03f, 0.05f));
            var eye = Lit("KrasueEye", new Color(0.9f, 0.1f, 0.15f), 0.6f);
            var glow = Glowing(Lit("KrasueGlow", Neon, 0.4f));

            var visual = Child(root, "Visual", Vector3.zero);
            float h = GhostController.HeadHeight;
            Primitive(visual, "Head", PrimitiveType.Sphere, new Vector3(0f, h, 0f), new Vector3(0.42f, 0.46f, 0.42f), skin, false);
            Primitive(visual, "Hair", PrimitiveType.Sphere, new Vector3(0f, h + 0.08f, -0.06f), new Vector3(0.5f, 0.5f, 0.5f), hair, false);
            Primitive(visual, "HairTail", PrimitiveType.Capsule, new Vector3(0f, h - 0.15f, -0.22f), new Vector3(0.32f, 0.3f, 0.12f), hair, false);
            Primitive(visual, "EyeL", PrimitiveType.Sphere, new Vector3(-0.08f, h + 0.03f, 0.19f), new Vector3(0.08f, 0.06f, 0.05f), eye, false);
            Primitive(visual, "EyeR", PrimitiveType.Sphere, new Vector3(0.08f, h + 0.03f, 0.19f), new Vector3(0.08f, 0.06f, 0.05f), eye, false);
            Primitive(visual, "Entrail0", PrimitiveType.Capsule, new Vector3(0f, h - 0.42f, 0f), new Vector3(0.14f, 0.22f, 0.14f), glow, false);
            Primitive(visual, "Entrail1", PrimitiveType.Capsule, new Vector3(0.07f, h - 0.62f, 0.03f), new Vector3(0.1f, 0.18f, 0.1f), glow, false, new Vector3(0f, 0f, 12f));
            Primitive(visual, "Entrail2", PrimitiveType.Capsule, new Vector3(-0.06f, h - 0.78f, -0.02f), new Vector3(0.08f, 0.15f, 0.08f), glow, false, new Vector3(0f, 0f, -15f));
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
        static Material Glowing(Material mat)
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
