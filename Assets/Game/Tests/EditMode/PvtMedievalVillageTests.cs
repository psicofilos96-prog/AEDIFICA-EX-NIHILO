using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtMedievalVillageTests
    {
        [Test]
        public void CastleMarketAndSixResidencesAreDeterministicEditablePieces()
        {
            List<PieceData> first=PvtMedievalVillageScenario.Build();
            List<PieceData> second=PvtMedievalVillageScenario.Build();
            Assert.That(first.Count,Is.GreaterThan(150));
            Assert.That(first.Count,Is.LessThan(500));
            Assert.That(second.Count,Is.EqualTo(first.Count));
            var ids=new HashSet<PieceId>();
            var world=new ConstructionWorld();
            int walls=0,roofs=0,arches=0,vaults=0,domes=0,openings=0,finishes=0;
            bool castleGate=false,marketArcade=false;
            for (int i=0;i<first.Count;i++)
            {
                PieceData piece=first[i];
                Assert.That(ids.Add(piece.Id),Is.True,"Duplicate ID at "+i);
                Assert.That(second[i].Id,Is.EqualTo(piece.Id));
                Assert.That(second[i].Transform,Is.EqualTo(piece.Transform));
                Assert.That(world.Create(piece).Changed,Is.True);
                Bounds bounds=ConstructionChangeSet.WorldBounds(piece);
                Assert.That(bounds.min.x,Is.GreaterThan(-40f));
                Assert.That(bounds.max.x,Is.LessThan(40f));
                Assert.That(bounds.min.z,Is.GreaterThan(-40f));
                Assert.That(bounds.max.z,Is.LessThan(40f));
                openings+=piece.Openings.Count;finishes+=piece.Finishes.Count;
                if (piece.Type==PieceType.Wall)
                {
                    walls++;
                    if (Mathf.Abs(piece.Transform.Position.x)<0.1f &&
                        Mathf.Abs(piece.Transform.Position.z-6f)<0.1f)
                        castleGate=piece.Openings.Count>=3;
                    if (Mathf.Abs(piece.Transform.Position.x)<0.1f &&
                        Mathf.Abs(piece.Transform.Position.z+21f)<0.1f)
                        marketArcade=piece.Openings.Count==3;
                }
                if (piece.Dimensions.IsRoof) roofs++;
                if (piece.Type==PieceType.Arch) arches++;
                if (piece.Type==PieceType.Vault) vaults++;
                if (piece.Type==PieceType.Dome) domes++;
            }
            Assert.That(world.Count,Is.EqualTo(first.Count));
            Assert.That(walls,Is.GreaterThan(35));
            Assert.That(roofs,Is.GreaterThanOrEqualTo(11));
            Assert.That(arches,Is.GreaterThanOrEqualTo(4));
            Assert.That(vaults,Is.GreaterThanOrEqualTo(1));
            Assert.That(domes,Is.GreaterThanOrEqualTo(1));
            Assert.That(openings,Is.GreaterThan(25));
            Assert.That(finishes,Is.GreaterThan(45));
            Assert.That(castleGate,Is.True);
            Assert.That(marketArcade,Is.True);
            ConstructionWorld loaded=Pvt2Snapshot.Deserialize(Pvt2Snapshot.Serialize(world));
            Assert.That(Pvt2Snapshot.Matches(loaded,PvtIntegrity.Capture(world)),Is.True);
        }

        [Test]
        public void OlderSingleResidenceRecipeIsNotReplaced()
        {
            List<PieceData> residence=PvtVisualCityScenario.BuildReferenceResidence();
            Assert.That(residence.Count,Is.LessThan(100));
            Assert.That(PvtMedievalVillageScenario.Build().Count,Is.GreaterThan(residence.Count));
        }
    }
}
