using System.Collections;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PvtFreeAssetsReferencePlayModeTests
    {
        [UnityTest]
        public IEnumerator ReferencePieceCanBePickedEditedUndoneAndRestoredFromSnapshot()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            var root=new GameObject("Free asset reference functional gate");
            var world=new ConstructionWorld();
            List<PieceData> residence=PvtVisualCityScenario.BuildReferenceResidence();
            foreach (PieceData piece in residence) Assert.That(world.Create(piece).Changed,Is.True);
            var engine=new PvtChunkVisualEngine(world,
                new MaterialRegistry(material,material,material,material),root.transform);
            var history=new ConstructionCommandHistory(world);
            try
            {
                Assert.That(engine.RebuildDirty(),Is.GreaterThan(0));
                PieceData target=residence[0];
                Physics.SyncTransforms();
                Ray ray=new Ray(target.Transform.Position+Vector3.up*12f,Vector3.down);
                Assert.That(engine.TryPick(ray,30f,out PieceId picked),Is.True);
                Assert.That(world.TryGet(picked,out _),Is.True);
                engine.BeginEdit(target.Id);
                Assert.That(engine.ExtractedCount,Is.EqualTo(1));
                engine.EndEdit(target.Id);
                Assert.That(engine.ExtractedCount,Is.Zero);
                PieceData moved=target.WithTransform(new PieceTransform(
                    target.Transform.Position+Vector3.right,target.Transform.Rotation));
                Assert.That(history.Update(target.Id,moved).Changed,Is.True);
                Assert.That(engine.RebuildDirty(),Is.GreaterThan(0));
                PieceData resized=moved.WithDimensions(moved.Dimensions.Resize(0,moved.Dimensions.X+0.5f));
                Assert.That(history.Update(target.Id,resized).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(history.Update(target.Id,resized.WithMaterial(LabMaterialIds.Brick)).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(history.Delete(target.Id).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(target.Id,out _),Is.False);
                Assert.That(history.TryUndo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(target.Id,out _),Is.True);
                Assert.That(history.TryRedo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(target.Id,out _),Is.False);
                ConstructionWorld loaded=Pvt2Snapshot.Deserialize(Pvt2Snapshot.Serialize(world));
                Assert.That(Pvt2Snapshot.Matches(loaded,PvtIntegrity.Capture(world)),Is.True);
                Assert.That(engine.PieceCount,Is.EqualTo(world.Count));
            }
            finally { engine.Dispose();Object.Destroy(root);Object.Destroy(material); }
            yield return null;
        }
    }
}
