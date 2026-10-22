using BooTrayal.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Builds scenes from code. Builders are idempotent: existing objects are reused, not recreated.</summary>
    public static class SceneBuilder
    {
        public const string BootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";

        public static void BuildBootstrap()
        {
            var scene = OpenOrCreate(BootstrapPath);

            var cam = FindOrCreate(scene, "Main Camera");
            cam.tag = "MainCamera";
            var camera = GetOrAdd<Camera>(cam);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.02f, 0.07f);
            GetOrAdd<AudioListener>(cam);

            var boot = FindOrCreate(scene, "Bootstrap");
            GetOrAdd<Bootstrap>(boot);

            EditorSceneManager.SaveScene(scene, BootstrapPath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapPath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Bootstrap scene ready");
        }

        static Scene OpenOrCreate(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static GameObject FindOrCreate(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
        }
    }
}
