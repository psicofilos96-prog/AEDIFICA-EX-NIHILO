using System;
using System.Linq;
using Aedifica.Construction;
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
            Assert.Throws<ArgumentException>(() => new PieceData(IdA, transform, default));
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
