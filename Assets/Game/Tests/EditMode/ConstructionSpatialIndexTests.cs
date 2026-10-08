using System;
using System.Collections.Generic;
using Aedifica.Construction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class ConstructionSpatialIndexTests
    {
        private static PieceId Id(int n) => PieceId.Parse(n.ToString("x32"));
        private static PieceData Block(int n, Vector3 position, float width = 2f, float depth = 2f, float yaw = 0f)
            => new PieceData(Id(n), new PieceTransform(position, Quaternion.Euler(0f, yaw, 0f)),
                new BlockDimensions(width, 2f, depth));
        private static Bounds Region(Vector3 center, Vector3 size) => new Bounds(center, size);

        [Test]
        public void NegativeCoordinatesAndHalfOpenBoundariesUseOnlyIntersectedChunks()
        {
            var world = new ConstructionWorld(10f);
            var piece = Block(1, new Vector3(-1f, 0f, -1f), 2f, 2f);
            world.Create(piece); // X/Z envelope [-2,0): neither touches cell zero.
            CollectionAssert.AreEquivalent(new[] { new ChunkCoordinate(-1, 0, -1) }, world.SpatialIndex.ChunksFor(piece.Id));
            Assert.That(world.SpatialIndex.Query(Region(new Vector3(-1f, 1f, -1f), Vector3.one)).Count, Is.EqualTo(1));
            Assert.That(world.SpatialIndex.Query(Region(new Vector3(0.5f, 1f, 0.5f), Vector3.one)).Count, Is.Zero);
            var crossing = Block(2, Vector3.zero, 4f, 4f);
            world.Create(crossing);
            Assert.That(world.SpatialIndex.ChunksFor(crossing.Id), Has.Count.EqualTo(4));
            Assert.That(world.SpatialIndex.Query(Region(Vector3.zero, new Vector3(8f, 2f, 8f))),
                Is.EquivalentTo(new[] { piece.Id, crossing.Id }), "A multi-chunk piece appears once.");
        }

        [Test]
        public void RotationResizeAndMovementInvalidateOldAndNewCellsOnly()
        {
            var world = new ConstructionWorld(10f);
            var invalidations = new List<ConstructionInvalidation>();
            world.ChunksInvalidated += invalidations.Add;
            PieceData piece = Block(1, new Vector3(5f, 0f, 5f), 2f, 12f);
            world.Create(piece);
            var initial = new HashSet<ChunkCoordinate>(world.SpatialIndex.ChunksFor(piece.Id));
            Assert.That(initial.Count, Is.EqualTo(3));
            PieceData rotated = Block(1, new Vector3(5f, 0f, 5f), 2f, 12f, 90f);
            world.Update(piece.Id, rotated);
            var rotationCoverage = new HashSet<ChunkCoordinate>(world.SpatialIndex.ChunksFor(piece.Id));
            Assert.That(rotationCoverage.Count, Is.EqualTo(3));
            var expected = new HashSet<ChunkCoordinate>(initial);
            expected.UnionWith(rotationCoverage);
            CollectionAssert.AreEquivalent(expected, invalidations[1].Chunks);
            PieceData resized = Block(1, new Vector3(5f, 0f, 5f), 42f, 12f, 90f);
            world.Update(piece.Id, resized);
            Assert.That(world.SpatialIndex.ChunksFor(piece.Id).Count, Is.GreaterThan(rotationCoverage.Count));
            var beforeMove = new HashSet<ChunkCoordinate>(world.SpatialIndex.ChunksFor(piece.Id));
            PieceData moved = resized.WithTransform(new PieceTransform(new Vector3(105f, 0f, 5f), resized.Transform.Rotation));
            world.Update(piece.Id, moved);
            var afterMove = new HashSet<ChunkCoordinate>(world.SpatialIndex.ChunksFor(piece.Id));
            expected = new HashSet<ChunkCoordinate>(beforeMove);
            expected.UnionWith(afterMove);
            CollectionAssert.AreEquivalent(expected, invalidations[3].Chunks);
            Assert.That(world.SpatialIndex.Query(Region(new Vector3(105f, 1f, 5f), new Vector3(30f, 2f, 30f))),
                Is.EquivalentTo(new[] { piece.Id }));
            Assert.That(world.SpatialIndex.Query(Region(new Vector3(5f, 1f, 5f), new Vector3(10f, 2f, 10f))), Is.Empty);
            world.Delete(piece.Id);
            Assert.That(world.SpatialIndex.IndexedPieceCount, Is.Zero);
            Assert.That(world.SpatialIndex.OccupiedChunkCount, Is.Zero);
        }

        [Test]
        public void RejectedAndNoOpOperationsPreserveIndexAndDoNotInvalidate()
        {
            var world = new ConstructionWorld(10f);
            PieceData piece = Block(1, Vector3.zero);
            world.Create(piece);
            int events = 0;
            world.ChunksInvalidated += _ => events++;
            var before = world.SpatialIndex.ChunksFor(piece.Id);
            Assert.That(world.Update(piece.Id, Block(1, Vector3.zero)).Changed, Is.False);
            Assert.That(world.Create(piece).Changed, Is.False);
            Assert.That(world.Update(Id(2), Block(2, Vector3.one)).Changed, Is.False);
            Assert.That(world.Delete(Id(2)).Changed, Is.False);
            Assert.That(events, Is.Zero);
            CollectionAssert.AreEqual(before, world.SpatialIndex.ChunksFor(piece.Id));
            Assert.That(world.Count, Is.EqualTo(world.SpatialIndex.IndexedPieceCount));
        }

        [Test]
        public void MaterialChangeInvalidatesOnlyOwningChunksNotUnrelatedPieces()
        {
            var world = new ConstructionWorld(10f);
            PieceData near = Block(1, new Vector3(5f, 0f, 5f));
            PieceData far = Block(2, new Vector3(305f, 0f, 305f));
            world.Create(near);
            world.Create(far);
            ConstructionInvalidation affected = default;
            world.ChunksInvalidated += value => affected = value;
            world.Update(near.Id, near.WithMaterial(LabMaterialIds.Stone));
            CollectionAssert.AreEquivalent(world.SpatialIndex.ChunksFor(near.Id), affected.Chunks);
            foreach (ChunkCoordinate distant in world.SpatialIndex.ChunksFor(far.Id))
                Assert.That(affected.Chunks, Does.Not.Contain(distant));
            Assert.That(world.SpatialIndex.IndexedPieceCount, Is.EqualTo(world.Count));
        }

        [Test]
        public void NearbyAndProgressiveWorldQueriesRemainDeduplicatedAndConsistent()
        {
            foreach (int count in new[] { 10, 100, 1000 })
            {
                var world = new ConstructionWorld();
                for (int n = 1; n <= count; n++) world.Create(Block(n, new Vector3(n * 3f, 0f, 0f)));
                Assert.That(world.Count, Is.EqualTo(world.SpatialIndex.IndexedPieceCount));
                Assert.That(world.SpatialIndex.QueryNearby(new Vector3(3f, 1f, 0f), 0.5f),
                    Is.EquivalentTo(new[] { Id(1) }));
                Assert.That(world.SpatialIndex.Query(Region(new Vector3(3f, 1f, 0f), new Vector3(4f, 2f, 4f))),
                    Is.EquivalentTo(new[] { Id(1) }));
                PieceData moved = Block(count, new Vector3(-100f, 0f, -100f));
                world.Update(moved.Id, moved);
                Assert.That(world.SpatialIndex.QueryNearby(new Vector3(-100f, 1f, -100f), 1f),
                    Is.EquivalentTo(new[] { moved.Id }));
                world.Delete(moved.Id);
                Assert.That(world.Count, Is.EqualTo(world.SpatialIndex.IndexedPieceCount));
                Assert.That(world.SpatialIndex.QueryNearby(new Vector3(-100f, 1f, -100f), 1f), Is.Empty);
            }
        }
    }
}
