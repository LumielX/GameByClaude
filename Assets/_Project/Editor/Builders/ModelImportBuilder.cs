using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BooTrayal.Editor.Builders
{
    /// <summary>
    /// Imports the Blender-built models (tools/blender/build_models.py): metre scale with the axis conversion baked in,
    /// every material slot remapped to a BooTrayal/Toon material of the same name, the player's actions split into
    /// looping clips, and the player's AnimatorController. Run before the prefab and scene builders.
    /// </summary>
    public static class ModelImportBuilder
    {
        public const string Folder = "Assets/_Project/Art/Models";
        public const string ControllerPath = Folder + "/PlayerAnimator.controller";
        public const string ToonFolder = "Assets/_Project/Art/Materials/Toon";
        public static readonly string[] ModelNames = { "Player", "Krasue", "Urn", "IncensePot", "ClayPot", "Bell", "SpiritHouse" };
        static readonly (string Name, bool Loop)[] PlayerClips = { ("Idle", true), ("Walk", true), ("Run", true), ("Carry", true), ("Fall", false) };

        /// <summary>Same palette as build_models.py PALETTE (its Blender preview colours).</summary>
        static readonly Dictionary<string, string> Palette = new Dictionary<string, string>
        {
            { "Body", "#F2B33D" }, { "Skin", "#FFD9B8" }, { "Hair", "#2A1B14" }, { "EyeWhite", "#F5F2FF" }, { "Pupil", "#120A16" }, { "Shoe", "#3A2E52" },
            { "KrasueSkin", "#EADFD6" }, { "KrasueHair", "#100A14" }, { "KrasueEye", "#E0213A" }, { "Glow", "#39FF88" },
            { "Urn", "#C79A3E" }, { "Gold", "#E8C25A" }, { "Clay", "#9E5536" }, { "Incense", "#6E8F4A" }, { "Stick", "#B5452E" }, { "Ember", "#FF8A3D" },
            { "Bell", "#D9A93A" }, { "Shrine", "#CDA04A" }, { "Roof", "#7A2E2A" }, { "Pillar", "#5A3A26" }
        };
        static readonly HashSet<string> Emissive = new HashSet<string> { "Glow", "KrasueEye", "Ember" };

        public static void Build()
        {
            foreach (var name in ModelNames) Import(name);
            EnsureController();
            AssetDatabase.SaveAssets();
            Debug.Log("[ModelImportBuilder] Models imported");
        }

        public static string ModelPath(string name) => $"{Folder}/{name}.fbx";

        public static GameObject Model(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name));
            if (go == null) throw new System.InvalidOperationException($"Missing {ModelPath(name)}; run tools/blender/build-models.ps1");
            return go;
        }

        static void Import(string name)
        {
            var path = ModelPath(name);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new System.InvalidOperationException($"Missing {path}; run tools/blender/build-models.ps1");
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.isReadable = false;

            bool player = name == "Player";
            importer.animationType = player ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = player;
            if (player)
            {
                var available = importer.defaultClipAnimations;
                importer.clipAnimations = PlayerClips.Select(c =>
                {
                    var src = available.FirstOrDefault(a => a.takeName.EndsWith(c.Name)) ?? throw new System.InvalidOperationException($"Player.fbx has no {c.Name} action");
                    src.name = c.Name;
                    src.loopTime = c.Loop;
                    return src;
                }).ToArray();
            }

            // First import creates the embedded materials; remap every slot to our toon material of the same name.
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).Distinct().ToList())
            {
                var toon = Toon(mat);
                if (toon != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat), toon);
            }
            foreach (var slot in Palette.Keys) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), Toon(slot));
            importer.SaveAndReimport();
        }

        /// <summary>A BooTrayal/Toon material for a palette slot (created once, colours re-applied every run).</summary>
        public static Material Toon(string slot)
        {
            if (!Palette.TryGetValue(slot, out var hex)) return null;
            System.IO.Directory.CreateDirectory(ToonFolder);
            var path = $"{ToonFolder}/Toon_{slot}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("BooTrayal/Toon"));
                AssetDatabase.CreateAsset(mat, path);
            }
            ColorUtility.TryParseHtmlString(hex, out var color);
            bool changed = false;
            if (mat.GetColor("_BaseColor") != color) { mat.SetColor("_BaseColor", color); changed = true; }
            var emission = Emissive.Contains(slot) ? color * 1.4f : Color.black;
            if (mat.GetColor("_EmissionColor") != emission) { mat.SetColor("_EmissionColor", emission); changed = true; }
            if (changed)
            {
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssetIfDirty(mat);
            }
            return mat;
        }

        /// <summary>Built once (an AnimatorController rebuilt every run would get new file ids and dirty git).</summary>
        static void EnsureController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) return;
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath("Player")).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carry", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Fall", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;

            var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 3.2f);
            tree.AddChild(clips["Run"], 5.6f);
            var locomotion = sm.AddState("Locomotion");
            locomotion.motion = tree;
            sm.defaultState = locomotion;

            var carry = sm.AddState("Carry");
            carry.motion = clips["Carry"];
            var fall = sm.AddState("Fall");
            fall.motion = clips["Fall"];

            void To(AnimatorState from, AnimatorState to, string param, bool value)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = false;
                t.duration = 0.12f;
                t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
            }
            To(locomotion, carry, "Carry", true);
            To(carry, locomotion, "Carry", false);
            To(locomotion, fall, "Fall", true);
            To(carry, fall, "Fall", true);
            To(fall, locomotion, "Fall", false);
            EditorUtility.SetDirty(controller);
        }
    }
}
