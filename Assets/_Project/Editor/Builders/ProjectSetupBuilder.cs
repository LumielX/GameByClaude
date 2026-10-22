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
