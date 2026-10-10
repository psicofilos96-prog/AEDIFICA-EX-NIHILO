using System;
using System.IO;
using Aedifica.Construction;
using Aedifica.Interaction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class Pvt2IntegratedTests
    {
        [Test]
        public void ScenarioFitsOneKilometerAndIsDeterministic()
        {
            CollectionAssert.AreEqual(new[] { 10000,25000,50000 },Pvt2Scenario.Sizes);
            foreach (int index in new[] { 0,1,5,6,9,9999,24999,49999 })
            {
                PieceData first=Pvt2Scenario.PieceAt(index);
                PieceData second=Pvt2Scenario.PieceAt(index);
                Assert.That(first.Id,Is.EqualTo(second.Id));
                Assert.That(first.Transform,Is.EqualTo(second.Transform));
                Assert.That(Mathf.Abs(first.Transform.Position.x),Is.LessThanOrEqualTo(500f));
                Assert.That(Mathf.Abs(first.Transform.Position.z),Is.LessThanOrEqualTo(500f));
                Assert.That(first.Transform.Position.y,Is.GreaterThan(0f));
            }
            Assert.Throws<ArgumentOutOfRangeException>(()=>Pvt2Scenario.PieceAt(50000));
            Assert.That(Pvt2Scenario.GroundHeight(0f,0f),Is.GreaterThan(0f));
        }

        [Test]
        public void SnapshotRoundTripsEveryArchitecturalFamilyAndOpenings()
        {
            var world=new ConstructionWorld();
            for (int i=0;i<10;i++) Assert.That(world.Create(Pvt2Scenario.PieceAt(i)).Changed,Is.True);
            for (int i=0;i<FreePieceCatalog.Types.Length;i++)
            {
                PieceId id=PieceId.Parse(new Guid(5901,(short)i,0,0x50,0x56,0x54,0x32,0,0,0,4).ToString("N"));
                PieceData piece=FreePieceCatalog.Create(FreePieceCatalog.Types[i],id,
                    new Vector3(100f+i*10f,1f,100f),45f);
                Assert.That(world.Create(piece).Changed,Is.True);
            }
            PieceData original=Pvt2Scenario.PieceAt(1);
            Assert.That(original.Openings.Count,Is.GreaterThan(0));
            byte[] first=Pvt2Snapshot.Serialize(world);
            ConstructionWorld restored=Pvt2Snapshot.Deserialize(first);
            Assert.That(Pvt2Snapshot.Matches(restored,PvtIntegrity.Capture(world)),Is.True);
            CollectionAssert.AreEqual(first,Pvt2Snapshot.Serialize(world));
            PieceData changed=Pvt2Scenario.PieceAt(0);
            Assert.That(restored.Update(changed.Id,changed.WithTransform(new PieceTransform(
                changed.Transform.Position,Quaternion.Euler(0f,5f,0f)))).Changed,Is.True);
            Assert.That(Pvt2Snapshot.Matches(restored,PvtIntegrity.Capture(world)),Is.False,
                "A real orientation change must not be hidden by float tolerance.");
            byte[] corrupted=(byte[])first.Clone(); corrupted[0]=0;
            Assert.Throws<InvalidDataException>(()=>Pvt2Snapshot.Deserialize(corrupted));
        }

        [Test]
        public void StreamingCoordinatesCoverNegativeEdgesAndFarBoundary()
        {
            Assert.That(Pvt2Environment.Cell(new Vector3(-500f,0f,-500f)),Is.EqualTo(new Vector2Int(0,0)));
            Assert.That(Pvt2Environment.Cell(Vector3.zero),Is.EqualTo(new Vector2Int(8,8)));
            Assert.That(Pvt2Environment.Cell(new Vector3(500f,0f,500f)),Is.EqualTo(new Vector2Int(15,15)));
        }

        [Test]
        public void CsvHeadersRejectMissingMeasurementsRatherThanSilentlyShiftingColumns()
        {
            Assert.That(Pvt2IntegratedRunner.FramesHeader.Split(',').Length,Is.GreaterThan(35));
            Assert.That(Pvt2IntegratedRunner.EventsHeader.Split(',').Length,Is.EqualTo(12));
            Assert.Throws<ArgumentException>(()=>PvtBenchmarkCsv.Row(Pvt2IntegratedRunner.FramesHeader,"short"));
        }
    }
}
