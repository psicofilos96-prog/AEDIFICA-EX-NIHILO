using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class VisualChunkBenchmarkTests
    {
        [Test]
        public void CoverageUsesHalfOpenCellsAcrossNegativeBoundariesAndLongPieces()
        {
            using (var chunks = new VisualChunkManager(10f))
            {
                CollectionAssert.AreEquivalent(new[] { new ChunkCoordinate(-1, 0, 0) },
                    chunks.CoveredCells(new Bounds(new Vector3(-5f, 0f, 5f), new Vector3(10f, 2f, 10f))));
                CollectionAssert.AreEquivalent(new[] {
                    new ChunkCoordinate(-2, 0, -1), new ChunkCoordinate(-1, 0, -1),
                    new ChunkCoordinate(0, 0, -1), new ChunkCoordinate(1, 0, -1)
                }, chunks.CoveredCells(new Bounds(new Vector3(0f, 0f, -5f), new Vector3(40f, 2f, 10f))));
                CollectionAssert.AreEquivalent(new[] { new ChunkCoordinate(0, 0, 0) },
                    chunks.CoveredCells(new Bounds(new Vector3(5f, 0f, 5f), new Vector3(10f, 2f, 10f))));
            }
        }

        [Test]
        public void RotatedPieceUsesFullWorldBoundsAcrossChunkEdges()
        {
            PieceData piece = new PieceData(PieceId.Parse("33333333333333333333333333333333"),
                new PieceTransform(new Vector3(-9f, 0f, -9f), Quaternion.Euler(0f, 45f, 0f)),
                new BlockDimensions(28f, 2f, 2f));
            Bounds bounds = ConstructionChangeSet.WorldBounds(piece);
            using (var chunks = new VisualChunkManager(10f))
            {
                IReadOnlyList<ChunkCoordinate> coverage = chunks.CoveredCells(bounds);
                Assert.That(coverage.Count, Is.GreaterThan(4));
                Assert.That(coverage, Does.Contain(new ChunkCoordinate(-2, 0, -2)));
                Assert.That(coverage, Does.Contain(new ChunkCoordinate(0, 0, 0)));
            }
        }

        [Test]
        public void LayoutOrderingBalancesEachModeAndCsvRequiresAllMetrics()
        {
            var firstPositions = new HashSet<string>();
            for (int repeat = 1; repeat <= 3; repeat++)
            {
                string[] order = VisualChunkBenchmarkModes.Layouts(repeat);
                CollectionAssert.AreEquivalent(new[] { "baseline", "chunked_no_culling", "chunked_culling" }, order);
                firstPositions.Add(order[0]);
            }
            Assert.That(firstPositions.Count, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "fixed", "path", "overview" }, VisualChunkBenchmarkModes.Cameras);
            Assert.That(VisualChunkCsv.Columns, Is.EqualTo(VisualBenchmarkCsv.Columns + 10));
            var fields = new string[VisualChunkCsv.Columns];
            fields[0] = "a,\"b\"";
            StringAssert.StartsWith("\"a,\"\"b\"\"\"", VisualChunkCsv.Row(fields));
            Assert.Throws<ArgumentException>(() => VisualChunkCsv.Row("missing"));
        }

        [Test]
        public void WorkloadIdsAndGeometryAreIdenticalToE2c1AtEveryScenarioBoundary()
        {
            var ids = new HashSet<PieceId>();
            for (int i = 0; i < VisualBenchmarkScenario.Sizes[4]; i++)
            {
                PieceData piece = VisualBenchmarkScenario.PieceAt(i, VisualBenchmarkScenario.Seed);
                Assert.That(ids.Add(piece.Id), Is.True, $"duplicate index={i}");
                Assert.That(piece.Dimensions.IsValid, Is.True, $"invalid index={i}");
            }
            Assert.That(ids.Count, Is.EqualTo(50000));
        }
    }
}
