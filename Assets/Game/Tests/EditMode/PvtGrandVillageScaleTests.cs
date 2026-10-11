using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtGrandVillageScaleTests
    {
        [Test]
        public void RequestedTiersHaveExactCountsAndStableNonOverlappingTiles()
        {
            List<PieceData> template = PvtGrandVillageScenario.Build();
            Assert.That(PvtGrandVillageScaleScenario.Sizes,
                Is.EqualTo(new[] { 1000, 5000, 10000, 25000, 50000, 100000 }));
            var offsets = new HashSet<Vector3>();
            for (int tile = 0; tile < PvtGrandVillageScaleScenario.TileCount(100000, template.Count); tile++)
                Assert.That(offsets.Add(PvtGrandVillageScaleScenario.TileOffset(tile)), Is.True);
            foreach (int size in PvtGrandVillageScaleScenario.Sizes)
            {
                int last = size - 1;
                PieceData piece = PvtGrandVillageScaleScenario.PieceAt(template, last);
                Assert.That(piece.Id.IsValid, Is.True);
                Assert.That(PvtGrandVillageScaleScenario.PieceAt(template, last).Id, Is.EqualTo(piece.Id));
                Assert.That(PvtGrandVillageScaleScenario.TileCount(size, template.Count) * template.Count,
                    Is.GreaterThanOrEqualTo(size));
            }
        }

        [Test]
        public void ClonedVillagePreservesOpeningsFinishesAndEditableIdentity()
        {
            List<PieceData> template = PvtGrandVillageScenario.Build();
            var world = new ConstructionWorld();
            int count = template.Count * 2 + 17;
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                PieceData clone = PvtGrandVillageScaleScenario.PieceAt(template, ordinal);
                PieceData original = template[ordinal % template.Count];
                Vector3 offset = PvtGrandVillageScaleScenario.TileOffset(ordinal / template.Count);
                Assert.That(clone.Transform.Position, Is.EqualTo(original.Transform.Position + offset));
                Assert.That(clone.Dimensions, Is.EqualTo(original.Dimensions));
                Assert.That(clone.MaterialId, Is.EqualTo(original.MaterialId));
                Assert.That(clone.Openings.Count, Is.EqualTo(original.Openings.Count));
                Assert.That(clone.Finishes.Count, Is.EqualTo(original.Finishes.Count));
                foreach (WallOpening opening in clone.Openings)
                    Assert.That(opening.HostWallId, Is.EqualTo(clone.Id));
                foreach (ArchitecturalFinish finish in clone.Finishes)
                {
                    Assert.That(finish.HostId, Is.EqualTo(clone.Id));
                    if (finish.OpeningId != Guid.Empty)
                    {
                        bool found = false;
                        foreach (WallOpening opening in clone.Openings)
                            found |= opening.Id == finish.OpeningId;
                        Assert.That(found, Is.True);
                    }
                }
                Assert.That(world.Create(clone).Changed, Is.True, "Duplicate ID at " + ordinal);
            }
            Assert.That(world.Count, Is.EqualTo(count));
            Assert.That(world.SpatialIndex.IndexedPieceCount, Is.EqualTo(count));
        }
    }
}
