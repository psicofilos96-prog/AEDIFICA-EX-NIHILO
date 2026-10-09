using System;
using System.Collections.Generic;
using System.Linq;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class SpatialStressLabTests
    {
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(50000)]
        public void FixedSeedProducesUniqueStableIdsAndArchitecturalPieces(int count)
        {
            PieceData[] first = SpatialStressScenario.Generate(count, SpatialStressScenario.Seed).ToArray();
            PieceData[] second = SpatialStressScenario.Generate(count, SpatialStressScenario.Seed).ToArray();
            Assert.That(first, Has.Length.EqualTo(count));
            Assert.That(first.Select(piece => piece.Id).Distinct().Count(), Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(first[i].Id, Is.EqualTo(second[i].Id));
                Assert.That(first[i].Transform.Equals(second[i].Transform), Is.True);
                Assert.That(first[i].Type, Is.EqualTo(second[i].Type));
            }
            Assert.That(first.Any(piece => piece.Transform.Position.x < 0f), Is.True);
            Assert.That(first.Any(piece => piece.Transform.Position.x > 0f), Is.True);
            Assert.That(first.Any(piece => piece.Transform.Rotation.eulerAngles.y > 0f), Is.True);
            Assert.That(first.Any(piece => piece.Dimensions.X > SpatialStressScenario.ChunkMeters), Is.True);
        }

        [Test]
        public void ReferenceQueriesMatchAfterUpdatesDeletionAndHistoryReplay()
        {
            PieceData[] pieces = SpatialStressScenario.Generate(1000, SpatialStressScenario.Seed).ToArray();
            var world = new ConstructionWorld(SpatialStressScenario.ChunkMeters);
            var reference = new Dictionary<PieceId, PieceData>();
            foreach (PieceData piece in pieces)
            {
                Assert.That(world.Create(piece).Changed, Is.True);
                reference.Add(piece.Id, piece);
            }
            Assert.That(world.SpatialIndex.ChunksFor(pieces[0].Id).Count, Is.GreaterThan(1));
            Assert.That(world.SpatialIndex.ChunksFor(pieces[0].Id).Any(chunk => chunk.X < 0 || chunk.Z < 0), Is.True);
            SpatialStressScenario.Validate(world, reference);
            var history = new ConstructionCommandHistory(world);
            PieceData initial = pieces[0];
            var before = new HashSet<ChunkCoordinate>(world.SpatialIndex.ChunksFor(initial.Id));
            ConstructionInvalidation? invalidated = null;
            world.ChunksInvalidated += change => invalidated = change;
            PieceData moved = initial.WithTransform(new PieceTransform(initial.Transform.Position + new Vector3(65f, 0f, 65f),
                Quaternion.Euler(0f, 45f, 0f)));
            Assert.That(history.Update(initial.Id, moved).Changed, Is.True);
            reference[initial.Id] = moved;
            before.UnionWith(world.SpatialIndex.ChunksFor(initial.Id));
            Assert.That(invalidated.HasValue, Is.True);
            CollectionAssert.AreEquivalent(before, invalidated.Value.Chunks);
            SpatialStressScenario.Validate(world, reference);
            Assert.That(history.Delete(initial.Id).Changed, Is.True);
            reference.Remove(initial.Id);
            Assert.That(world.SpatialIndex.ChunksFor(initial.Id), Is.Empty);
            SpatialStressScenario.Validate(world, reference);
            Assert.That(history.TryUndo(out _), Is.True);
            reference.Add(initial.Id, moved);
            SpatialStressScenario.Validate(world, reference);
            Assert.That(history.TryUndo(out _), Is.True);
            reference[initial.Id] = initial;
            SpatialStressScenario.Validate(world, reference);
            Assert.That(history.TryRedo(out _), Is.True);
            reference[initial.Id] = moved;
            SpatialStressScenario.Validate(world, reference);
        }

        [Test]
        public void PercentilesUseNearestRankOnSortedPerOperationSamples()
        {
            double[] samples = { 1d, 2d, 3d, 4d, 5d };
            Assert.That(SpatialStressLabRunner.Percentile(samples, 0.5d), Is.EqualTo(3d));
            Assert.That(SpatialStressLabRunner.Percentile(samples, 0.95d), Is.EqualTo(5d));
            Assert.That(SpatialStressLabRunner.Percentile(samples, 0.99d), Is.EqualTo(5d));
            Assert.Throws<ArgumentException>(() => SpatialStressLabRunner.Percentile(Array.Empty<double>(), 0.5d));
        }
    }
}
