using System;
using Aedifica.Construction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class ConstructionCommandHistoryTests
    {
        private static PieceId Id(int n) => PieceId.Parse(n.ToString("x32"));
        private static PieceData Block(int n, float x = 0f) => new PieceData(Id(n),
            new PieceTransform(new Vector3(x, 0f, 0f), Quaternion.identity), new BlockDimensions(2f, 2f, 2f));

        [Test]
        public void UndoRedoCreateUpdateDeleteRestoreStateAndSpatialIndex()
        {
            var world = new ConstructionWorld(10f);
            var history = new ConstructionCommandHistory(world);
            PieceData original = Block(1);
            PieceData moved = original.WithTransform(new PieceTransform(new Vector3(25f, 0f, 0f), Quaternion.Euler(0f, 45f, 0f)))
                .WithDimensions(new PieceDimensions(new BlockDimensions(4f, 3f, 2f)))
                .WithMaterial(LabMaterialIds.Stone);
            Assert.That(history.Create(original).Changed, Is.True);
            Assert.That(history.Update(original.Id, moved).Changed, Is.True);
            Assert.That(history.Delete(original.Id).Changed, Is.True);
            Assert.That(world.Count, Is.Zero);
            Assert.That(history.TryUndo(out _), Is.True);
            Assert.That(world.TryGet(original.Id, out PieceData found), Is.True);
            Assert.That(found, Is.SameAs(moved));
            Assert.That(world.SpatialIndex.QueryNearby(new Vector3(25f, 1f, 0f), 1f), Is.EquivalentTo(new[] { original.Id }));
            Assert.That(history.TryUndo(out _), Is.True);
            Assert.That(world.TryGet(original.Id, out found), Is.True);
            Assert.That(found, Is.SameAs(original));
            Assert.That(world.SpatialIndex.QueryNearby(new Vector3(0f, 1f, 0f), 1f), Is.EquivalentTo(new[] { original.Id }));
            Assert.That(history.TryUndo(out _), Is.True);
            Assert.That(world.Count, Is.Zero);
            Assert.That(world.SpatialIndex.IndexedPieceCount, Is.Zero);
            for (int n = 0; n < 3; n++) Assert.That(history.TryRedo(out _), Is.True);
            Assert.That(world.Count, Is.Zero);
            Assert.That(world.SpatialIndex.OccupiedChunkCount, Is.Zero);
            Assert.That(history.CanRedo, Is.False);
        }

        [Test]
        public void WallOpeningAndMaterialSnapshotsSurviveUndoRedo()
        {
            var world = new ConstructionWorld();
            var history = new ConstructionCommandHistory(world);
            PieceData wall = new PieceData(Id(1), new PieceTransform(Vector3.zero, Quaternion.identity),
                new WallDimensions(6f, 3f, 0.3f));
            var opening = new WallOpening(Guid.NewGuid(), wall.Id, WallOpeningKind.Passage, 1f, 0f, 1.2f, 2f);
            PieceData changed = wall.WithOpening(opening).WithMaterial(LabMaterialIds.Brick);
            history.Create(wall);
            history.Update(wall.Id, changed);
            Assert.That(history.TryUndo(out _), Is.True);
            Assert.That(world.TryGet(wall.Id, out PieceData found), Is.True);
            Assert.That(found.Openings, Is.Empty);
            Assert.That(found.MaterialId, Is.EqualTo(wall.MaterialId));
            Assert.That(history.TryRedo(out _), Is.True);
            Assert.That(world.TryGet(wall.Id, out found), Is.True);
            Assert.That(found.Openings, Has.Count.EqualTo(1));
            Assert.That(found.Openings[0].Id, Is.EqualTo(opening.Id));
            Assert.That(found.MaterialId, Is.EqualTo(LabMaterialIds.Brick));
        }

        [Test]
        public void NoOpsRejectionsBranchingAndCapacityDoNotPolluteHistory()
        {
            var world = new ConstructionWorld();
            var history = new ConstructionCommandHistory(world, 2);
            PieceData a = Block(1);
            history.Create(a);
            Assert.That(history.Update(a.Id, Block(1)).Changed, Is.False);
            Assert.That(history.Delete(Id(2)).Changed, Is.False);
            Assert.That(history.Create(a).Changed, Is.False);
            Assert.That(history.UndoCount, Is.EqualTo(1));
            history.Update(a.Id, Block(1, 10f));
            Assert.That(history.TryUndo(out _), Is.True);
            Assert.That(history.RedoCount, Is.EqualTo(1));
            history.Update(a.Id, Block(1, 20f));
            Assert.That(history.CanRedo, Is.False);
            history.Update(a.Id, Block(1, 30f));
            Assert.That(history.UndoCount, Is.EqualTo(2));
            Assert.That(history.TryUndo(out _), Is.True);
            Assert.That(history.TryUndo(out _), Is.True); // oldest create was evicted; two updates remain reversible
            Assert.That(history.TryUndo(out _), Is.False);
            Assert.That(world.TryGet(a.Id, out PieceData found), Is.True);
            Assert.That(found.Transform.Position.x, Is.EqualTo(0f));
        }

        [Test]
        public void ExternalMutationBlocksConflictingReplayWithoutChangingState()
        {
            var world = new ConstructionWorld();
            var history = new ConstructionCommandHistory(world);
            PieceData a = Block(1);
            history.Create(a);
            PieceData external = Block(1, 50f);
            world.Update(a.Id, external);
            Assert.That(history.TryUndo(out _), Is.False);
            Assert.That(history.UndoCount, Is.EqualTo(1));
            Assert.That(world.TryGet(a.Id, out PieceData found), Is.True);
            Assert.That(found, Is.SameAs(external));
            Assert.That(world.SpatialIndex.QueryNearby(new Vector3(50f, 1f, 0f), 1f), Is.EquivalentTo(new[] { a.Id }));
        }
    }
}
