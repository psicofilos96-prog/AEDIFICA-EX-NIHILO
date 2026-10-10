using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtFinalScenarioTests
    {
        [TestCase(10000,300,100,50)]
        [TestCase(25000,500,400,100)]
        [TestCase(50000,1000,800,200)]
        [TestCase(60000,1000,800,325)]
        [TestCase(70000,1000,800,450)]
        [TestCase(80000,1000,800,575)]
        public void CompositionCountsRealBuildingsAndPieces(int target,int simple,int medium,int complex)
        {
            PvtFinalScenario.Composition(target,out int s,out int m,out int c);
            Assert.That(s,Is.EqualTo(simple));
            Assert.That(m,Is.EqualTo(medium));
            Assert.That(c,Is.EqualTo(complex));
            Assert.That(PvtFinalScenario.BuildingCount(target),Is.EqualTo(simple+medium+complex));
            Assert.That(s*10+m*30+c*80,Is.EqualTo(target));
        }

        [Test]
        public void ArchitecturalFamiliesAndIdsAreDeterministic()
        {
            const int target=50000;
            int[] indices={0,1,3,5,6,7,9,9999,10000,33999,34000,34024,34034,34059,49999};
            var ids=new HashSet<PieceId>();
            bool opening=false,roof=false,arch=false,stair=false,domeOrVault=false;
            foreach (int index in indices)
            {
                PieceData a=PvtFinalScenario.PieceAt(index,target);
                PieceData b=PvtFinalScenario.PieceAt(index,target);
                Assert.That(a.Id,Is.EqualTo(b.Id),index.ToString());
                Assert.That(a.Transform,Is.EqualTo(b.Transform),index.ToString());
                Assert.That(ids.Add(a.Id),Is.True,"duplicate sample ID "+index);
                Assert.That(Mathf.Abs(a.Transform.Position.x),Is.LessThan(500f));
                Assert.That(Mathf.Abs(a.Transform.Position.z),Is.LessThan(500f));
                opening|=a.Openings.Count>0;
                roof|=a.Type==PieceType.GableRoof || a.Type==PieceType.HipRoof || a.Type==PieceType.ShedRoof;
                arch|=a.Type==PieceType.Arch;
                stair|=a.Type==PieceType.Stair;
                domeOrVault|=a.Type==PieceType.Dome || a.Type==PieceType.Vault;
            }
            Assert.That(opening && roof && arch && stair && domeOrVault,Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(()=>PvtFinalScenario.PieceAt(target,target));
            Assert.Throws<ArgumentOutOfRangeException>(()=>PvtFinalScenario.Composition(55000,out _,out _,out _));
        }

        [Test]
        public void BenchmarkHeadersExposeBuildingAndGeometryCounts()
        {
            foreach (string column in new[] { "buildings", "simple_buildings", "medium_buildings",
                "complex_buildings", "vertices", "triangles", "materials" })
                CollectionAssert.Contains(PvtFinalRunner.FramesHeader.Split(','),column);
            CollectionAssert.Contains(PvtFinalRunner.EventsHeader.Split(','),"managed_alloc_bytes");
        }

        [Test]
        public void DStopsOnlyAfterMeasuredSubSixtyMean()
        {
            Assert.That(PvtFinalScenario.ShouldStopD(50000,20d),Is.False);
            Assert.That(PvtFinalScenario.ShouldStopD(60000,60d),Is.False);
            Assert.That(PvtFinalScenario.ShouldStopD(60000,59.999d),Is.True);
            Assert.That(PvtFinalScenario.ShouldStopD(70000,double.NaN),Is.False);
        }
    }
}
