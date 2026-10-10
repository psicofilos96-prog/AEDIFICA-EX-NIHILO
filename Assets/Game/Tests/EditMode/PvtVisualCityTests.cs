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
            int framed=0;
            for (int i=0;i<first.Count;i++)
            {
                Assert.That(ids.Add(first[i].Id),Is.True,$"duplicate at {i}");
                Assert.That(second[i].Id,Is.EqualTo(first[i].Id));
                Assert.That(second[i].Transform,Is.EqualTo(first[i].Transform));
                types.Add(first[i].Type);
                if (first[i].Type==PieceType.Beam && first[i].MaterialId==PvtFinalScenario.Wood)
                    framed++;
            }
            foreach (PieceType type in new[] {PieceType.Wall,PieceType.Slab,PieceType.Beam,
                PieceType.Column,PieceType.Stair,PieceType.Arch,PieceType.Vault,PieceType.Dome,
                PieceType.GableRoof,PieceType.HipRoof})
                Assert.That(types.Contains(type),Is.True,type.ToString());
            Assert.That(framed,Is.GreaterThan(20),"Window and door trim must be separate editable pieces.");
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
