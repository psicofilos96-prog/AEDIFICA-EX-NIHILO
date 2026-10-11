using System.Collections;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PvtGrandVillageScalePlayModeTests
    {
        [UnityTest]
        public IEnumerator ThousandPieceTierRemainsSelectableEditableAndReversible()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var root = new GameObject("Scale lab visual test");
            var world = new ConstructionWorld();
            List<PieceData> template = PvtGrandVillageScenario.Build();
            for (int i = 0; i < 1000; i++)
                Assert.That(world.Create(PvtGrandVillageScaleScenario.PieceAt(template, i)).Changed, Is.True);
            var registry = new MaterialRegistry(material, material, material, material);
            registry.Register(PvtFinalScenario.Wood, material);
            registry.Register(PvtFinalScenario.Ceramic, material);
            registry.Register(PvtVisualCityScenario.Glass, material);
            var engine = new PvtChunkVisualEngine(world, registry, root.transform);
            try
            {
                engine.RebuildDirty();
                Assert.That(engine.PieceCount, Is.EqualTo(1000));
                PieceData piece = PvtGrandVillageScaleScenario.PieceAt(template, template.Count);
                Assert.That(world.TryGet(piece.Id, out _), Is.True);
                var history = new ConstructionCommandHistory(world);
                engine.BeginEdit(piece.Id);
                engine.EndEdit(piece.Id);
                PieceData moved = piece.WithTransform(new PieceTransform(
                    piece.Transform.Position + Vector3.right, piece.Transform.Rotation));
                Assert.That(history.Update(piece.Id, moved).Changed, Is.True);
                engine.RebuildDirty();
                Assert.That(history.TryUndo(out _), Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(piece.Id, out PieceData restored), Is.True);
                Assert.That(restored.Transform.Position, Is.EqualTo(piece.Transform.Position));
                Assert.That(engine.PieceCount, Is.EqualTo(1000));
            }
            finally { engine.Dispose(); Object.Destroy(root); Object.Destroy(material); }
            yield return null;
        }
    }
}
