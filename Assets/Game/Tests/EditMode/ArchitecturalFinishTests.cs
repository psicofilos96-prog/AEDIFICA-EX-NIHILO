using System;
using System.IO;
using System.Linq;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class ArchitecturalFinishTests
    {
        private static PieceData Wall()
        {
            PieceId id=PieceId.Parse("a0000000000040008000000000000001");
            var wall=new PieceData(id,new PieceTransform(new Vector3(2f,3f,-5f),
                Quaternion.Euler(0f,45f,0f)),new WallDimensions(6f,3.6f,0.36f));
            return wall.WithOpening(new WallOpening(Guid.Parse("a0000000000040008000000000000011"),
                id,WallOpeningKind.Window,1f,1f,1.4f,1.3f));
        }

        private static ArchitecturalFinish WindowFinish(PieceData wall) => new ArchitecturalFinish(
            Guid.Parse("a0000000000040008000000000000021"),wall.Id,wall.Openings[0].Id,
            ArchitecturalFinishKind.WindowFrameSill,LabMaterialIds.Stone);

        [Test]
        public void HostOpeningIdentityAndValidationAreExplicit()
        {
            PieceData wall=Wall();ArchitecturalFinish finish=WindowFinish(wall);
            PieceData framed=wall.WithFinish(finish);
            Assert.That(framed.Finishes.Count,Is.EqualTo(1));
            Assert.That(framed.Finishes[0].Id,Is.EqualTo(finish.Id));
            Assert.That(framed.Finishes[0].HostId,Is.EqualTo(wall.Id));
            Assert.That(framed.Finishes[0].OpeningId,Is.EqualTo(wall.Openings[0].Id));
            var segments=ArchitecturalFinishGeometry.Generate(framed);
            Assert.That(segments.Count,Is.EqualTo(5));
            Assert.That(segments[4].MaterialId,Is.EqualTo(ArchitecturalFinishMaterials.Glass));
            foreach (FinishSegment segment in segments)
            {
                Assert.That(segment.Geometry.Vertices.Length,Is.EqualTo(segment.Geometry.UVs.Length));
                Assert.That(segment.Geometry.Triangles.Length,Is.GreaterThan(0));
                foreach (Vector3 vertex in segment.Geometry.Vertices)
                    Assert.That(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z),Is.False);
            }
            Assert.Throws<ArgumentException>(()=>framed.WithFinish(finish));
            Assert.Throws<ArgumentException>(()=>wall.WithFinish(new ArchitecturalFinish(
                Guid.NewGuid(),PieceIdGenerator.New(),wall.Openings[0].Id,
                ArchitecturalFinishKind.WindowFrameSill,LabMaterialIds.Stone)));
            Assert.Throws<ArgumentException>(()=>wall.WithFinish(new ArchitecturalFinish(
                Guid.NewGuid(),wall.Id,wall.Openings[0].Id,
                ArchitecturalFinishKind.DoorFrame,LabMaterialIds.Stone)));
        }

        [Test]
        public void MovingAndResizingWallRetainsFinishIdAndRecomputesLocalGeometry()
        {
            PieceData wall=Wall().WithFinish(WindowFinish(Wall()));
            var before=ArchitecturalFinishGeometry.Generate(wall);
            PieceData moved=wall.WithTransform(new PieceTransform(
                wall.Transform.Position+new Vector3(4f,1f,-2f),wall.Transform.Rotation));
            var afterMove=ArchitecturalFinishGeometry.Generate(moved);
            Assert.That(moved.Finishes[0].Id,Is.EqualTo(wall.Finishes[0].Id));
            Assert.That(afterMove.Count,Is.EqualTo(before.Count));
            for (int i=0;i<before.Count;i++)
            {
                Vector3 local=before[i].Geometry.Vertices[0];
                Assert.That(Vector3.Distance(local,afterMove[i].Geometry.Vertices[0]),Is.LessThan(0.00001f));
                Vector3 oldWorld=wall.Transform.Position+wall.Transform.Rotation*local;
                Vector3 newWorld=moved.Transform.Position+moved.Transform.Rotation*local;
                Assert.That(Vector3.Distance(newWorld-oldWorld,new Vector3(4f,1f,-2f)),Is.LessThan(0.00001f));
            }
            PieceData resized=moved.WithDimensions(new PieceDimensions(new WallDimensions(7f,4f,0.36f)));
            Assert.That(resized.Finishes[0].Id,Is.EqualTo(wall.Finishes[0].Id));
            Assert.That(ArchitecturalFinishGeometry.Generate(resized).Count,Is.EqualTo(before.Count));
            var world=new ConstructionWorld();
            Assert.That(world.Create(moved).Changed,Is.True);
            Assert.Throws<ArgumentException>(()=>moved.WithDimensions(
                new PieceDimensions(new WallDimensions(1.3f,3.6f,0.36f))));
            Assert.That(world.TryGet(moved.Id,out PieceData afterReject),Is.True);
            Assert.That(afterReject.Finishes[0].Id,Is.EqualTo(moved.Finishes[0].Id));
            Assert.That(afterReject.Dimensions,Is.EqualTo(moved.Dimensions));
        }

        [Test]
        public void OpeningEditRecomputesSillAndRemovingOpeningRemovesOnlyItsDependents()
        {
            PieceData wall=Wall().WithFinish(WindowFinish(Wall()));
            var before=ArchitecturalFinishGeometry.Generate(wall);
            WallOpening opening=wall.Openings[0];
            PieceData changed=wall.ReplaceOpening(opening.WithRect(1.1f,1.2f,1.6f,1.2f));
            var after=ArchitecturalFinishGeometry.Generate(changed);
            Assert.That(changed.Finishes[0].Id,Is.EqualTo(wall.Finishes[0].Id));
            Assert.That(Vector3.Distance(before[0].Geometry.Bounds.center,
                after[0].Geometry.Bounds.center),Is.GreaterThan(0.05f));
            PieceData without=changed.WithoutOpening(opening.Id);
            Assert.That(without.Finishes.Count,Is.Zero);
            Assert.That(ArchitecturalFinishGeometry.Generate(without).Count,Is.Zero);
        }

        [Test]
        public void RemovingOpeningThroughHistoryRemovesFinishAndUndoRestoresBoth()
        {
            PieceData wall=Wall().WithFinish(WindowFinish(Wall()));
            var world=new ConstructionWorld();
            Assert.That(world.Create(wall).Changed,Is.True);
            var history=new ConstructionCommandHistory(world);
            PieceData without=wall.WithoutOpening(wall.Openings[0].Id);
            Assert.That(history.Update(wall.Id,without).Changed,Is.True);
            Assert.That(world.TryGet(wall.Id,out PieceData current),Is.True);
            Assert.That(current.Openings.Count,Is.Zero);
            Assert.That(current.Finishes.Count,Is.Zero);
            Assert.That(history.TryUndo(out _),Is.True);
            Assert.That(world.TryGet(wall.Id,out current),Is.True);
            Assert.That(current.Openings[0].Id,Is.EqualTo(wall.Openings[0].Id));
            Assert.That(current.Finishes[0].Id,Is.EqualTo(wall.Finishes[0].Id));
            Assert.That(history.TryRedo(out _),Is.True);
            Assert.That(world.TryGet(wall.Id,out current),Is.True);
            Assert.That(current.Finishes.Count,Is.Zero);
        }

        [Test]
        public void RoofEaveTracksDimensionsAndCannotAttachToWall()
        {
            PieceId id=PieceId.Parse("a0000000000040008000000000000002");
            var roof=new PieceData(id,new PieceTransform(Vector3.zero,Quaternion.identity),
                new HipRoofDimensions(8f,6f,0.3f,1.4f));
            var finish=new ArchitecturalFinish(Guid.Parse("a0000000000040008000000000000022"),
                id,Guid.Empty,ArchitecturalFinishKind.RoofEave,LabMaterialIds.Stone);
            PieceData decorated=roof.WithFinish(finish);
            var before=ArchitecturalFinishGeometry.Generate(decorated);
            Assert.That(before.Count,Is.EqualTo(4));
            PieceData resized=decorated.WithDimensions(new PieceDimensions(new HipRoofDimensions(10f,7f,0.3f,1.4f)));
            var after=ArchitecturalFinishGeometry.Generate(resized);
            Assert.That(after.Count,Is.EqualTo(4));
            Assert.That(after[0].Geometry.Bounds.size.x,Is.GreaterThan(before[0].Geometry.Bounds.size.x));
            Assert.Throws<ArgumentException>(()=>Wall().WithFinish(new ArchitecturalFinish(
                Guid.NewGuid(),Wall().Id,Guid.Empty,ArchitecturalFinishKind.RoofEave,LabMaterialIds.Stone)));
        }

        [Test]
        public void HistoryAndVersionedSnapshotRestoreDependentIdentities()
        {
            PieceData wall=Wall();var world=new ConstructionWorld();
            Assert.That(world.Create(wall).Changed,Is.True);
            var history=new ConstructionCommandHistory(world);
            PieceData framed=wall.WithFinish(WindowFinish(wall));
            Assert.That(history.Update(wall.Id,framed).Changed,Is.True);
            Assert.That(history.Delete(wall.Id).Changed,Is.True);
            Assert.That(world.TryGet(wall.Id,out _),Is.False);
            Assert.That(history.TryUndo(out _),Is.True);
            Assert.That(world.TryGet(wall.Id,out PieceData restored),Is.True);
            Assert.That(restored.Finishes[0].Id,Is.EqualTo(framed.Finishes[0].Id));
            Assert.That(history.TryUndo(out _),Is.True);
            Assert.That(world.TryGet(wall.Id,out restored),Is.True);
            Assert.That(restored.Finishes.Count,Is.Zero);
            Assert.That(history.TryRedo(out _),Is.True);
            Assert.That(world.TryGet(wall.Id,out restored),Is.True);
            Assert.That(restored.Finishes[0].Id,Is.EqualTo(framed.Finishes[0].Id));
            byte[] snapshot=Pvt2Snapshot.Serialize(world);
            ConstructionWorld loaded=Pvt2Snapshot.Deserialize(snapshot);
            Assert.That(loaded.TryGet(wall.Id,out PieceData loadedWall),Is.True);
            Assert.That(loadedWall.Finishes[0].Equals(framed.Finishes[0]),Is.True);
            Assert.That(Pvt2Snapshot.Matches(loaded,PvtIntegrity.Capture(world)),Is.True);
        }

        [Test]
        public void LegacyVersionOneSnapshotStillLoadsWithNoFinishes()
        {
            var world=new ConstructionWorld();
            PieceData wall=Wall();Assert.That(world.Create(wall).Changed,Is.True);
            byte[] versionTwo=Pvt2Snapshot.Serialize(world);
            // One wall; version 2 adds exactly one trailing empty-finish count.
            byte[] legacy=versionTwo.Take(versionTwo.Length-4).ToArray();
            using (var stream=new MemoryStream(legacy))
            using (var writer=new BinaryWriter(stream))
            { stream.Position=4;writer.Write(1); }
            ConstructionWorld loaded=Pvt2Snapshot.Deserialize(legacy);
            Assert.That(loaded.TryGet(wall.Id,out PieceData restored),Is.True);
            Assert.That(restored.Finishes.Count,Is.Zero);
            Assert.That(restored.Openings.Count,Is.EqualTo(1));
        }
    }
}
