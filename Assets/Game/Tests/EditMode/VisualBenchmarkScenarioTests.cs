using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class VisualBenchmarkScenarioTests
    {
        [Test]
        public void ScenarioSizesAndPieceIdsAreDeterministicAndUnique()
        {
            CollectionAssert.AreEqual(new[] { 1000, 5000, 10000, 25000, 50000 }, VisualBenchmarkScenario.Sizes);
            var ids = new HashSet<PieceId>();
            foreach (int index in new[] { 0, 1, 7, 8, 999, 4999, 9999, 24999, 49999 })
            {
                PieceData a = VisualBenchmarkScenario.PieceAt(index, VisualBenchmarkScenario.Seed);
                PieceData b = VisualBenchmarkScenario.PieceAt(index, VisualBenchmarkScenario.Seed);
                Assert.That(ids.Add(a.Id), Is.True, $"duplicate index={index}");
                Assert.That(b.Id, Is.EqualTo(a.Id));
                Assert.That(b.Transform.Equals(a.Transform), Is.True);
                Assert.That(b.Dimensions.Equals(a.Dimensions), Is.True);
                Assert.That(a.Dimensions.IsValid, Is.True);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => VisualBenchmarkScenario.PieceAt(50000, VisualBenchmarkScenario.Seed));
        }

        [Test]
        public void SpiralPlacesNearAndFarHousesOnBothSidesOfOrigin()
        {
            Assert.That(VisualBenchmarkScenario.Cell(0), Is.EqualTo(Vector2Int.zero));
            var cells = new HashSet<Vector2Int>();
            int minX = 0, maxX = 0, minY = 0, maxY = 0;
            for (int house = 0; house < 6250; house++)
            {
                Vector2Int cell = VisualBenchmarkScenario.Cell(house);
                Assert.That(cells.Add(cell), Is.True, $"duplicate house={house}");
                minX = Math.Min(minX, cell.x); maxX = Math.Max(maxX, cell.x);
                minY = Math.Min(minY, cell.y); maxY = Math.Max(maxY, cell.y);
            }
            Assert.That(minX, Is.LessThan(-30)); Assert.That(maxX, Is.GreaterThan(30));
            Assert.That(minY, Is.LessThan(-30)); Assert.That(maxY, Is.GreaterThan(30));
        }

        [Test]
        public void SafetyRulesAndCsvSchemaKeepFailuresExplicit()
        {
            Assert.That(VisualBenchmarkRules.ExceedsMemory(100, 50, 100), Is.False);
            Assert.That(VisualBenchmarkRules.ExceedsMemory(101, 50, 100), Is.True);
            Assert.That(VisualBenchmarkRules.ExceedsMemory(50, 101, 100), Is.True);
            Assert.That(VisualBenchmarkRules.ExceedsGenerationSeconds(20d, 20d), Is.False);
            Assert.That(VisualBenchmarkRules.ExceedsGenerationSeconds(20.001d, 20d), Is.True);
            Assert.That(VisualBenchmarkRules.Percentile(new[] { 1f, 2f, 3f, 4f }, .95d), Is.EqualTo(4d));
            var fields = new string[VisualBenchmarkCsv.Columns];
            fields[0] = "quoted,\"value\"";
            string row = VisualBenchmarkCsv.Row(fields);
            StringAssert.StartsWith("\"quoted,\"\"value\"\"\"", row);
            StringAssert.Contains("\"unavailable\"", row);
            Assert.Throws<ArgumentException>(() => VisualBenchmarkCsv.Row("short"));
        }
    }
}
