using BooTrayal.Game.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Builds the network prefabs. Run: -executeMethod BooTrayal.Editor.Builders.NetworkPrefabBuilder.Build</summary>
    public static class NetworkPrefabBuilder
    {
        public const string Folder = "Assets/_Project/Prefabs/Network";
        public const string PlayerStubPath = Folder + "/NetPlayerStub.prefab";
        public const string NetworkManagerPath = Folder + "/NetworkManager.prefab";
        public const string PrefabListPath = "Assets/DefaultNetworkPrefabs.asset";
        public const int TickRate = 30;

        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Folder);
            var stub = EnsurePrefab(PlayerStubPath, "NetPlayerStub", go =>
            {
                Ensure<NetworkObject>(go);
                Ensure<NetPlayerStub>(go);
            });

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            if (!list.Contains(stub))
            {
                list.Add(new NetworkPrefab { Prefab = stub });
                EditorUtility.SetDirty(list);
            }

            EnsurePrefab(NetworkManagerPath, "NetworkManager", go =>
            {
                var transport = Ensure<UnityTransport>(go);
                var nm = Ensure<NetworkManager>(go);
                nm.NetworkConfig ??= new NetworkConfig();
                nm.NetworkConfig.NetworkTransport = transport;
                nm.NetworkConfig.TickRate = TickRate;
                nm.NetworkConfig.ConnectionApproval = true;
                nm.NetworkConfig.PlayerPrefab = stub;
                if (!nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
                    nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            });

            AssetDatabase.SaveAssets();
            Debug.Log("[NetworkPrefabBuilder] Network prefabs ready");
        }

        static GameObject EnsurePrefab(string path, string name, System.Action<GameObject> configure)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            configure(root);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
            return saved;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
        }
    }
}
