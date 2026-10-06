using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class BlockGeometryTests
    {
        [TestCase(1f, 1f, 1f)]
        [TestCase(2f, 1f, 3f)]
        [TestCase(4f, 0.5f, 2f)]
        public void BlockHasExactDimensionsAndBasePivot(float width, float height, float depth)
        {
            BlockGeometry geometry = BlockGeometryGenerator.Generate(new BlockDimensions(width, height, depth));
            Assert.That(geometry.Vertices.Length, Is.EqualTo(24));
            Assert.That(geometry.Triangles.Length, Is.EqualTo(36));
            Assert.That(geometry.Normals.Length, Is.EqualTo(24));
            Assert.That(geometry.UVs.Length, Is.EqualTo(24));
            Assert.That(geometry.Bounds.min, Is.EqualTo(new Vector3(-width / 2f, 0f, -depth / 2f)));
            Assert.That(geometry.Bounds.max, Is.EqualTo(new Vector3(width / 2f, height, depth / 2f)));
            foreach (Vector3 vertex in geometry.Vertices)
            {
                Assert.That(vertex.x, Is.InRange(-width / 2f, width / 2f));
                Assert.That(vertex.y, Is.InRange(0f, height));
                Assert.That(vertex.z, Is.InRange(-depth / 2f, depth / 2f));
                Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), Is.False);
                Assert.That(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False);
                Assert.That(float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False);
            }
        }

        [Test]
        public void FacesHaveFlatNormalsOutwardWindingAndMeterUvs()
        {
            var geometry = BlockGeometryGenerator.Generate(new BlockDimensions(2f, 1f, 3f));
            var expectedNormals = new[] { Vector3.back, Vector3.forward, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            var expectedUvMax = new[] { new Vector2(2f, 1f), new Vector2(2f, 1f), new Vector2(3f, 1f),
                new Vector2(3f, 1f), new Vector2(2f, 3f), new Vector2(2f, 3f) };
            for (int face = 0; face < 6; face++)
            {
                int vertex = face * 4;
                int index = face * 6;
                for (int i = 0; i < 4; i++) Assert.That(geometry.Normals[vertex + i], Is.EqualTo(expectedNormals[face]));
                Assert.That(geometry.UVs[vertex], Is.EqualTo(Vector2.zero));
                Assert.That(geometry.UVs[vertex + 2], Is.EqualTo(expectedUvMax[face]));
                Assert.That(geometry.Triangles[index], Is.EqualTo(vertex));
                Assert.That(geometry.Triangles[index + 1], Is.EqualTo(vertex + 1));
                Assert.That(geometry.Triangles[index + 2], Is.EqualTo(vertex + 2));
                Vector3 firstEdge = geometry.Vertices[vertex + 1] - geometry.Vertices[vertex];
                Vector3 secondEdge = geometry.Vertices[vertex + 2] - geometry.Vertices[vertex];
                Assert.That(Vector3.Dot(Vector3.Cross(firstEdge, secondEdge).normalized, expectedNormals[face]), Is.GreaterThan(0.999f));
            }
        }

        [Test]
        public void SameDimensionsProduceSameLocalGeometryRegardlessOfPieceTransform()
        {
            var dimensions = new BlockDimensions(2f, 1f, 3f);
            var a = new PieceData(PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                new PieceTransform(Vector3.zero, Quaternion.identity), dimensions);
            var b = new PieceData(PieceId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                new PieceTransform(new Vector3(10f, 0f, 5f), Quaternion.Euler(0f, 30f, 0f)), dimensions);
            BlockGeometry first = BlockGeometryGenerator.Generate(a.BlockDimensions);
            BlockGeometry second = BlockGeometryGenerator.Generate(b.BlockDimensions);
            Assert.That(first.Vertices, Is.EqualTo(second.Vertices));
            Assert.That(first.Triangles, Is.EqualTo(second.Triangles));
            Assert.That(first.Normals, Is.EqualTo(second.Normals));
            Assert.That(first.UVs, Is.EqualTo(second.UVs));
            Assert.That(a.Transform.Position, Is.Not.EqualTo(b.Transform.Position));
            Assert.That(a.Transform.Rotation, Is.Not.EqualTo(b.Transform.Rotation));
        }

        [Test]
        public void MeshAdapterPreservesGeometry()
        {
            BlockGeometry geometry = BlockGeometryGenerator.Generate(new BlockDimensions(2f, 1f, 3f));
            Mesh mesh = BlockMeshFactory.Build(geometry);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(24));
                Assert.That(mesh.triangles, Is.EqualTo(geometry.Triangles));
                Assert.That(mesh.bounds, Is.EqualTo(geometry.Bounds));
                Assert.That(mesh.uv, Is.EqualTo(geometry.UVs));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void DefaultDimensionsAreRejected()
        {
            Assert.Throws<System.ArgumentException>(() => BlockGeometryGenerator.Generate(default));
        }
    }
}
