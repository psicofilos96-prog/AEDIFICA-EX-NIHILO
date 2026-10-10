using System;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aedifica.Tests.EditMode
{
    public sealed class Pvt2MaterialReferencesTests
    {
        private static readonly string[] Names = { "Terrain", "Bark", "Foliage", "Grass", "Road", "Water" };

        [Test]
        public void PersistentEnvironmentMaterialsUseSupportedUrpShaders()
        {
            var materials = new Material[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = "Assets/Game/Materials/Pvt2" + Names[i] + ".mat";
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(materials[i], Is.Not.Null, path);
                Assert.That(materials[i].shader, Is.Not.Null, path);
                Assert.That(materials[i].shader.name,
                    Is.EqualTo(i == 0 ? "Universal Render Pipeline/Terrain/Lit" : "Universal Render Pipeline/Lit"), path);
                Assert.That(materials[i].shader.isSupported, Is.True, path);
            }
            Assert.DoesNotThrow(() => Pvt2Environment.ValidateMaterials(materials[0], materials[1],
                materials[2], materials[3], materials[4], materials[5]));
        }

        [Test]
        public void InvalidEnvironmentMaterialFailsClearly()
        {
            Material lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Pvt2Bark.mat");
            Assert.That(() => Pvt2Environment.ValidateMaterials(null, lit, lit, lit, lit, lit),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("terrain"));
            Assert.That(() => Pvt2Environment.ValidateMaterials(lit, lit, lit, lit, lit, lit),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("Terrain/Lit"));
        }

        [Test]
        public void PersistentSurfaceMaterialsRetainPbrValues()
        {
            var colors = new[] {
                new Color(0.28f,0.18f,0.1f), new Color(0.16f,0.31f,0.12f),
                new Color(0.24f,0.38f,0.13f), new Color(0.36f,0.32f,0.25f),
                new Color(0.11f,0.25f,0.31f) };
            var smoothness = new[] { 0.05f, 0.08f, 0.02f, 0.04f, 0.75f };
            for (int i = 1; i < Names.Length; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Game/Materials/Pvt2" + Names[i] + ".mat");
                Color actual = material.GetColor("_BaseColor");
                Color expected = colors[i-1];
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.00001f), Names[i] + " R");
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.00001f), Names[i] + " G");
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.00001f), Names[i] + " B");
                Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.00001f), Names[i] + " A");
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(smoothness[i-1]).Within(0.00001f));
                Assert.That(material.enableInstancing, Is.True);
            }
        }

        [Test]
        public void WindowsDefaultPipelineReferencesPcUrpAsset()
        {
            var pc = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Assert.That(pc, Is.Not.Null);
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.EqualTo(pc));
        }
    }
}
