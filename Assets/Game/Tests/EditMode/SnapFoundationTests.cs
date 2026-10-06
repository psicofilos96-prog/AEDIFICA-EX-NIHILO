using System;
using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class SnapFoundationTests
    {
        private static readonly PieceId Id = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        [TestCase(1f, 0.49f, 0f)]
        [TestCase(1f, 0.5f, 1f)]
        [TestCase(1f, -0.5f, -1f)]
        [TestCase(0.5f, 0.24f, 0f)]
        [TestCase(0.5f, 0.26f, 0.5f)]
        [TestCase(0.5f, 0.74f, 0.5f)]
        [TestCase(0.5f, 0.76f, 1f)]
        [TestCase(0.25f, 0.375f, 0.5f)]
        [TestCase(0.25f, -0.375f, -0.5f)]
        [TestCase(0.5f, 0f, 0f)]
        [TestCase(0.5f, 1f, 1f)]
        public void PositionQuantizationUsesWorldZeroAndHalfAwayFromZero(float increment, float input, float expected)
        {
            Assert.That(SnapPolicy.Quantize(input, increment), Is.EqualTo(expected));
        }

        [Test]
        public void PositionQuantizesAllWorldAxesOrReturnsFreeVector()
        {
            var value = new Vector3(-0.26f, 0.76f, 0.12f);
            Assert.That(SnapPolicy.Quantize(value, 0.5f), Is.EqualTo(new Vector3(-0.5f, 1f, 0f)));
            Assert.That(SnapPolicy.Quantize(value, 0f, false), Is.EqualTo(value));
        }

        [TestCase(90f, 46f, 90f)]
        [TestCase(45f, 22.5f, 45f)]
        [TestCase(15f, 7f, 0f)]
        [TestCase(15f, 8f, 15f)]
        [TestCase(15f, 22f, 15f)]
        [TestCase(15f, 23f, 30f)]
        [TestCase(5f, -2.5f, -5f)]
        [TestCase(15f, 360f, 360f)]
        [TestCase(15f, -15f, -15f)]
        public void RotationQuantizationUsesConfiguredDegrees(float increment, float input, float expected)
        {
            Assert.That(SnapPolicy.Quantize(input, increment), Is.EqualTo(expected));
            Assert.That(SnapPolicy.Quantize(input, 0f, false), Is.EqualTo(input));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void EnabledSnapRejectsInvalidIncrements(float invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SnapPolicy.Quantize(1f, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => SnapPolicy.FromSession(0f, 1f, invalid));
            Assert.That(SnapPolicy.Quantize(1.23f, invalid, false), Is.EqualTo(1.23f));
            var settings = new SnapSettings { PositionIncrement = invalid, RotationIncrementDegrees = invalid };
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.TogglePosition());
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToggleRotation());
            Assert.That(settings.PositionSnapEnabled || settings.RotationSnapEnabled, Is.False);
        }

        [Test]
        public void SessionPreservesUnalignedStartUntilFirstGridStep()
        {
            Assert.That(SnapPolicy.FromSession(0.2f, 0.2f, 0.5f), Is.EqualTo(0.2f));
            Assert.That(SnapPolicy.FromSession(0.2f, 0.3f, 0.5f), Is.EqualTo(0.2f));
            Assert.That(SnapPolicy.FromSession(0.2f, 0.36f, 0.5f), Is.EqualTo(0.5f));
            Assert.That(SnapPolicy.FromSession(0.2f, 0.12f, 0.5f), Is.EqualTo(0.2f));
            Assert.That(SnapPolicy.FromSession(0.2f, 0.08f, 0.5f), Is.EqualTo(0f));
            Assert.That(SnapPolicy.FromSession(359f, 361f, 15f), Is.EqualTo(360f));
        }

        [TestCase(PieceType.Block, ManipulationAxis.X)]
        [TestCase(PieceType.Wall, ManipulationAxis.Y)]
        [TestCase(PieceType.Slab, ManipulationAxis.Z)]
        public void MoveSnapIsDeterministicForEveryPieceFamilyAndAxis(PieceType type, ManipulationAxis axis)
        {
            PieceData piece = MakePiece(type, new Vector3(0.2f, 1.2f, -0.2f));
            var settings = new SnapSettings { PositionSnapEnabled = true, PositionIncrement = 0.5f };
            var session = new ManipulationSession(piece, ManipulationMode.Move, axis, Vector2.zero, Vector2.right, 100f, settings);
            Assert.That(session.Evaluate(Vector2.zero).Transform, Is.EqualTo(piece.Transform));
            PieceData first = session.Evaluate(Vector2.right * 36f);
            PieceData repeated = session.Evaluate(Vector2.right * 36f);
            float expected = axis == ManipulationAxis.Z ? 0f : axis == ManipulationAxis.Y ? 1.5f : 0.5f;
            Assert.That(first.Transform.Position[(int)axis], Is.EqualTo(expected));
            Assert.That(repeated.Transform, Is.EqualTo(first.Transform));
            Assert.That(first.MaterialId, Is.EqualTo(piece.MaterialId));
            Assert.That(first.Dimensions, Is.EqualTo(piece.Dimensions));
            settings.PositionSnapEnabled = false; // An active session retains its captured policy.
            Assert.That(session.Evaluate(Vector2.right * 36f).Transform, Is.EqualTo(first.Transform));
            var free = new ManipulationSession(piece, ManipulationMode.Move, axis, Vector2.zero, Vector2.right, 100f, settings);
            Assert.That(free.Evaluate(Vector2.right * 36f).Transform.Position[(int)axis],
                Is.EqualTo(piece.Transform.Position[(int)axis] + 0.36f).Within(0.0001f));
        }

        [Test]
        public void RotationSnapUsesTotalGestureAndFreeRotationRemainsAvailable()
        {
            PieceData piece = MakePiece(PieceType.Wall, Vector3.zero);
            var settings = new SnapSettings { RotationSnapEnabled = true, RotationIncrementDegrees = 15f };
            var session = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y, Vector2.zero, Vector2.right, 1f, settings);
            Assert.That(session.Evaluate(Vector2.zero).Transform, Is.EqualTo(piece.Transform));
            Assert.That(Quaternion.Angle(session.Evaluate(Vector2.right * 14f).Transform.Rotation, Quaternion.identity), Is.LessThan(0.001f));
            PieceData snapped = session.Evaluate(Vector2.right * 16f);
            Assert.That(Quaternion.Angle(snapped.Transform.Rotation, Quaternion.Euler(0f, 15f, 0f)), Is.LessThan(0.001f));
            Assert.That(session.Evaluate(Vector2.right * 16f).Transform, Is.EqualTo(snapped.Transform));
            Assert.That(snapped.MaterialId, Is.EqualTo(piece.MaterialId));
            settings.RotationSnapEnabled = false;
            var free = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y, Vector2.zero, Vector2.right, 1f, settings);
            Assert.That(Quaternion.Angle(free.Evaluate(Vector2.right * 16f).Transform.Rotation, Quaternion.Euler(0f, 8f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void RotationSnapCrossesZeroWithoutIncrementalDrift()
        {
            var piece = new PieceData(Id, new PieceTransform(Vector3.zero, Quaternion.Euler(0f, 359f, 0f)),
                new BlockDimensions(1f, 1f, 1f));
            var settings = new SnapSettings { RotationSnapEnabled = true };
            var session = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 1f, settings);
            PieceData result = session.Evaluate(Vector2.right * 4f);
            Assert.That(Quaternion.Angle(result.Transform.Rotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(session.Evaluate(Vector2.right * 4f).Transform, Is.EqualTo(result.Transform));
        }

        [Test]
        public void ResizeIgnoresTransformSnapSettings()
        {
            PieceData piece = MakePiece(PieceType.Slab, Vector3.zero);
            var settings = new SnapSettings { PositionSnapEnabled = true, RotationSnapEnabled = true };
            var session = new ManipulationSession(piece, ManipulationMode.Resize, ManipulationAxis.Y, Vector2.zero, Vector2.right, 100f, settings);
            PieceData changed = session.Evaluate(Vector2.right * 100f);
            Assert.That(changed.SlabDimensions.Thickness, Is.EqualTo(piece.SlabDimensions.Thickness + 1f));
            Assert.That(changed.Transform, Is.EqualTo(piece.Transform));
        }

        private static PieceData MakePiece(PieceType type, Vector3 position)
        {
            var transform = new PieceTransform(position, Quaternion.identity);
            switch (type)
            {
                case PieceType.Block: return new PieceData(Id, transform, new BlockDimensions(2f, 1f, 3f)).WithMaterial(LabMaterialIds.Stone);
                case PieceType.Wall: return new PieceData(Id, transform, new WallDimensions(4f, 3f, 0.2f)).WithMaterial(LabMaterialIds.Stone);
                case PieceType.Slab: return new PieceData(Id, transform, new SlabDimensions(4f, 0.2f, 3f)).WithMaterial(LabMaterialIds.Stone);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
