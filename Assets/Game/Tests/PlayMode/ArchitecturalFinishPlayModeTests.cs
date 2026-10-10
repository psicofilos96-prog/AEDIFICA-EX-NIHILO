using System;
using System.Collections;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class ArchitecturalFinishPlayModeTests
    {
        [UnityTest]
        public IEnumerator LinkedFinishFollowsHostAndOnlyItsRegionRebuilds()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            var root=new GameObject("finish functional gate");
            PieceId id=PieceId.Parse("a0000000000040008000000000000101");
            Guid openingId=Guid.Parse("a0000000000040008000000000000111");
            var wall=new PieceData(id,new PieceTransform(new Vector3(5f,0f,5f),Quaternion.identity),
                new WallDimensions(5f,3f,0.3f));
            wall=wall.WithOpening(new WallOpening(openingId,id,WallOpeningKind.Passage,1.5f,0f,1.2f,2.1f));
            Guid finishId=Guid.Parse("a0000000000040008000000000000121");
            wall=wall.WithFinish(new ArchitecturalFinish(finishId,id,openingId,
                ArchitecturalFinishKind.DoorFrame,LabMaterialIds.Stone));
            var world=new ConstructionWorld();
            Assert.That(world.Create(wall).Changed,Is.True);
            var engine=new PvtChunkVisualEngine(world,
                new MaterialRegistry(material,material,material,material),root.transform);
            var history=new ConstructionCommandHistory(world);
            try
            {
                Assert.That(engine.RebuildDirty(),Is.EqualTo(1));
                int regions=engine.RebuiltRegions;
                PieceView view=engine.BeginEdit(id);
                Assert.That(view.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(4),
                    "Wall plus three linked door-frame segments must be visible while selected.");
                Vector3 before=view.transform.position;
                PieceData moved=wall.WithTransform(new PieceTransform(before+Vector3.right,
                    Quaternion.Euler(0f,30f,0f)));
                Assert.That(history.Update(id,moved).Changed,Is.True);
                engine.RebuildDirty();
                Assert.That(view.transform.position,Is.EqualTo(moved.Transform.Position));
                Assert.That(view.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(4));
                Assert.That(world.TryGet(id,out PieceData current),Is.True);
                Assert.That(current.Finishes[0].Id,Is.EqualTo(finishId));
                engine.EndEdit(id);
                Assert.That(engine.RebuiltRegions-regions,Is.LessThanOrEqualTo(4),
                    "One host edit must not rebuild the entire reference world.");
                Assert.That(history.TryUndo(out _),Is.True);
                engine.RebuildDirty();
                Assert.That(world.TryGet(id,out current),Is.True);
                Assert.That(current.Transform.Position,Is.EqualTo(before));
                Assert.That(current.Finishes[0].Id,Is.EqualTo(finishId));
                Assert.That(Pvt2Snapshot.Deserialize(Pvt2Snapshot.Serialize(world))
                    .TryGet(id,out PieceData loaded),Is.True);
                Assert.That(loaded.Finishes[0].Id,Is.EqualTo(finishId));
            }
            finally { engine.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(material); }
            yield return null;
        }
    }
}
