using BooTrayal.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>
    /// The runtime UI setup (M6): PanelSettings with Unity's default runtime theme, and the UIDocument + UIRoot on the
    /// persistent Services object with every screen template, the theme and the Kanit fonts assigned.
    /// </summary>
    public static class UIBuilder
    {
        public const string Folder = "Assets/_Project/UI";
        public const string PanelPath = Folder + "/BooPanelSettings.asset";
        public const string RuntimeThemePath = Folder + "/BooRuntimeTheme.tss";
        const string Screens = Folder + "/Screens/";

        public static void Wire(GameObject services)
        {
            // IMGUI debug menus from M2/M5 are gone; drop their components (or their missing-script husks).
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(services);

            var panel = EnsurePanel();
            var doc = Ensure<UIDocument>(services);
            doc.panelSettings = panel;
            doc.sortingOrder = 10;

            var ui = Ensure<UIRoot>(services);
            ui.Panel = panel;
            ui.Theme = Load<StyleSheet>(Folder + "/Theme.uss");
            ui.Regular = Load<Font>(Folder + "/Fonts/Kanit-Regular.ttf");
            ui.SemiBold = Load<Font>(Folder + "/Fonts/Kanit-SemiBold.ttf");
            ui.Heavy = Load<Font>(Folder + "/Fonts/Kanit-ExtraBold.ttf");
            ui.TitleUxml = Uxml("Title");
            ui.PlayUxml = Uxml("Play");
            ui.CreateUxml = Uxml("CreateRoom");
            ui.SettingsUxml = Uxml("Settings");
            ui.HowToUxml = Uxml("HowTo");
            ui.ErrorUxml = Uxml("Error");
            ui.LobbyUxml = Uxml("Lobby", optional: true);
            ui.HudUxml = Uxml("Hud", optional: true);
            ui.VoteUxml = Uxml("Vote", optional: true);
            ui.SummaryUxml = Uxml("Summary", optional: true);
            ui.PauseUxml = Uxml("Pause", optional: true);
            EditorUtility.SetDirty(ui);
        }

        static PanelSettings EnsurePanel()
        {
            if (!System.IO.File.Exists(RuntimeThemePath))
            {
                System.IO.File.WriteAllText(RuntimeThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(RuntimeThemePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = Load<ThemeStyleSheet>(RuntimeThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssetIfDirty(panel);
            return panel;
        }

        static VisualTreeAsset Uxml(string name, bool optional = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Screens + name + ".uxml");
            if (asset == null && !optional) throw new System.InvalidOperationException($"Missing {Screens}{name}.uxml");
            return asset;
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new System.InvalidOperationException($"Missing {path}");
            return asset;
        }
    }
}
