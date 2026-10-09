using System.Collections;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class VisualBenchmarkPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealPieceViewsInstantiateAndCleanUpIndividually()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var registry = new MaterialRegistry(material);
            var root = new GameObject("Visual benchmark test root");
            var world = new ConstructionWorld();
            var views = new List<PieceView>();
            try
            {
                for (int i = 0; i < 8; i++)
                {
                    PieceData piece = VisualBenchmarkScenario.PieceAt(i, VisualBenchmarkScenario.Seed);
                    Assert.That(world.Create(piece).Changed, Is.True);
                    var go = new GameObject("Benchmark piece");
                    go.transform.SetParent(root.transform, false);
                    PieceView view = go.AddComponent<PieceView>();
                    view.Initialize(piece, registry);
                    views.Add(view);
                }
                yield return null;
                Assert.That(world.Count, Is.EqualTo(8));
                Assert.That(views.Count, Is.EqualTo(8));
                foreach (PieceView view in views)
                {
                    Assert.That(view.Id.IsValid, Is.True);
                    Assert.That(view.GetComponent<MeshRenderer>().enabled, Is.True);
                    Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                }
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(material);
            }
            yield return null;
            Assert.That(root == null, Is.True);
        }
    }
}
