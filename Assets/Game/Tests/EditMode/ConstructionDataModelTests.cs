using System;
using System.Linq;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class ConstructionDataModelTests
    {
        private static readonly PieceId IdA = PieceId.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly PieceId IdB = PieceId.Parse("22222222-2222-2222-2222-222222222222");

        [Test]
        public void PieceIdsAreStableComparableAndRejectInvalidValues()
        {
            Assert.That(IdA, Is.EqualTo(PieceId.Parse("11111111111111111111111111111111")));
            Assert.That(IdA, Is.Not.EqualTo(IdB));
            Assert.That(IdA.CompareTo(IdB), Is.LessThan(0));
            Assert.That(IdA.ToString(), Is.EqualTo("11111111111111111111111111111111"));
            Assert.That(default(PieceId).IsValid, Is.False);
            Assert.That(PieceId.TryParse(Guid.Empty.ToString(), out _), Is.False);
            Assert.That(PieceId.TryParse("not-a-guid", out _), Is.False);
            Assert.Throws<ArgumentException>(() => PieceId.Parse("not-a-guid"));
        }

        [Test]
        public void TransformAcceptsFinitePositionAndNormalizesRotation()
        {
            var transform = new PieceTransform(new Vector3(2f, 3f, 4f), new Quaternion(0f, 0f, 0f, 2f));
            Assert.That(transform.Position, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(transform.Rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(transform.IsValid, Is.True);
            Assert.That(default(PieceTransform).IsValid, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TransformRejectsNonfinitePositionAndRotation(float invalid)
        {
            Assert.Throws<ArgumentException>(() => new PieceTransform(new Vector3(invalid, 0f, 0f), Quaternion.identity));
            Assert.Throws<ArgumentException>(() => new PieceTransform(Vector3.zero, new Quaternion(0f, invalid, 0f, 1f)));
        }

        [Test]
        public void TransformRejectsZeroRotation()
        {
            Assert.Throws<ArgumentException>(() => new PieceTransform(Vector3.zero, new Quaternion(0f, 0f, 0f, 0f)));
        }

        [Test]
        public void DimensionsRepresentPositiveFiniteMeters()
        {
            var dimensions = new BlockDimensions(2f, 1f, 3f);
            Assert.That(dimensions.IsValid, Is.True);
            Assert.That((dimensions.Width, dimensions.Height, dimensions.Depth), Is.EqualTo((2f, 1f, 3f)));
            Assert.That(default(BlockDimensions).IsValid, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void DimensionsRejectInvalidValuesOnEveryAxis(float invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlockDimensions(invalid, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlockDimensions(1f, invalid, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlockDimensions(1f, 1f, invalid));
        }

        [Test]
        public void PiecePreservesIdentityTypeTransformAndParameters()
        {
            var transform = new PieceTransform(new Vector3(1f, 2f, 3f), Quaternion.identity);
            var dimensions = new BlockDimensions(2f, 1f, 3f);
            var piece = new PieceData(IdA, transform, dimensions);
            Assert.That(piece.Id, Is.EqualTo(IdA));
            Assert.That(piece.Type, Is.EqualTo(PieceType.Block));
            Assert.That(piece.Transform, Is.EqualTo(transform));
            Assert.That(piece.BlockDimensions, Is.EqualTo(dimensions));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PieceData(IdA, PieceType.Unknown, transform, dimensions));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PieceData(IdA, (PieceType)999, transform, dimensions));
            Assert.Throws<ArgumentException>(() => new PieceData(default, transform, dimensions));
            Assert.Throws<ArgumentException>(() => new PieceData(IdA, default, dimensions));
            Assert.Throws<ArgumentException>(() => new PieceData(IdA, transform, default(BlockDimensions)));
        }

        [TestCase(PieceType.Column, "Width", "Height", "Depth")]
        [TestCase(PieceType.Beam, "Length", "Height", "Width")]
        [TestCase(PieceType.Parapet, "Length", "Height", "Thickness")]
        public void ArchitecturalFamiliesRetainSemanticDimensionsAndSupportExistingTools(
            PieceType type, string xName, string yName, string zName)
        {
            var transform = new PieceTransform(new Vector3(2f, 3f, 4f), Quaternion.identity);
            PieceData piece;
            switch (type)
            {
                case PieceType.Column: piece = new PieceData(IdA, transform, new ColumnDimensions(2f, 1f, 3f)); break;
                case PieceType.Beam: piece = new PieceData(IdA, transform, new BeamDimensions(2f, 1f, 3f)); break;
                case PieceType.Parapet: piece = new PieceData(IdA, transform, new ParapetDimensions(2f, 1f, 3f)); break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
            Assert.That(piece.Type, Is.EqualTo(type));
            Assert.That((piece.Dimensions.X, piece.Dimensions.Y, piece.Dimensions.Z), Is.EqualTo((2f, 1f, 3f)));
            Assert.That(piece.Dimensions.IsValid, Is.True);
            var geometry = BlockGeometryGenerator.GeneratePiece(piece.Dimensions);
            Assert.That(geometry.Bounds.center, Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
            Assert.That(geometry.Bounds.size, Is.EqualTo(new Vector3(2f, 1f, 3f)));
            Assert.That(piece.WithMaterial(LabMaterialIds.Stone).Dimensions, Is.EqualTo(piece.Dimensions));
            Assert.Throws<ArgumentException>(() => piece.WithDimensions(new PieceDimensions(new BlockDimensions(2f, 1f, 3f))));
            var handle = new GameObject("Dimension label test").AddComponent<GizmoHandle>();
            try
            {
                foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
                {
                    handle.Configure(ManipulationMode.Resize, axis);
                    handle.SetPieceType(type);
                    Assert.That(handle.SemanticDimension, Is.EqualTo(axis == ManipulationAxis.X ? xName
                        : axis == ManipulationAxis.Y ? yName : zName));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(handle.gameObject); }
            var move = new ManipulationSession(piece, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f).Evaluate(new Vector2(100f, 0f));
            Assert.That(move.Transform.Position, Is.EqualTo(new Vector3(3f, 3f, 4f)));
            Assert.That(move.Dimensions, Is.EqualTo(piece.Dimensions));
            var rotate = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 1f).Evaluate(new Vector2(90f, 0f));
            Assert.That(Quaternion.Angle(rotate.Transform.Rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.001f));
            Assert.That(rotate.Dimensions, Is.EqualTo(piece.Dimensions));
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            {
                var resized = new ManipulationSession(piece, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f).Evaluate(new Vector2(100f, 0f));
                Assert.That(resized.Dimensions.X, Is.EqualTo(axis == ManipulationAxis.X ? 3f : 2f));
                Assert.That(resized.Dimensions.Y, Is.EqualTo(axis == ManipulationAxis.Y ? 2f : 1f));
                Assert.That(resized.Dimensions.Z, Is.EqualTo(axis == ManipulationAxis.Z ? 4f : 3f));
            }
        }

        [Test]
        public void ArchitecturalDimensionsRejectInvalidValues()
        {
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnDimensions(invalid, 1f, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnDimensions(1f, invalid, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnDimensions(1f, 1f, invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => new BeamDimensions(invalid, 1f, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new BeamDimensions(1f, invalid, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new BeamDimensions(1f, 1f, invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ParapetDimensions(invalid, 1f, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ParapetDimensions(1f, invalid, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ParapetDimensions(1f, 1f, invalid));
            }
        }

        [Test]
        public void WorldAddsFindsEnumeratesAndRemovesByStableId()
        {
            var world = new ConstructionWorld();
            Assert.That(world.Count, Is.Zero);
            Assert.That(world.TryGet(IdA, out _), Is.False);
            Assert.That(world.Remove(IdA), Is.False);

            var transform = new PieceTransform(Vector3.zero, Quaternion.identity);
            var dimensions = new BlockDimensions(2f, 1f, 3f);
            var a = new PieceData(IdA, transform, dimensions);
            var b = new PieceData(IdB, transform, dimensions);
            Assert.That(world.Add(a), Is.True);
            Assert.That(world.Add(b), Is.True);
            Assert.That(world.Count, Is.EqualTo(2));
            Assert.That(world.Add(new PieceData(IdA, transform, dimensions)), Is.False);
            Assert.That(world.Count, Is.EqualTo(2));
            Assert.That(world.TryGet(IdA, out var found), Is.True);
            Assert.That(found, Is.SameAs(a));
            Assert.That(world.Pieces.Select(piece => piece.Id), Is.EquivalentTo(new[] { IdA, IdB }));
            Assert.That(world.Remove(IdA), Is.True);
            Assert.That(world.Remove(IdA), Is.False);
            Assert.That(world.TryGet(IdA, out _), Is.False);
            Assert.That(world.TryGet(IdB, out found), Is.True);
            Assert.That(found, Is.SameAs(b));
            Assert.That(world.Count, Is.EqualTo(1));
            Assert.That(world.Pieces.Single(), Is.SameAs(b));
        }

        [Test]
        public void WorldRejectsNullAndTreatsInvalidLookupAsMissing()
        {
            var world = new ConstructionWorld();
            Assert.Throws<ArgumentNullException>(() => world.Add(null));
            Assert.That(world.TryGet(default, out var piece), Is.False);
            Assert.That(piece, Is.Null);
            Assert.That(world.Remove(default), Is.False);
        }
    }
}
