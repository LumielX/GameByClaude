using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BooTrayal.Tests
{
    public class ModelBudgetTests
    {
        const string Models = "Assets/_Project/Art/Models/";

        static readonly Dictionary<string, int> Budget = new Dictionary<string, int>
        {
            { "Player", 3000 }, { "Krasue", 2500 }, { "Urn", 800 }, { "IncensePot", 800 }, { "ClayPot", 800 }, { "Bell", 800 }, { "SpiritHouse", 1500 }
        };

        [Test]
        public void Models_WithinTrisBudget()
        {
            foreach (var kv in Budget)
            {
                var meshes = AssetDatabase.LoadAllAssetsAtPath(Models + kv.Key + ".fbx").OfType<Mesh>().ToList();
                Assert.IsNotEmpty(meshes, $"{kv.Key}.fbx has no mesh");
                int tris = meshes.Sum(m => m.triangles.Length / 3);
                Assert.LessOrEqual(tris, kv.Value, $"{kv.Key}: {tris} tris");
            }
        }

        [Test]
        public void PlayerModel_FacesPlusZ_FeetAtOrigin()
        {
            // Skinned vertices live in the rig's space; bake the bind pose and look at it from the model root.
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Models + "Player.fbx"));
            try
            {
                var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.IsNotNull(smr, "the player is skinned");
                int pupil = System.Array.FindIndex(smr.sharedMaterials, m => m != null && m.name.Contains("Pupil"));
                Assert.GreaterOrEqual(pupil, 0, "a Pupil material slot exists");
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var world = baked.vertices.Select(v => go.transform.InverseTransformPoint(smr.transform.TransformPoint(v))).ToArray();
                float z = baked.GetIndices(pupil).Select(i => world[i].z).Average();
                Assert.Greater(z, 0.1f, "the eyes are on the +Z side (Unity forward)");
                Assert.AreEqual(0f, world.Min(v => v.y), 0.05f, "feet at the origin");
                Assert.AreEqual(1.7f, world.Max(v => v.y), 0.15f, "about 1.7 m tall");
                Object.DestroyImmediate(baked);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PlayerClips_NamedAndLooped()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Models + "Player.fbx");
            var clips = importer.clipAnimations.ToDictionary(c => c.name, c => c.loopTime);
            foreach (var name in new[] { "Idle", "Walk", "Run", "Carry" }) Assert.IsTrue(clips.TryGetValue(name, out var loop) && loop, $"{name} loops");
            Assert.IsTrue(clips.ContainsKey("Fall") && !clips["Fall"], "Fall plays once");
        }

        [Test]
        public void PlayerAnimator_HasAllStates()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Models + "PlayerAnimator.controller");
            Assert.IsNotNull(controller);
            var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Locomotion", "Carry", "Fall" }, states);
            var parameters = controller.parameters.Select(p => p.name).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Speed", "Carry", "Fall" }, parameters);
        }

        [Test]
        public void HouseModels_StandUpright()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/House.unity");
            try
            {
                // Each placed model is taller than it is wide (pots, urn, incense, bell, spirit house).
                foreach (var path in new[] { "Item_pot1/Model", "Item_urn/Model", "Item_incense/Model", "ShrineBell/Bell/Model", "Environment/Yard/SpiritHouse/Model" })
                {
                    var root = path.Split('/')[0];
                    var top = scene.GetRootGameObjects().First(g => g.name == root).transform;
                    var model = path.Contains("/") ? top.Find(path.Substring(root.Length + 1)) : top;
                    Assert.IsNotNull(model, path);
                    var renderers = model.GetComponentsInChildren<Renderer>();
                    Assert.IsNotEmpty(renderers, $"{path} has no renderer");
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    Assert.Greater(b.size.y, 0.9f * Mathf.Max(b.size.x, b.size.z), $"{path} is lying down: {b.size}");
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene); }
        }

        [Test]
        public void ToonShader_Compiles()
        {
            var shader = Shader.Find("BooTrayal/Toon");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "BooTrayal/Toon has compile errors");
        }

        [Test]
        public void ToonShader_HasOwnDepthAndShadowPasses()
        {
            // SSAO and the depth texture need DepthNormals/DepthOnly; the SRP Batcher needs every pass to share the
            // Toon UnityPerMaterial buffer, so none of them may be borrowed from Lit with UsePass.
            var shader = Shader.Find("BooTrayal/Toon");
            var modes = Enumerable.Range(0, shader.passCount)
                .Select(i => shader.FindPassTagValue(i, new UnityEngine.Rendering.ShaderTagId("LightMode")).name.ToUpperInvariant()).ToList();
            // Tag ids are interned engine-wide, so their spelling can come back in another case.
            CollectionAssert.IsSubsetOf(new[] { "UNIVERSALFORWARD", "SRPDEFAULTUNLIT", "SHADOWCASTER", "DEPTHONLY", "DEPTHNORMALS" }, modes);
            var source = System.IO.File.ReadAllText("Assets/_Project/Art/Shaders/Toon.shader");
            StringAssert.DoesNotContain("UsePass", source);
        }
    }
}
