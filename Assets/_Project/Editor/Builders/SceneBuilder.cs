using BooTrayal.Game;
using BooTrayal.Game.Dev;
using BooTrayal.Game.Flow;
using BooTrayal.Game.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Builds scenes from code. Builders are idempotent: existing objects are reused, not recreated.</summary>
    public static class SceneBuilder
    {
        public const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string MenuPath = "Assets/_Project/Scenes/Menu.unity";

        /// <summary>Run: -executeMethod BooTrayal.Editor.Builders.SceneBuilder.BuildAll (needs NetworkPrefabBuilder.Build first).</summary>
        public static void BuildAll()
        {
            BuildBootstrap();
            BuildMenu();
            SandboxBuilder.Build();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(SandboxBuilder.ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] All scenes ready");
        }

        public static void BuildBootstrap()
        {
            var scene = OpenOrCreateScene(BootstrapPath);
            Camera(scene, "Main Camera");

            var boot = Root(scene, "Bootstrap");
            Ensure<Bootstrap>(boot);

            EnsurePrefabInstance(scene, "NetworkManager", NetworkPrefabBuilder.NetworkManagerPath);
            var services = Root(scene, "Services");
            Ensure<ConnectionService>(services);
            Ensure<DebugNetMenu>(services);
            Ensure<SessionFlow>(services);
            Ensure<SmokeProbe>(services);

            EditorSceneManager.SaveScene(scene, BootstrapPath);
        }

        public static void BuildMenu()
        {
            var scene = OpenOrCreateScene(MenuPath);
            Camera(scene, "MenuCamera");
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        static void Camera(Scene scene, string name)
        {
            var cam = Root(scene, name);
            cam.tag = "MainCamera";
            var camera = Ensure<Camera>(cam);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.02f, 0.07f);
            Ensure<AudioListener>(cam);
        }

        static void EnsurePrefabInstance(Scene scene, string name, string prefabPath)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new System.InvalidOperationException($"Missing prefab {prefabPath}; run NetworkPrefabBuilder.Build first");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
        }
    }
}
