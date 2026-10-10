using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtFreeAssetsReferenceTests
    {
        [Test]
        public void SingleResidenceIsDeterministicAndAllArchitectureRemainsLogical()
        {
            List<PieceData> first=PvtVisualCityScenario.BuildReferenceResidence();
            List<PieceData> again=PvtVisualCityScenario.BuildReferenceResidence();
            Assert.That(first.Count,Is.GreaterThan(10));
            Assert.That(first.Count,Is.LessThan(150),"This art gate contains exactly one residence.");
            Assert.That(again.Count,Is.EqualTo(first.Count));
            var ids=new HashSet<PieceId>();
            var world=new ConstructionWorld();
            int walls=0,roofs=0,openings=0,doorLeaves=0,finishes=0;
            for (int i=0;i<first.Count;i++)
            {
                PieceData piece=first[i];
                Assert.That(ids.Add(piece.Id),Is.True,"Duplicate logical ID at "+i);
                Assert.That(again[i].Id,Is.EqualTo(piece.Id));
                Assert.That(again[i].Transform,Is.EqualTo(piece.Transform));
                Assert.That(world.Create(piece).Changed,Is.True);
                finishes+=piece.Finishes.Count;
                if (piece.Type==PieceType.Wall) { walls++;openings+=piece.Openings.Count; }
                if (piece.Dimensions.IsSlopedRoof) roofs++;
                if (piece.Type==PieceType.Block && piece.MaterialId==PvtFinalScenario.Wood &&
                    piece.Dimensions.Y>2f) doorLeaves++;
            }
            Assert.That(world.Count,Is.EqualTo(first.Count));
            Assert.That(walls,Is.EqualTo(4));
            Assert.That(roofs,Is.EqualTo(1));
            Assert.That(openings,Is.GreaterThanOrEqualTo(5));
            Assert.That(doorLeaves,Is.EqualTo(1));
            Assert.That(finishes,Is.GreaterThanOrEqualTo(15));
        }
    }
}
