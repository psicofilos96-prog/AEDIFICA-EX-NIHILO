using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtScenarioTests
    {
        [Test]
        public void ScenarioIsDeterministicAndHasArchitecturalVariety()
        {
            CollectionAssert.AreEqual(new[] { 10000, 25000, 50000 }, PvtScenario.Sizes);
            var ids = new HashSet<PieceId>();
            var types = new HashSet<PieceType>();
            int openings = 0;
            for (int i = 0; i < PvtScenario.Sizes[2]; i++)
            {
                PieceData a = PvtScenario.PieceAt(i);
                Assert.That(ids.Add(a.Id), Is.True, $"duplicate index={i}");
                Assert.That(a.Dimensions.IsValid, Is.True, $"invalid dimensions index={i}");
                if (i % 997 == 0)
                {
                    PieceData b = PvtScenario.PieceAt(i);
                    Assert.That(b.Id, Is.EqualTo(a.Id));
                    Assert.That(b.Transform, Is.EqualTo(a.Transform));
                    Assert.That(b.Dimensions, Is.EqualTo(a.Dimensions));
                }
                types.Add(a.Type);
                openings += a.Openings.Count;
            }
            Assert.That(ids.Count, Is.EqualTo(50000));
            Assert.That(types, Does.Contain(PieceType.Wall));
            Assert.That(types, Does.Contain(PieceType.Arch));
            Assert.That(types, Does.Contain(PieceType.GableRoof));
            Assert.That(types, Does.Contain(PieceType.HipRoof));
            Assert.That(openings, Is.GreaterThan(1000));
            Assert.Throws<ArgumentOutOfRangeException>(() => PvtScenario.PieceAt(50000));
        }

        [Test]
        public void EveryRepresentativePieceHasCompleteGeometryForCombining()
        {
            foreach (int i in new[] { 0, 1, 2, 5, 6, 7, 8, 9, 11, 15 })
            {
                PieceData piece = PvtScenario.PieceAt(i);
                var geometry = PvtChunkVisualEngine.Geometry(piece);
                Assert.That(geometry.Vertices.Length, Is.EqualTo(geometry.Normals.Length), $"index={i}");
                Assert.That(geometry.Vertices.Length, Is.EqualTo(geometry.UVs.Length), $"index={i}");
                Assert.That(geometry.Triangles.Length % 3, Is.Zero, $"index={i}");
                Assert.That(geometry.Triangles.Length, Is.GreaterThan(0), $"index={i}");
            }
        }

        [Test]
        public void CsvSchemaAndPercentilesKeepUnavailableValuesExplicit()
        {
            var fields = new string[PvtBenchmarkCsv.FramesHeader.Split(',').Length];
            fields[0] = "quoted,\"value\"";
            StringAssert.StartsWith("\"quoted,\"\"value\"\"\"",
                PvtBenchmarkCsv.Row(PvtBenchmarkCsv.FramesHeader, fields));
            StringAssert.Contains("\"unavailable\"", PvtBenchmarkCsv.Row(PvtBenchmarkCsv.FramesHeader, fields));
            Assert.That(PvtBenchmarkCsv.EditsHeader.Split(',').Length, Is.EqualTo(15));
            Assert.Throws<ArgumentException>(() => PvtBenchmarkCsv.Row(PvtBenchmarkCsv.FramesHeader, "short"));
            var samples = new List<float> { 1f, 2f, 3f, 4f };
            Assert.That(PvtBenchmarkCsv.Percentile(samples, 0.95d), Is.EqualTo(4d));
        }

        [Test]
        public void PvtRecolorRequiresAnEffectiveMaterialChange()
        {
            var world = new ConstructionWorld();
            PieceData slab = PvtScenario.PieceAt(0);
            Assert.That(slab.MaterialId, Is.EqualTo(LabMaterialIds.Stone));
            Assert.That(world.Create(slab).Changed, Is.True);
            var history = new ConstructionCommandHistory(world);
            int notifications = 0;
            world.Changed += _ => notifications++;

            Assert.That(history.Update(slab.Id, slab.WithMaterial(LabMaterialIds.Stone)).Changed, Is.False);
            Assert.That(notifications, Is.Zero);
            Assert.That(history.Update(slab.Id, slab.WithMaterial(LabMaterialIds.Brick)).Changed, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(world.TryGet(slab.Id, out PieceData recolored), Is.True);
            Assert.That(recolored.MaterialId, Is.EqualTo(LabMaterialIds.Brick));
            Assert.That(history.TryUndo(out _), Is.True);
            Assert.That(world.TryGet(slab.Id, out PieceData restored), Is.True);
            Assert.That(restored.MaterialId, Is.EqualTo(LabMaterialIds.Stone));
        }
    }
}
