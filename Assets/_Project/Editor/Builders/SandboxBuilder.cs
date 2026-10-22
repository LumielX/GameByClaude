using BooTrayal.Game.Flow;
using BooTrayal.Game.Items;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor.SceneManagement;
using UnityEngine;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Graybox test room for M3: floor, walls, ramp, step, 4 spawns, 4 light boxes and one heavy urn.</summary>
    public static class SandboxBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";

        public static void Build()
        {
            var scene = OpenOrCreateScene(ScenePath);
            var floorMat = Lit("Floor", new Color(0.23f, 0.16f, 0.13f));
            var wallMat = Lit("Wall", new Color(0.36f, 0.23f, 0.17f));
            var propMat = Lit("Prop", new Color(0.55f, 0.42f, 0.30f));
            var boxMat = Lit("ItemBox", new Color(0.85f, 0.55f, 0.25f));
            var urnMat = Lit("ItemUrn", new Color(0.78f, 0.62f, 0.25f), 0.5f);

            var env = Root(scene, "Environment");
            Primitive(env, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f), new Vector3(30f, 0.5f, 30f), floorMat, true);
            Primitive(env, "WallN", PrimitiveType.Cube, new Vector3(0f, 1.5f, 15f), new Vector3(30f, 3f, 0.5f), wallMat, true);
            Primitive(env, "WallS", PrimitiveType.Cube, new Vector3(0f, 1.5f, -15f), new Vector3(30f, 3f, 0.5f), wallMat, true);
            Primitive(env, "WallE", PrimitiveType.Cube, new Vector3(15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f), wallMat, true);
            Primitive(env, "WallW", PrimitiveType.Cube, new Vector3(-15f, 1.5f, 0f), new Vector3(0.5f, 3f, 30f), wallMat, true);
            Primitive(env, "Ramp", PrimitiveType.Cube, new Vector3(9f, 0.9f, -8f), new Vector3(3f, 0.3f, 6f), propMat, true, new Vector3(-17f, 0f, 0f));
            Primitive(env, "RampTop", PrimitiveType.Cube, new Vector3(9f, 0.9f, -12.5f), new Vector3(3f, 1.8f, 3f), propMat, true);
            Primitive(env, "Step", PrimitiveType.Cube, new Vector3(-9f, 0.3f, -8f), new Vector3(3f, 0.6f, 3f), propMat, true);
            Primitive(env, "Pillar1", PrimitiveType.Cylinder, new Vector3(-5f, 1.5f, 6f), new Vector3(0.5f, 1.5f, 0.5f), wallMat, true);
            Primitive(env, "Pillar2", PrimitiveType.Cylinder, new Vector3(5f, 1.5f, 6f), new Vector3(0.5f, 1.5f, 0.5f), wallMat, true);

            var spawns = Root(scene, "SpawnPoints");
            var corners = new[] { new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, 10f), new Vector3(10f, 0f, -10f), new Vector3(-10f, 0f, 10f) };
            for (int i = 0; i < corners.Length; i++)
            {
                var sp = Child(spawns, $"Spawn{i}", corners[i]);
                sp.transform.rotation = Quaternion.LookRotation(-corners[i].normalized, Vector3.up);
                Ensure<SpawnPoint>(sp).Index = i;
            }

            var boxes = new[] { new Vector3(-2f, 0.3f, -2f), new Vector3(2f, 0.3f, 2f), new Vector3(2f, 0.3f, -2f), new Vector3(-2f, 0.3f, 2f) };
            for (int i = 0; i < boxes.Length; i++)
                Item(scene, $"box{i + 1}", PrimitiveType.Cube, boxes[i], Vector3.one * 0.45f, boxMat, 2f, heavy: false);
            Item(scene, "urn", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 5f), new Vector3(0.9f, 0.6f, 0.9f), urnMat, 60f, heavy: true);

            var lights = Root(scene, "Lights");
            var moon = Ensure<Light>(Child(lights, "Moon", new Vector3(0f, 10f, 0f)));
            moon.type = LightType.Directional;
            moon.intensity = 0.35f;
            moon.color = new Color(0.62f, 0.7f, 1f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            Lamp(lights, "LampGreen", new Vector3(-6f, 2.5f, 0f), new Color(0.3f, 1f, 0.55f));
            Lamp(lights, "LampPurple", new Vector3(6f, 2.5f, 0f), new Color(0.65f, 0.35f, 1f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            // NetworkObject.GlobalObjectIdHash for in-scene objects is computed in OnValidate from the saved
            // scene/object ids, so a freshly created object saves 0 the first time. Reopen and save again.
            BuilderUtil.ResaveScene(ScenePath);
            Debug.Log("[SandboxBuilder] Sandbox scene ready");
        }

        static void Lamp(GameObject parent, string name, Vector3 pos, Color color)
        {
            var light = Ensure<Light>(Child(parent, name, pos));
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 3f;
            light.color = color;
        }

        /// <summary>In-scene networked grabbable at scene root (NGO in-scene placed object).</summary>
        static void Item(UnityEngine.SceneManagement.Scene scene, string id, PrimitiveType type, Vector3 pos, Vector3 scale,
            Material mat, float mass, bool heavy)
        {
            var name = "Item_" + id;
            GameObject go = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) go = root;
            if (go == null)
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            }
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = Ensure<Rigidbody>(go);
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = heavy ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;

            var no = Ensure<NetworkObject>(go);
            no.DontDestroyWithOwner = true;
            var nt = Ensure<NetworkTransform>(go);
            nt.AuthorityMode = heavy ? NetworkTransform.AuthorityModes.Server : NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            Ensure<NetworkRigidbody>(go);
            var item = Ensure<GrabbableItem>(go);
            item.Heavy = heavy;
            item.ItemId = id;
        }
    }
}
