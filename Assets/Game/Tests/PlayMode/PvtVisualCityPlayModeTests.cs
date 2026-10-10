using System.Collections;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PvtVisualCityPlayModeTests
    {
        [UnityTest]
        public IEnumerator CityPieceKeepsIdentityThroughPickEditUndoRedoAndSaveLoad()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            var root=new GameObject("Visual city functional gate");
            var world=new ConstructionWorld();
            List<PieceData> city=PvtVisualCityScenario.Build();
            foreach (PieceData piece in city)
                Assert.That(world.Create(piece).Changed,Is.True);
            var engine=new PvtChunkVisualEngine(world,new MaterialRegistry(material,material,
                material,material),root.transform);
            var history=new ConstructionCommandHistory(world);
            try
            {
                Assert.That(engine.RebuildDirty(),Is.GreaterThan(0));
                PieceData target=city[0];
                Physics.SyncTransforms();
                Ray ray=new Ray(target.Transform.Position+Vector3.up*12f,Vector3.down);
                Assert.That(engine.TryPick(ray,30f,out PieceId picked),Is.True);
                Assert.That(world.TryGet(picked,out _),Is.True);
                engine.BeginEdit(target.Id);
                Assert.That(engine.ExtractedCount,Is.EqualTo(1));
                engine.EndEdit(target.Id);
                Assert.That(engine.ExtractedCount,Is.Zero);
                int rebuiltBefore=engine.RebuiltRegions;
                PieceData moved=target.WithTransform(new PieceTransform(
                    target.Transform.Position+new Vector3(1f,0f,0f),target.Transform.Rotation));
                Assert.That(history.Update(target.Id,moved).Changed,Is.True);
                Assert.That(engine.RebuildDirty(),Is.GreaterThan(0));
                Assert.That(engine.RebuiltRegions,Is.GreaterThan(rebuiltBefore));
                PieceData resized=moved.WithDimensions(moved.Dimensions.Resize(0,moved.Dimensions.X+0.5f));
                Assert.That(history.Update(target.Id,resized).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(history.Update(target.Id,resized.WithMaterial(LabMaterialIds.Brick)).Changed,Is.True);
                engine.RebuildDirty();
                PieceData added=new PieceData(PieceId.Parse("33333333333333333333333333333333"),
                    new PieceTransform(new Vector3(-210f,14f,-45f),Quaternion.identity),
                    new BlockDimensions(1f,1f,1f));
                Assert.That(history.Create(added).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(history.Delete(added.Id).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(history.TryUndo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(added.Id,out _),Is.True);
                Assert.That(history.TryRedo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(added.Id,out _),Is.False);
                var snapshot=PvtIntegrity.Capture(world);
                ConstructionWorld loaded=Pvt2Snapshot.Deserialize(Pvt2Snapshot.Serialize(world));
                Assert.That(Pvt2Snapshot.Matches(loaded,snapshot),Is.True);
                Assert.That(engine.PieceCount,Is.EqualTo(world.Count));
            }
            finally { engine.Dispose();Object.Destroy(root);Object.Destroy(material); }
            yield return null;
        }
    }
}
