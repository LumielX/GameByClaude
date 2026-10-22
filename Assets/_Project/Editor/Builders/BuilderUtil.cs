using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Idempotent helpers shared by the builders: reuse existing objects/assets by name instead of recreating them.</summary>
    public static class BuilderUtil
    {
        public const string MaterialsFolder = "Assets/_Project/Art/Materials";

        public static Scene OpenOrCreateScene(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>
        /// Gives in-scene NetworkObjects their GlobalObjectIdHash. NGO computes it in an internal OnValidate that
        /// needs the scene to be an imported asset, and batchmode does not reliably call it, so we call it ourselves.
        /// Throws if any hash is still 0 (a build with 0 hashes cannot sync in-scene objects).
        /// </summary>
        public static void ResaveScene(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var onValidate = typeof(Unity.Netcode.NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var objects = Object.FindObjectsByType<Unity.Netcode.NetworkObject>(FindObjectsSortMode.None);
            foreach (var no in objects)
            {
                onValidate?.Invoke(no, null);
                EditorUtility.SetDirty(no);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            foreach (var no in objects)
                if (no.PrefabIdHash == 0)
                    throw new System.InvalidOperationException($"{no.name} in {path} has GlobalObjectIdHash 0");
        }

        public static GameObject Root(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        public static GameObject Child(GameObject parent, string name, Vector3 localPos)
        {
            var t = parent.transform.Find(name);
            var go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            return go;
        }

        /// <summary>A primitive child; <paramref name="keepCollider"/> false strips the collider (visual only).</summary>
        public static GameObject Primitive(GameObject parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale,
            Material material, bool keepCollider, Vector3 euler = default)
        {
            var t = parent != null ? parent.transform.Find(name) : null;
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                if (parent != null) go.transform.SetParent(parent.transform, false);
            }
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (!keepCollider && col != null) Object.DestroyImmediate(col);
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static T Ensure<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
        }

        public static Material Lit(string name, Color color, float smoothness = 0.15f)
        {
            System.IO.Directory.CreateDirectory(MaterialsFolder);
            var path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            bool changed = false;
            if (mat.GetColor("_BaseColor") != color) { mat.SetColor("_BaseColor", color); changed = true; }
            // URP later mirrors _BaseColor into legacy _Color via a linear/gamma round trip; pre-set that exact value
            // so the next builder run is a no-op.
            var legacy = color.linear.gamma;
            if (mat.GetColor("_Color") != legacy) { mat.SetColor("_Color", legacy); changed = true; }
            if (!Mathf.Approximately(mat.GetFloat("_Smoothness"), smoothness)) { mat.SetFloat("_Smoothness", smoothness); changed = true; }
            if (changed || created)
            {
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssetIfDirty(mat);
                // Re-import so URP's material postprocessor syncs legacy properties (_Color) now, not on the next run.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            return mat;
        }
    }
}
