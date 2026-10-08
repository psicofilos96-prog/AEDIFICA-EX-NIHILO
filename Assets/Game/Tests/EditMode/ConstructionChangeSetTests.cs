using System;
using System.Collections.Generic;
using Aedifica.Construction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class ConstructionChangeSetTests
    {
        private static readonly PieceId A = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        private static readonly PieceId B = PieceId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        private static PieceData Block(PieceId id) => new PieceData(id,
            new PieceTransform(new Vector3(1f, 0f, 2f), Quaternion.identity), new BlockDimensions(2f, 3f, 4f));

        [Test]
        public void CreateUpdateAndDeleteReportSnapshotsIdsAndRotatedBounds()
        {
            var world = new ConstructionWorld();
            var events = new List<ConstructionChangeSet>();
            world.Changed += events.Add;
            PieceData original = Block(A);
            ConstructionChangeSet created = world.Create(original);
            Assert.That(created.Operation, Is.EqualTo(ConstructionOperation.Create));
            Assert.That(created.Status, Is.EqualTo(ConstructionChangeStatus.Changed));
            Assert.That(created.PieceId, Is.EqualTo(A));
            Assert.That(created.Before, Is.Null);
            Assert.That(created.After, Is.SameAs(original));
            Assert.That(created.BoundsBefore.HasValue, Is.False);
            Assert.That(created.BoundsAfter.Value.center, Is.EqualTo(new Vector3(1f, 1.5f, 2f)));
            Assert.That(created.BoundsAfter.Value.size, Is.EqualTo(new Vector3(2f, 3f, 4f)));

            PieceData changed = original.WithTransform(new PieceTransform(new Vector3(5f, 0f, 7f),
                Quaternion.Euler(0f, 90f, 0f))).WithDimensions(new PieceDimensions(new BlockDimensions(6f, 2f, 4f)));
            ConstructionChangeSet updated = world.Update(A, changed);
            Assert.That(updated.Operation, Is.EqualTo(ConstructionOperation.Update));
            Assert.That(updated.Before, Is.SameAs(original));
            Assert.That(updated.After, Is.SameAs(changed));
            Assert.That(updated.BoundsBefore.Value.size, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(updated.BoundsAfter.Value.center.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(updated.BoundsAfter.Value.center.z, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(updated.BoundsAfter.Value.size.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(updated.BoundsAfter.Value.size.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(updated.BoundsAfter.Value.size.z, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(changed.Id, Is.EqualTo(original.Id));
            Assert.That(world.TryGet(A, out PieceData found), Is.True);
            Assert.That(found, Is.SameAs(changed));

            ConstructionChangeSet deleted = world.Delete(A);
            Assert.That(deleted.Operation, Is.EqualTo(ConstructionOperation.Delete));
            Assert.That(deleted.Before, Is.SameAs(changed));
            Assert.That(deleted.After, Is.Null);
            Assert.That(deleted.BoundsAfter.HasValue, Is.False);
            Assert.That(deleted.BoundsBefore.HasValue, Is.True);
            Assert.That(world.Count, Is.Zero);
            Assert.That(events, Has.Count.EqualTo(3));
        }

        [Test]
        public void NoChangeAndRejectedMutationsLeaveStateAndEventsUntouched()
        {
            var world = new ConstructionWorld();
            PieceData original = Block(A);
            Assert.That(world.Add(original), Is.True);
            int notifications = 0;
            world.Changed += _ => notifications++;
            PieceData equivalent = Block(A);
            ConstructionChangeSet noChange = world.Update(A, equivalent);
            Assert.That(noChange.Status, Is.EqualTo(ConstructionChangeStatus.NoChange));
            Assert.That(noChange.Before, Is.SameAs(original));
            Assert.That(noChange.After, Is.SameAs(original));
            Assert.That(world.Replace(A, equivalent), Is.True, "The existing bool API accepts a valid no-op.");
            Assert.That(world.Create(equivalent).Status, Is.EqualTo(ConstructionChangeStatus.Rejected));
            Assert.That(world.Update(B, Block(B)).Status, Is.EqualTo(ConstructionChangeStatus.Rejected));
            Assert.That(world.Update(A, Block(B)).Status, Is.EqualTo(ConstructionChangeStatus.Rejected));
            Assert.That(world.Update(A, new PieceData(A, original.Transform,
                new WallDimensions(2f, 3f, 0.2f))).Status, Is.EqualTo(ConstructionChangeStatus.Rejected));
            Assert.That(world.Delete(B).Status, Is.EqualTo(ConstructionChangeStatus.Rejected));
            Assert.That(world.TryGet(A, out PieceData found), Is.True);
            Assert.That(found, Is.SameAs(original));
            Assert.That(world.Count, Is.EqualTo(1));
            Assert.That(notifications, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => world.Create(null));
            Assert.Throws<ArgumentNullException>(() => world.Update(A, null));
        }

        [Test]
        public void MaterialAndWallOpeningsAreEffectiveChanges()
        {
            var world = new ConstructionWorld();
            PieceData wall = new PieceData(A,
                new PieceTransform(Vector3.zero, Quaternion.identity), new WallDimensions(6f, 3f, 0.3f));
            world.Create(wall);
            Assert.That(world.Update(A, wall.WithMaterial(LabMaterialIds.Stone)).Changed, Is.True);
            PieceData materialWall = wall.WithMaterial(LabMaterialIds.Stone);
            var opening = new WallOpening(Guid.Parse("11111111-1111-1111-1111-111111111111"), A,
                WallOpeningKind.Passage, 1f, 0f, 1.2f, 2f);
            PieceData opened = materialWall.WithOpening(opening);
            Assert.That(world.Update(A, opened).Changed, Is.True);
            Assert.That(world.Update(A, opened.WithOpening(new WallOpening(Guid.NewGuid(), A,
                WallOpeningKind.Window, 4f, 1f, 1f, 1f))).Changed, Is.True);
        }

        [Test]
        public void PositionRotationAndDimensionsEachProduceOneEffectiveChange()
        {
            var world = new ConstructionWorld();
            PieceData original = Block(A);
            world.Create(original);
            PieceData moved = original.WithTransform(new PieceTransform(new Vector3(9f, 0f, 2f), Quaternion.identity));
            ConstructionChangeSet move = world.Update(A, moved);
            Assert.That(move.Changed, Is.True);
            Assert.That(move.BoundsBefore.Value.center.x, Is.EqualTo(1f));
            Assert.That(move.BoundsAfter.Value.center.x, Is.EqualTo(9f));
            PieceData rotated = moved.WithTransform(new PieceTransform(moved.Transform.Position,
                Quaternion.Euler(0f, 90f, 0f)));
            ConstructionChangeSet rotate = world.Update(A, rotated);
            Assert.That(rotate.Changed, Is.True);
            Assert.That(rotate.BoundsBefore.Value.size.x, Is.EqualTo(2f));
            Assert.That(rotate.BoundsAfter.Value.size.x, Is.EqualTo(4f).Within(0.0001f));
            PieceData resized = rotated.WithDimensions(new PieceDimensions(new BlockDimensions(8f, 3f, 4f)));
            ConstructionChangeSet resize = world.Update(A, resized);
            Assert.That(resize.Changed, Is.True);
            Assert.That(resize.BoundsAfter.Value.size.z, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(world.Count, Is.EqualTo(1));
            Assert.That(resized.Id, Is.EqualTo(original.Id));
        }
    }
}
