using BooTrayal.Game.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Builds the networked player prefab (placeholder look until the Blender model in M7).</summary>
    public static class PlayerPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        public static GameObject Build()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Player");

            var cc = Ensure<CharacterController>(root);
            cc.height = PlayerMotor.StandHeight;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, PlayerMotor.StandHeight * 0.5f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            // Default 0.001 drops whole frames of movement at high frame rates (e.g. 3.2 m/s * 0.0003 s).
            cc.minMoveDistance = 0f;

            Ensure<NetworkObject>(root);
            var nt = Ensure<NetworkTransform>(root);
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.Interpolate = true;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            Ensure<PlayerInputReader>(root);
            Ensure<BotInput>(root).enabled = false;
            Ensure<PlayerMotor>(root);
            Ensure<PlayerLook>(root);
            Ensure<PlayerFlashlight>(root);
            Ensure<PlayerSpawnPlacer>(root);
            Ensure<PlayerAvatar>(root);
            Ensure<PlayerHands>(root);
            Ensure<PlayerPush>(root);
            Ensure<PlayerLife>(root);
            Ensure<PlayerNoise>(root);
            Ensure<PlayerActions>(root);
            Ensure<PlayerLobby>(root);
            Ensure<BooTrayal.Game.Fear.FearDirector>(root);

            var body = Lit("PlayerBody", new Color(0.95f, 0.70f, 0.24f));
            var skin = Lit("PlayerSkin", new Color(1f, 0.85f, 0.72f));
            var eyeWhite = Lit("EyeWhite", new Color(0.96f, 0.95f, 1f), 0.5f);
            var eyeBlack = Lit("EyeBlack", new Color(0.07f, 0.04f, 0.09f), 0.6f);

            var visual = Child(root, "Visual", Vector3.zero);
            Primitive(visual, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.75f, 0.7f), body, false);
            Primitive(visual, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.5f, 0f), new Vector3(0.62f, 0.58f, 0.6f), skin, false);
            foreach (var side in new[] { -1f, 1f })
            {
                var tag = side < 0 ? "L" : "R";
                Primitive(visual, "Eye" + tag, PrimitiveType.Sphere, new Vector3(0.12f * side, 1.55f, 0.25f), new Vector3(0.17f, 0.2f, 0.1f), eyeWhite, false);
                Primitive(visual, "Pupil" + tag, PrimitiveType.Sphere, new Vector3(0.12f * side, 1.54f, 0.3f), new Vector3(0.08f, 0.1f, 0.05f), eyeBlack, false);
            }

            root.layer = PlayerLayers.Player;
            var ghostLook = Lit("MiniGhost", new Color(0.86f, 0.92f, 1f), 0.6f);
            var mini = Child(root, "MiniGhostVisual", Vector3.zero);
            Primitive(mini, "Body", PrimitiveType.Sphere, new Vector3(0f, 1.25f, 0f), new Vector3(0.55f, 0.6f, 0.55f), ghostLook, false);
            Primitive(mini, "Tail", PrimitiveType.Capsule, new Vector3(0f, 0.9f, -0.05f), new Vector3(0.35f, 0.3f, 0.35f), ghostLook, false);
            Primitive(mini, "EyeL", PrimitiveType.Sphere, new Vector3(-0.1f, 1.32f, 0.24f), new Vector3(0.09f, 0.13f, 0.06f), eyeBlack, false);
            Primitive(mini, "EyeR", PrimitiveType.Sphere, new Vector3(0.1f, 1.32f, 0.24f), new Vector3(0.09f, 0.13f, 0.06f), eyeBlack, false);
            mini.SetActive(false);

            var head = Child(root, "HeadPivot", new Vector3(0f, PlayerMotor.StandEyeHeight, 0f));
            Child(head, "HoldPoint", new Vector3(0f, -0.25f, 0.8f));
            var flash = Child(head, "Flashlight", new Vector3(0.15f, -0.12f, 0.1f));
            var light = Ensure<Light>(flash);
            light.type = LightType.Spot;
            light.range = 18f;
            light.spotAngle = 55f;
            light.innerSpotAngle = 30f;
            light.intensity = 6f;
            light.color = new Color(1f, 0.93f, 0.8f);
            light.shadows = LightShadows.Soft;
            light.enabled = false;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
            Debug.Log("[PlayerPrefabBuilder] Player prefab ready");
            return saved;
        }
    }
}
