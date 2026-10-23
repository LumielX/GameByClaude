using BooTrayal.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BooTrayal.Editor.Builders
{
    /// <summary>Applies all player/project settings from code. Run: -executeMethod BooTrayal.Editor.Builders.ProjectSetupBuilder.Apply</summary>
    public static class ProjectSetupBuilder
    {
        const int InputSystemOnly = 1;

        /// <summary>
        /// The Humanoid NavMesh agent climbs only what the player's CharacterController can step (0.4 m), so paths
        /// (bots, ghost) never go up the side of a ramp a player cannot climb.
        /// </summary>
        static void SetNavMeshAgentClimb(float climb)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var first = so.FindProperty("m_Settings").GetArrayElementAtIndex(0);
            var prop = first.FindPropertyRelative("agentClimb");
            if (Mathf.Approximately(prop.floatValue, climb)) return;
            prop.floatValue = climb;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        public static void Apply()
        {
            PlayerSettings.companyName = "LumielX";
            PlayerSettings.productName = "BOO-TRAYAL";
            PlayerSettings.bundleVersion = GameVersion.Current;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            // Several game instances on one PC must keep simulating while unfocused.
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            SetNavMeshAgentClimb(0.4f);
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SetActiveInputHandler(InputSystemOnly);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ProjectSetupBuilder] Applied settings for v{GameVersion.Current}");
        }

        static void SetActiveInputHandler(int value)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0) return;
            var so = new SerializedObject(settings[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == value) return;
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
