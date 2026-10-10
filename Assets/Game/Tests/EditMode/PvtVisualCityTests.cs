using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class PvtVisualCityTests
    {
        [Test]
        public void HistoricCityIsDeterministicEditableAndContainsArchitecturalFamilies()
        {
            List<PieceData> first=PvtVisualCityScenario.Build();
            List<PieceData> second=PvtVisualCityScenario.Build();
            Assert.That(first.Count,Is.GreaterThan(400));
            Assert.That(second.Count,Is.EqualTo(first.Count));
            var ids=new HashSet<PieceId>();
            var types=new HashSet<PieceType>();
            int framed=0,woodDoorPanels=0;
            for (int i=0;i<first.Count;i++)
            {
                Assert.That(ids.Add(first[i].Id),Is.True,$"duplicate at {i}");
                Assert.That(second[i].Id,Is.EqualTo(first[i].Id));
                Assert.That(second[i].Transform,Is.EqualTo(first[i].Transform));
                types.Add(first[i].Type);
                if (first[i].Type==PieceType.Beam && first[i].MaterialId==PvtFinalScenario.Wood)
                    framed++;
                if (first[i].Type==PieceType.Block && first[i].MaterialId==PvtFinalScenario.Wood &&
                    first[i].Dimensions.Y>2f) woodDoorPanels++;
            }
            foreach (PieceType type in new[] {PieceType.Wall,PieceType.Slab,PieceType.Beam,
                PieceType.Column,PieceType.Stair,PieceType.Arch,PieceType.Vault,PieceType.Dome,
                PieceType.GableRoof,PieceType.HipRoof})
                Assert.That(types.Contains(type),Is.True,type.ToString());
            Assert.That(framed,Is.GreaterThan(20),"Window and door trim must be separate editable pieces.");
            Assert.That(woodDoorPanels,Is.EqualTo(12),"Every residence needs an editable door leaf.");
        }

        [Test]
        public void MonumentBuildsWithSemanticDimensionsAndClosedSideWalls()
        {
            List<PieceData> city=PvtVisualCityScenario.Build();
            float ground=Pvt2Scenario.GroundHeight(-220f,78f);
            var world=new ConstructionWorld();
            foreach (PieceData piece in city)
                Assert.That(world.Create(piece).Changed,Is.True,$"Invalid or duplicate {piece.Id}");
            int sides=0,arches=0,domes=0,vaults=0;
            foreach (PieceData piece in city)
            {
                if (Mathf.Abs(piece.Transform.Position.x+220f)>13f ||
                    Mathf.Abs(piece.Transform.Position.z-78f)>14f) continue;
                if (piece.Type==PieceType.Wall &&
                    Mathf.Abs(Mathf.Abs(piece.Transform.Position.x+220f)-12f)<0.05f &&
                    Mathf.Abs(piece.Transform.Position.y-(ground+0.45f))<0.05f)
                {
                    sides++;
                    Assert.That(piece.WallDimensions.Length,Is.EqualTo(17f));
                    Assert.That(Mathf.Abs(Vector3.Dot(piece.Transform.Rotation*Vector3.right,
                        Vector3.forward)),Is.GreaterThan(0.999f),"Side wall must run along Z.");
                }
                if (piece.Type==PieceType.Arch) arches++;
                if (piece.Type==PieceType.Dome) domes++;
                if (piece.Type==PieceType.Vault) vaults++;
            }
            Assert.That(sides,Is.EqualTo(2));
            Assert.That(arches,Is.GreaterThanOrEqualTo(3));
            Assert.That(domes,Is.EqualTo(1));
            Assert.That(vaults,Is.EqualTo(2));
        }

        [Test]
        public void FirstResidentialShellHasSealedSideWallsAndRoofOverhang()
        {
            List<PieceData> city=PvtVisualCityScenario.Build();
            PieceData roof=null;
            int sideWalls=0;
            foreach (PieceData piece in city)
            {
                if (Vector2.Distance(new Vector2(piece.Transform.Position.x,piece.Transform.Position.z),
                    new Vector2(-237f,-82f))>7f) continue;
                if (piece.Dimensions.IsSlopedRoof) roof=piece;
                if (piece.Type==PieceType.Wall &&
                    Mathf.Abs(Vector3.Dot(piece.Transform.Rotation*Vector3.right,
                        Vector3.right))>0.999f) sideWalls++;
            }
            Assert.That(roof,Is.Not.Null);
            Assert.That(sideWalls,Is.EqualTo(2));
            Assert.That(roof.Dimensions.X,Is.GreaterThan(7.6f));
            Assert.That(roof.Dimensions.Z,Is.GreaterThan(6.8f));
        }

        [Test]
        public void SlopedAndVerticalArchitecturalFacesHaveNoncollapsedUvsAndTangents()
        {
            var dimensions=new[] {
                new PieceDimensions(new GableRoofDimensions(8f,7f,0.35f,1.8f)),
                new PieceDimensions(new VaultDimensions(4f,2.5f,6f,0.35f)),
                new PieceDimensions(new DomeDimensions(6f,3f,0.35f)),
                new PieceDimensions(new StairDimensions(3f,1.5f,4f,6)) };
            foreach (PieceDimensions d in dimensions)
            {
                var piece=new PieceData(PieceId.Parse("22222222222222222222222222222222"),
                    new PieceTransform(Vector3.zero,Quaternion.identity),d);
                BlockGeometry geometry=PvtChunkVisualEngine.Geometry(piece);
                for (int i=0;i<geometry.Triangles.Length;i+=3)
                {
                    Vector2 a=geometry.UVs[geometry.Triangles[i]];
                    Vector2 b=geometry.UVs[geometry.Triangles[i+1]];
                    Vector2 c=geometry.UVs[geometry.Triangles[i+2]];
                    float area=Mathf.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x));
                    Assert.That(area,Is.GreaterThan(1e-10f),$"{d.Type} triangle {i/3} has collapsed UVs");
                }
                Mesh mesh=BlockMeshFactory.Build(geometry);
                try
                {
                    Assert.That(mesh.tangents.Length,Is.EqualTo(mesh.vertexCount),d.Type.ToString());
                    foreach (Vector4 tangent in mesh.tangents)
                        Assert.That(float.IsNaN(tangent.x) || float.IsNaN(tangent.y) ||
                            float.IsNaN(tangent.z) || tangent.sqrMagnitude<0.5f,Is.False,
                            $"{d.Type} invalid tangent");
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
        }
    }
}
