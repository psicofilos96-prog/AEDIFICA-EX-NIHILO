using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtGrandVillageTests
    {
        [Test]
        public void CastleBarracksMarketAndEighteenHousesHaveStableEditableIds()
        {
            List<PieceData> first=PvtGrandVillageScenario.Build();
            List<PieceData> second=PvtGrandVillageScenario.Build();
            Assert.That(first.Count,Is.GreaterThan(500));
            Assert.That(second.Count,Is.EqualTo(first.Count));
            var ids=new HashSet<PieceId>();
            var world=new ConstructionWorld();
            int castle=0,barracks=0,houses=0,arches=0,bridgeArches=0,openings=0;
            foreach (PieceData piece in first)
            {
                Assert.That(ids.Add(piece.Id),Is.True,"Duplicate PieceId: "+piece.Id);
                Assert.That(world.Create(piece).Changed,Is.True);
                if (piece.Transform.Position.z>14f && piece.Transform.Position.y>11f) castle++;
                if (piece.Transform.Position.x<-45f && piece.Transform.Position.z>-17f &&
                    piece.Transform.Position.z<3f) barracks++;
                if (piece.Transform.Position.z<-18f) houses++;
                if (piece.Type==PieceType.Arch) arches++;
                if (piece.Type==PieceType.Arch && piece.Transform.Position.x>30f &&
                    Mathf.Abs(piece.Transform.Position.z+18f)<0.1f) bridgeArches++;
                openings+=piece.Openings.Count;
                Bounds bounds=ConstructionChangeSet.WorldBounds(piece);
                Assert.That(bounds.min.x,Is.GreaterThan(-90f));
                Assert.That(bounds.max.x,Is.LessThan(90f));
                Assert.That(bounds.min.z,Is.GreaterThan(-90f));
                Assert.That(bounds.max.z,Is.LessThan(90f));
            }
            for (int i=0;i<first.Count;i++)
            {
                Assert.That(second[i].Id,Is.EqualTo(first[i].Id));
                Assert.That(second[i].Transform,Is.EqualTo(first[i].Transform));
            }
            Assert.That(castle,Is.GreaterThan(100));
            Assert.That(barracks,Is.GreaterThan(10));
            Assert.That(houses,Is.GreaterThan(300));
            Assert.That(arches,Is.GreaterThanOrEqualTo(9));
            Assert.That(bridgeArches,Is.EqualTo(3));
            Assert.That(openings,Is.GreaterThan(18));
            Assert.That(world.Count,Is.EqualTo(first.Count));
            ConstructionWorld loaded=Pvt2Snapshot.Deserialize(Pvt2Snapshot.Serialize(world));
            Assert.That(Pvt2Snapshot.Matches(loaded,PvtIntegrity.Capture(world)),Is.True);
        }

        [Test]
        public void PlateauRiverAndBuildingExclusionsAreConsistent()
        {
            float castle=PvtGrandVillageEnvironment.Height(-22f,38f);
            float river=PvtGrandVillageEnvironment.Height(
                PvtGrandVillageEnvironment.RiverCenter(-18f),-18f);
            Assert.That(castle,Is.EqualTo(12f).Within(0.01f));
            Assert.That(river,Is.LessThan(0.75f));
            Assert.That(PvtGrandVillageEnvironment.Height(-60f,-6f),Is.GreaterThan(river));
            Assert.That(PvtGrandVillageEnvironment.IsReserved(-22f,38f),Is.True);
            Assert.That(PvtGrandVillageEnvironment.IsReserved(-60f,-6f),Is.True);
            Assert.That(PvtGrandVillageEnvironment.IsReserved(
                PvtGrandVillageEnvironment.RiverCenter(-18f),-18f),Is.True);
            Assert.That(PvtGrandVillageEnvironment.IsReserved(-80f,65f),Is.False);
            foreach (float x in new[] {-72f,-57f,-42f,-27f,-12f,3f})
                foreach (float z in new[] {-58f,-40f,-22f})
                    Assert.That(PvtGrandVillageEnvironment.IsReserved(x,z),Is.True,
                        $"House plot at ({x},{z}) must be clear of trees.");
            foreach (float z in new[] {-70f,-18f,50f})
                Assert.That(float.IsNaN(PvtGrandVillageEnvironment.Height(45f,z)),Is.False);
        }
    }
}
