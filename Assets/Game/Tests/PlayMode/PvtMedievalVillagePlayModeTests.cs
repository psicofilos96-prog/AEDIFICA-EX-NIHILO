using System.Collections;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PvtMedievalVillagePlayModeTests
    {
        [UnityTest]
        public IEnumerator CastleWallFinishAndMarketCanBeEditedRebuiltAndRestored()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            var root=new GameObject("Castle village functional gate");
            var world=new ConstructionWorld();
            List<PieceData> pieces=PvtMedievalVillageScenario.Build();
            foreach (PieceData piece in pieces)
                Assert.That(world.Create(piece).Changed,Is.True);
            var registry=new MaterialRegistry(material,material,material,material);
            registry.Register(PvtFinalScenario.Wood,material);
            registry.Register(PvtFinalScenario.Ceramic,material);
            registry.Register(PvtVisualCityScenario.Glass,material);
            var engine=new PvtChunkVisualEngine(world,registry,root.transform);
            var history=new ConstructionCommandHistory(world);
            try
            {
                Assert.That(engine.RebuildDirty(),Is.GreaterThan(0));
                PieceData gate=null;
                foreach (PieceData piece in pieces)
                    if (piece.Type==PieceType.Wall && piece.Openings.Count>=3 &&
                        Mathf.Abs(piece.Transform.Position.z-6f)<0.1f)
                    { gate=piece;break; }
                Assert.That(gate,Is.Not.Null);
                Assert.That(gate.Finishes.Count,Is.GreaterThan(0));
                int before=engine.RebuiltRegions;
                PieceData moved=gate.WithTransform(new PieceTransform(
                    gate.Transform.Position+Vector3.right,gate.Transform.Rotation));
                Assert.That(history.Update(gate.Id,moved).Changed,Is.True);
                int regions=engine.RebuildDirty();
                Assert.That(regions,Is.GreaterThan(0));
                Assert.That(regions,Is.LessThan(8),"Only affected castle regions may rebuild.");
                Assert.That(engine.RebuiltRegions,Is.EqualTo(before+regions));
                Assert.That(world.TryGet(gate.Id,out PieceData current),Is.True);
                Assert.That(current.Finishes[0].Id,Is.EqualTo(gate.Finishes[0].Id));
                Assert.That(history.TryUndo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(gate.Id,out current),Is.True);
                Assert.That(current.Transform.Position,Is.EqualTo(gate.Transform.Position));
                Assert.That(history.TryRedo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(history.Delete(gate.Id).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(gate.Id,out _),Is.False);
                Assert.That(history.TryUndo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(gate.Id,out current),Is.True);
                Assert.That(current.Finishes.Count,Is.EqualTo(gate.Finishes.Count));
                ConstructionWorld loaded=Pvt2Snapshot.Deserialize(Pvt2Snapshot.Serialize(world));
                Assert.That(Pvt2Snapshot.Matches(loaded,PvtIntegrity.Capture(world)),Is.True);
                Assert.That(engine.PieceCount,Is.EqualTo(world.Count));
            }
            finally { engine.Dispose();Object.Destroy(root);Object.Destroy(material); }
            yield return null;
        }
    }
}
