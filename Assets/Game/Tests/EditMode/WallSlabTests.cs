using System;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class WallSlabTests
    {
        private static readonly PieceId WallId = PieceId.Parse("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
        private static readonly PieceId SlabId = PieceId.Parse("dddddddddddddddddddddddddddddddd");
        private static readonly PieceTransform Base = new PieceTransform(Vector3.zero, Quaternion.identity);

        [Test]
        public void WallAndSlabDimensionsAcceptValidMetersAndCompareDeterministically()
        {
            var wall = new WallDimensions(4f, 3f, 0.2f);
            var slab = new SlabDimensions(4f, 0.2f, 3f);
            Assert.That(wall.IsValid && slab.IsValid, Is.True);
            Assert.That(default(WallDimensions).IsValid || default(SlabDimensions).IsValid, Is.False);
            Assert.That(wall, Is.EqualTo(new WallDimensions(4f, 3f, 0.2f)));
            Assert.That(slab, Is.EqualTo(new SlabDimensions(4f, 0.2f, 3f)));
            Assert.That(wall.GetHashCode(), Is.EqualTo(new WallDimensions(4f, 3f, 0.2f).GetHashCode()));
            Assert.That(slab.GetHashCode(), Is.EqualTo(new SlabDimensions(4f, 0.2f, 3f).GetHashCode()));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void WallAndSlabRejectInvalidValuesOnEveryAxis(float invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallDimensions(invalid, 2f, 0.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallDimensions(3f, invalid, 0.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallDimensions(3f, 2f, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlabDimensions(invalid, 0.2f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlabDimensions(4f, invalid, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlabDimensions(4f, 0.2f, invalid));
        }

        [Test]
        public void SemanticParametersAndWorldPreserveTypesAndIdentity()
        {
            var wall = new PieceData(WallId, Base, new WallDimensions(4f, 3f, 0.2f));
            var slab = new PieceData(SlabId, Base, new SlabDimensions(4f, 0.2f, 3f));
            Assert.That(wall.Type, Is.EqualTo(PieceType.Wall));
            Assert.That(slab.Type, Is.EqualTo(PieceType.Slab));
            Assert.That(wall.WallDimensions, Is.EqualTo(new WallDimensions(4f, 3f, 0.2f)));
            Assert.That(slab.SlabDimensions, Is.EqualTo(new SlabDimensions(4f, 0.2f, 3f)));
            Assert.Throws<InvalidOperationException>(() => { var _ = wall.BlockDimensions; });
            Assert.Throws<InvalidOperationException>(() => { var _ = slab.WallDimensions; });
            Assert.Throws<ArgumentException>(() => wall.WithDimensions(slab.Dimensions));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PieceData(WallId, PieceType.Wall, Base, new BlockDimensions(1f, 1f, 1f)));
            Assert.Throws<ArgumentException>(() => new PieceData(WallId, Base, default(PieceDimensions)));
            var world = new ConstructionWorld();
            Assert.That(world.Add(wall), Is.True);
            Assert.That(world.Add(slab), Is.True);
            Assert.That(world.Replace(WallId, new PieceData(WallId, Base, slab.SlabDimensions)), Is.False);
            Assert.That(world.TryGet(WallId, out PieceData foundWall), Is.True);
            Assert.That(foundWall, Is.SameAs(wall));
            PieceData resized = wall.WithWallDimensions(new WallDimensions(5f, 3f, 0.2f));
            Assert.That(world.Replace(WallId, resized), Is.True);
            Assert.That(world.TryGet(WallId, out foundWall), Is.True);
            Assert.That(foundWall.WallDimensions.Length, Is.EqualTo(5f));
        }

        [TestCase(PieceType.Wall, 4f, 3f, 0.2f)]
        [TestCase(PieceType.Slab, 4f, 0.2f, 3f)]
        public void GeometrySharesCuboidTopologyAndBasePivot(PieceType type, float x, float y, float z)
        {
            PieceDimensions dimensions = type == PieceType.Wall
                ? new PieceDimensions(new WallDimensions(x, y, z))
                : new PieceDimensions(new SlabDimensions(x, y, z));
            BlockGeometry first = BlockGeometryGenerator.GeneratePiece(dimensions);
            BlockGeometry second = BlockGeometryGenerator.GeneratePiece(dimensions);
            Assert.That(first.Vertices.Length, Is.EqualTo(24));
            Assert.That(first.Triangles.Length, Is.EqualTo(36));
            Assert.That(first.Bounds.min, Is.EqualTo(new Vector3(-x / 2f, 0f, -z / 2f)));
            Assert.That(first.Bounds.max, Is.EqualTo(new Vector3(x / 2f, y, z / 2f)));
            Assert.That(first.UVs[2], Is.EqualTo(new Vector2(x, y)));
            Assert.That(first.Vertices, Is.EqualTo(second.Vertices));
            Assert.That(first.Normals, Is.EqualTo(second.Normals));
            Assert.That(first.UVs, Is.EqualTo(second.UVs));
            Assert.That(first.Triangles, Is.EqualTo(second.Triangles));
        }

        [TestCase(PieceType.Wall, ManipulationAxis.X, 5f, 3f, 0.2f)]
        [TestCase(PieceType.Wall, ManipulationAxis.Y, 4f, 4f, 0.2f)]
        [TestCase(PieceType.Wall, ManipulationAxis.Z, 4f, 3f, 1.2f)]
        [TestCase(PieceType.Slab, ManipulationAxis.X, 5f, 0.2f, 3f)]
        [TestCase(PieceType.Slab, ManipulationAxis.Y, 4f, 1.2f, 3f)]
        [TestCase(PieceType.Slab, ManipulationAxis.Z, 4f, 0.2f, 4f)]
        public void ResizeChangesSemanticAxisWithoutScalingTransform(PieceType type, ManipulationAxis axis, float x, float y, float z)
        {
            PieceData piece = type == PieceType.Wall
                ? new PieceData(WallId, Base, new WallDimensions(4f, 3f, 0.2f))
                : new PieceData(SlabId, Base, new SlabDimensions(4f, 0.2f, 3f));
            var session = new ManipulationSession(piece, ManipulationMode.Resize, axis, Vector2.zero, Vector2.right, 100f);
            PieceData changed = session.Evaluate(Vector2.right * 100f);
            Assert.That((changed.Dimensions.X, changed.Dimensions.Y, changed.Dimensions.Z), Is.EqualTo((x, y, z)));
            Assert.That(changed.Type, Is.EqualTo(type));
            Assert.That(changed.Transform, Is.EqualTo(piece.Transform));
            PieceDimensions minimum = session.Evaluate(Vector2.left * 10000f).Dimensions;
            float clamped = axis == ManipulationAxis.X ? minimum.X : axis == ManipulationAxis.Y ? minimum.Y : minimum.Z;
            Assert.That(clamped, Is.EqualTo(ManipulationSession.MinimumDimension));
        }

        [TestCase(PieceType.Wall)]
        [TestCase(PieceType.Slab)]
        public void MoveAndRotatePreserveSemanticParameters(PieceType type)
        {
            PieceData piece = type == PieceType.Wall
                ? new PieceData(WallId, Base, new WallDimensions(4f, 3f, 0.2f))
                : new PieceData(SlabId, Base, new SlabDimensions(4f, 0.2f, 3f));
            var move = new ManipulationSession(piece, ManipulationMode.Move, ManipulationAxis.X, Vector2.zero, Vector2.right, 100f);
            var rotate = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y, Vector2.zero, Vector2.right, 1f);
            PieceData moved = move.Evaluate(Vector2.right * 100f);
            PieceData rotated = rotate.Evaluate(Vector2.right * 90f);
            Assert.That(moved.Transform.Position, Is.EqualTo(Vector3.right));
            Assert.That(Quaternion.Angle(rotated.Transform.Rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.001f));
            Assert.That(moved.Dimensions, Is.EqualTo(piece.Dimensions));
            Assert.That(rotated.Dimensions, Is.EqualTo(piece.Dimensions));
        }
    }
}
