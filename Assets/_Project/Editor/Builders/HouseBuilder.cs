using BooTrayal.Game.Fear;
using BooTrayal.Game.Flow;
using BooTrayal.Game.Ghosts;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>
    /// Graybox Thai stilt house (GDD 9): open dark ใต้ถุน under a raised floor at 2.6 m, bedroom / shrine room /
    /// terrace / kitchen upstairs, front and back stairs (chase loops), yard with ศาลพระภูมิ, fence and escape gate.
    /// Bakes the NavMesh from the Environment colliders.
    /// </summary>
    public static class HouseBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/House.unity";
        public const string NavMeshPath = "Assets/_Project/Scenes/House/NavMesh-House.asset";
        public const float FloorY = 2.6f;
        const float WallH = 2.4f;
        const float WallT = 0.2f;
        const float Door = 1.6f;
        static readonly float RampAngle = Mathf.Atan2(FloorY, 5f) * Mathf.Rad2Deg;
        static readonly float RampLength = Mathf.Sqrt(FloorY * FloorY + 25f);

        static Material _wall, _floor, _rail;

        public static void Build()
        {
            var scene = OpenOrCreateScene(ScenePath);
            var ground = Lit("Ground", new Color(0.16f, 0.13f, 0.10f));
            _floor = Lit("HouseFloor", new Color(0.40f, 0.26f, 0.16f));
            _wall = Lit("HouseWall", new Color(0.47f, 0.31f, 0.19f));
            _rail = Lit("HouseRail", new Color(0.33f, 0.21f, 0.13f));
            var roof = Lit("HouseRoof", new Color(0.24f, 0.12f, 0.10f));
            var pillar = Lit("HousePillar", new Color(0.28f, 0.19f, 0.13f));
            var fence = Lit("Fence", new Color(0.30f, 0.27f, 0.22f));
            var shrine = Lit("SpiritHouse", new Color(0.80f, 0.62f, 0.25f), 0.5f);
            var leaf = Lit("TreeLeaf", new Color(0.10f, 0.22f, 0.12f));
            var clutter = Lit("Prop", new Color(0.55f, 0.42f, 0.30f));
            var potMat = Lit("ItemPot", new Color(0.62f, 0.33f, 0.22f));
            var urnMat = Lit("ItemUrn", new Color(0.78f, 0.62f, 0.25f), 0.5f);

            var env = Root(scene, "Environment");
            Primitive(env, "Ground", PrimitiveType.Cube, new Vector3(0f, -0.25f, -1f), new Vector3(60f, 0.5f, 50f), ground, true);

            // Fence (1.6 m, not jumpable) with the escape gate in the south side. The gate is closed in M4; M5 opens it.
            Primitive(env, "FenceN", PrimitiveType.Cube, new Vector3(0f, 0.8f, 20f), new Vector3(50f, 1.6f, WallT), fence, true);
            Primitive(env, "FenceE", PrimitiveType.Cube, new Vector3(25f, 0.8f, -1f), new Vector3(WallT, 1.6f, 42f), fence, true);
            Primitive(env, "FenceW", PrimitiveType.Cube, new Vector3(-25f, 0.8f, -1f), new Vector3(WallT, 1.6f, 42f), fence, true);
            Primitive(env, "FenceSW", PrimitiveType.Cube, new Vector3(-13.25f, 0.8f, -22f), new Vector3(23.5f, 1.6f, WallT), fence, true);
            Primitive(env, "FenceSE", PrimitiveType.Cube, new Vector3(13.25f, 0.8f, -22f), new Vector3(23.5f, 1.6f, WallT), fence, true);
            Primitive(env, "EscapeGate", PrimitiveType.Cube, new Vector3(0f, 0.8f, -22f), new Vector3(3f, 1.6f, 0.1f), _rail, true);

            // Raised floor and the space under it (ใต้ถุน).
            var house = Child(env, "House", Vector3.zero);
            Primitive(house, "Floor", PrimitiveType.Cube, new Vector3(0f, FloorY - 0.1f, 0f), new Vector3(20f, 0.2f, 10f), _floor, true);
            int p = 0;
            foreach (var x in new[] { -9f, -3f, 3f, 9f })
                foreach (var z in new[] { -4.5f, 0f, 4.5f })
                    Primitive(house, $"Pillar{p++}", PrimitiveType.Cylinder, new Vector3(x, (FloorY - 0.2f) * 0.5f, z),
                        new Vector3(0.4f, (FloorY - 0.2f) * 0.5f, 0.4f), pillar, true);
            Primitive(house, "Crate1", PrimitiveType.Cube, new Vector3(-6f, 0.4f, 2f), Vector3.one * 0.8f, clutter, true);
            Primitive(house, "Crate2", PrimitiveType.Cube, new Vector3(6f, 0.5f, -2f), new Vector3(1.2f, 1f, 1f), clutter, true);
            Primitive(house, "Loom", PrimitiveType.Cube, new Vector3(-1.5f, 0.6f, -3f), new Vector3(2f, 1.2f, 0.6f), clutter, true);

            // West block: bedroom (north) and shrine room (south), doors onto the terrace.
            WallX(house, "WestOuter", -10f, -5f, 5f);
            WallZ(house, "WestNorth", 5f, -10f, -3f);
            WallZ(house, "WestSouth", -5f, -10f, -3f);
            WallZ(house, "BedShrineDivider", 0f, -10f, -3f);
            WallX(house, "WestToTerrace", -3f, -5f, 5f, -2.5f, 2.5f);
            // East block: kitchen, door onto the terrace and a back door to the back stairs.
            WallX(house, "EastOuter", 10f, -5f, 5f, 3f);
            WallZ(house, "EastNorth", 5f, 3f, 10f);
            WallZ(house, "EastSouth", -5f, 3f, 10f);
            WallX(house, "EastToTerrace", 3f, -5f, 5f, 0f);
            // Terrace (ชาน) railings; the south railing has the front-stairs gap.
            Rail(house, "RailN", new Vector3(0f, FloorY + 0.45f, 5f), new Vector3(6f, 0.9f, WallT));
            Rail(house, "RailSW", new Vector3(-2f, FloorY + 0.45f, -5f), new Vector3(2f, 0.9f, WallT));
            Rail(house, "RailSE", new Vector3(2f, FloorY + 0.45f, -5f), new Vector3(2f, 0.9f, WallT));

            // Stairs as ramps (rise 2.6 over 5 m): front from the terrace to the yard, back from the kitchen.
            Primitive(house, "StairsFront", PrimitiveType.Cube, new Vector3(0f, FloorY * 0.5f - 0.1f, -7.5f),
                new Vector3(2f, 0.2f, RampLength), _floor, true, new Vector3(-RampAngle, 0f, 0f));
            Primitive(house, "StairsBack", PrimitiveType.Cube, new Vector3(12.5f, FloorY * 0.5f - 0.1f, 3f),
                new Vector3(RampLength, 0.2f, 2f), _floor, true, new Vector3(0f, 0f, -RampAngle));

            // Steep gable roofs over the two blocks (visual only).
            var roofs = Child(house, "Roofs", Vector3.zero);
            foreach (var (name, cx) in new[] { ("West", -6.5f), ("East", 6.5f) })
            {
                float slope = Mathf.Atan2(2.5f, 5.5f) * Mathf.Rad2Deg;
                float len = Mathf.Sqrt(2.5f * 2.5f + 5.5f * 5.5f);
                float y = FloorY + WallH + 1.25f;
                Primitive(roofs, name + "N", PrimitiveType.Cube, new Vector3(cx, y, 2.75f), new Vector3(8f, 0.15f, len), roof, false, new Vector3(slope, 0f, 0f));
                Primitive(roofs, name + "S", PrimitiveType.Cube, new Vector3(cx, y, -2.75f), new Vector3(8f, 0.15f, len), roof, false, new Vector3(-slope, 0f, 0f));
            }

            // Yard: ศาลพระภูมิ (meeting / revive point in M5) and a few trees.
            var yard = Child(env, "Yard", Vector3.zero);
            var spirit = Child(yard, "SpiritHouse", new Vector3(0f, 0f, -15f));
            Primitive(spirit, "Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 0.6f, 0.3f), pillar, true);
            Primitive(spirit, "Shrine", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0f), new Vector3(0.8f, 0.6f, 0.7f), shrine, true);
            Primitive(spirit, "ShrineRoof", PrimitiveType.Cube, new Vector3(0f, 1.9f, 0f), new Vector3(0.7f, 0.7f, 0.9f), roof, false, new Vector3(0f, 0f, 45f));
            int t = 0;
            foreach (var pos in new[] { new Vector3(-18f, 0f, -12f), new Vector3(18f, 0f, -14f), new Vector3(-17f, 0f, 12f), new Vector3(19f, 0f, 10f) })
            {
                var tree = Child(yard, $"Tree{t++}", pos);
                Primitive(tree, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 1.5f, 0.5f), pillar, true);
                Primitive(tree, "Crown", PrimitiveType.Sphere, new Vector3(0f, 3.8f, 0f), new Vector3(3.2f, 2.6f, 3.2f), leaf, false);
            }

            var spawns = Root(scene, "SpawnPoints");
            for (int i = 0; i < 8; i++)
            {
                var sp = Child(spawns, $"Spawn{i}", new Vector3(-5.25f + 1.5f * i, 0f, -20f));
                sp.transform.rotation = Quaternion.identity;
                Ensure<SpawnPoint>(sp).Index = i;
            }

            var ghosts = Root(scene, "Ghosts");
            var ghostSpawn = Child(ghosts, "GhostSpawn", new Vector3(0f, 0f, 3f));
            ghostSpawn.transform.rotation = Quaternion.identity;
            Ensure<Unity.Netcode.NetworkObject>(ghostSpawn);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GhostPrefabBuilder.PrefabPath);
            if (prefab == null) throw new System.InvalidOperationException($"Missing {GhostPrefabBuilder.PrefabPath}; run NetworkPrefabBuilder.Build first");
            Ensure<GhostSpawner>(ghostSpawn).Prefab = prefab;
            var points = new[]
            {
                new Vector3(-6f, 0f, -1f), new Vector3(6f, 0f, 1f), new Vector3(0f, 0f, 4f),
                new Vector3(-6.5f, FloorY, 2.5f), new Vector3(-6.5f, FloorY, -2.5f), new Vector3(6.5f, FloorY, -1f),
                new Vector3(0f, FloorY, 0f), new Vector3(-15f, 0f, 8f)
            };
            for (int i = 0; i < points.Length; i++) Ensure<GhostWaypoint>(Child(ghosts, $"Waypoint{i}", points[i])).Index = i;

            Item(scene, "pot1", PrimitiveType.Cylinder, new Vector3(4f, 0.3f, -12f), new Vector3(0.4f, 0.25f, 0.4f), potMat, 2f, heavy: false);
            Item(scene, "pot2", PrimitiveType.Cylinder, new Vector3(6f, FloorY + 0.3f, 2f), new Vector3(0.4f, 0.25f, 0.4f), potMat, 2f, heavy: false);
            Item(scene, "pot3", PrimitiveType.Cylinder, new Vector3(-6f, FloorY + 0.3f, 3f), new Vector3(0.4f, 0.25f, 0.4f), potMat, 2f, heavy: false);
            Item(scene, "urn", PrimitiveType.Cylinder, new Vector3(0f, FloorY + 0.6f, 2f), new Vector3(0.9f, 0.6f, 0.9f), urnMat, 60f, heavy: true);

            var lights = Root(scene, "Lights");
            var moon = Ensure<Light>(Child(lights, "Moon", new Vector3(0f, 20f, 0f)));
            moon.type = LightType.Directional;
            moon.intensity = 0.3f;
            moon.color = new Color(0.62f, 0.7f, 1f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            Lamp(lights, "Lantern", new Vector3(0f, 2.2f, -15f), new Color(0.3f, 1f, 0.55f), 8f, 2.5f, false);
            Lamp(lights, "LampBedroom", new Vector3(-6.5f, FloorY + 2f, 2.5f), new Color(0.65f, 0.35f, 1f), 7f, 2f, true);
            Lamp(lights, "LampShrine", new Vector3(-6.5f, FloorY + 2f, -2.5f), new Color(1f, 0.6f, 0.3f), 7f, 2f, true);
            Lamp(lights, "LampKitchen", new Vector3(6.5f, FloorY + 2f, 0f), new Color(0.65f, 0.35f, 1f), 8f, 2f, true);
            Lamp(lights, "LampUnder", new Vector3(0f, 2f, 0f), new Color(0.3f, 1f, 0.55f), 9f, 1.2f, true);

            BakeNavMesh(env);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ResaveScene(ScenePath);
            var tri = NavMesh.CalculateTriangulation();
            CheckReachable(new Vector3(-5.25f, 0f, -20f), points);
            Debug.Log($"[HouseBuilder] House scene ready triangles={tri.indices.Length / 3}");
        }

        /// <summary>Fails the build if any ghost waypoint cannot be reached from the yard (broken stairs/doorways).</summary>
        static void CheckReachable(Vector3 from, Vector3[] targets)
        {
            if (!NavMesh.SamplePosition(from, out var start, 1.5f, NavMesh.AllAreas))
                throw new System.InvalidOperationException($"No NavMesh near {from}");
            var path = new NavMeshPath();
            foreach (var target in targets)
            {
                if (!NavMesh.SamplePosition(target, out var end, 1.5f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    throw new System.InvalidOperationException($"NavMesh cannot reach {target} from {from} ({path.status})");
            }
        }

        /// <summary>A wall along z at fixed x, with door gaps (1.6 m) centred on <paramref name="doors"/>.</summary>
        static void WallX(GameObject parent, string name, float x, float z0, float z1, params float[] doors)
        {
            int i = 0;
            foreach (var (a, b) in Segments(z0, z1, doors))
                Primitive(parent, $"{name}{i++}", PrimitiveType.Cube, new Vector3(x, FloorY + WallH * 0.5f, (a + b) * 0.5f),
                    new Vector3(WallT, WallH, b - a), _wall, true);
        }

        /// <summary>A wall along x at fixed z, with door gaps.</summary>
        static void WallZ(GameObject parent, string name, float z, float x0, float x1, params float[] doors)
        {
            int i = 0;
            foreach (var (a, b) in Segments(x0, x1, doors))
                Primitive(parent, $"{name}{i++}", PrimitiveType.Cube, new Vector3((a + b) * 0.5f, FloorY + WallH * 0.5f, z),
                    new Vector3(b - a, WallH, WallT), _wall, true);
        }

        static System.Collections.Generic.IEnumerable<(float, float)> Segments(float from, float to, float[] doors)
        {
            float start = from;
            foreach (var d in doors)
            {
                yield return (start, d - Door * 0.5f);
                start = d + Door * 0.5f;
            }
            yield return (start, to);
        }

        static void Rail(GameObject parent, string name, Vector3 pos, Vector3 size) =>
            Primitive(parent, name, PrimitiveType.Cube, pos, size, _rail, true);

        static void Lamp(GameObject parent, string name, Vector3 pos, Color color, float range, float intensity, bool flicker)
        {
            var go = Child(parent, name, pos);
            var light = Ensure<Light>(go);
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            if (flicker) Ensure<FlickerLight>(go).BaseIntensity = intensity;
        }

        /// <summary>Bakes from the Environment colliders (items and spawns live outside it) into a fixed asset path.</summary>
        static void BakeNavMesh(GameObject env)
        {
            var surface = Ensure<NavMeshSurface>(env);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.agentTypeID = 0; // Humanoid: radius 0.5, height 2, step 0.75, slope 45
            surface.BuildNavMesh();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(NavMeshPath));
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            }
            else
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.RemoveData();
                surface.navMeshData = existing;
                surface.AddData();
                EditorUtility.SetDirty(existing);
            }
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(surface);
        }
    }
}
