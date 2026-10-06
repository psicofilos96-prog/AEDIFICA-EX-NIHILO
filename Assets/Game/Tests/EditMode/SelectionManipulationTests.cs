using System;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class SelectionManipulationTests
    {
        private static readonly PieceId IdA = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        private static readonly PieceId IdB = PieceId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        private static PieceData Block(PieceId id) => new PieceData(id,
            new PieceTransform(new Vector3(1f, 2f, 3f), Quaternion.identity), new BlockDimensions(2f, 1f, 3f));

        [Test]
        public void SelectionStartsEmptySwitchesAndClearsWithoutChangingPiece()
        {
            var selection = new SelectionState();
            PieceData piece = Block(IdA);
            Assert.That(selection.HasSelection, Is.False);
            selection.Select(IdA);
            Assert.That(selection.SelectedPieceId, Is.EqualTo(IdA));
            selection.Select(IdB);
            Assert.That(selection.SelectedPieceId, Is.EqualTo(IdB));
            selection.Clear();
            Assert.That(selection.HasSelection, Is.False);
            Assert.That(piece.Id, Is.EqualTo(IdA));
            Assert.That(piece.Transform.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.Throws<ArgumentException>(() => selection.Select(default));
        }

        [Test]
        public void ClickThresholdDistinguishesSelectionFromPanDrag()
        {
            Assert.That(ConstructionLabInteraction.IsClick(Vector2.zero, new Vector2(3f, 4f)), Is.True);
            Assert.That(ConstructionLabInteraction.IsClick(Vector2.zero, new Vector2(6f, 0f)), Is.False);
        }

        [Test]
        public void ControlledReplacementPreservesIdentityAndRejectsMismatches()
        {
            var world = new ConstructionWorld();
            PieceData original = Block(IdA);
            world.Add(original);
            PieceData moved = original.WithTransform(new PieceTransform(new Vector3(4f, 2f, 3f), Quaternion.identity));
            Assert.That(world.Replace(IdA, moved), Is.True);
            Assert.That(world.Count, Is.EqualTo(1));
            Assert.That(world.TryGet(IdA, out PieceData found), Is.True);
            Assert.That(found, Is.SameAs(moved));
            Assert.That(found.Id, Is.EqualTo(original.Id));
            Assert.That(found.Type, Is.EqualTo(PieceType.Block));
            Assert.That(original.Transform.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(world.Replace(IdA, Block(IdB)), Is.False);
            Assert.That(world.Replace(IdB, Block(IdB)), Is.False);
            Assert.That(world.Replace(default, moved), Is.False);
            Assert.Throws<ArgumentNullException>(() => world.Replace(IdA, null));
            Assert.Throws<ArgumentException>(() => original.WithTransform(default));
            Assert.Throws<ArgumentException>(() => original.WithBlockDimensions(default));
        }

        [TestCase(ManipulationAxis.X, 2f, 2f, 3f)]
        [TestCase(ManipulationAxis.Y, 1f, 3f, 3f)]
        [TestCase(ManipulationAxis.Z, 1f, 2f, 4f)]
        public void MoveChangesOnlyChosenWorldAxis(ManipulationAxis axis, float x, float y, float z)
        {
            var session = new ManipulationSession(Block(IdA), ManipulationMode.Move, axis,
                Vector2.zero, Vector2.right, 100f);
            PieceData moved = session.Evaluate(new Vector2(100f, 0f));
            Assert.That(moved.Transform.Position, Is.EqualTo(new Vector3(x, y, z)));
            Assert.That(moved.Id, Is.EqualTo(IdA));
            Assert.That(moved.BlockDimensions, Is.EqualTo(new BlockDimensions(2f, 1f, 3f)));
        }

        [Test]
        public void RotateChangesOnlyWorldYaw()
        {
            PieceData original = Block(IdA);
            var session = new ManipulationSession(original, ManipulationMode.Rotate, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 1f);
            PieceData rotated = session.Evaluate(new Vector2(90f, 0f));
            Assert.That(Quaternion.Angle(rotated.Transform.Rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.001f));
            Assert.That(rotated.Transform.Position, Is.EqualTo(original.Transform.Position));
            Assert.That(rotated.BlockDimensions, Is.EqualTo(original.BlockDimensions));
        }

        [TestCase(ManipulationAxis.X, 4f, 1f, 3f)]
        [TestCase(ManipulationAxis.Y, 2f, 3f, 3f)]
        [TestCase(ManipulationAxis.Z, 2f, 1f, 5f)]
        public void ResizeUpdatesDimensionsAndDerivedGeometry(ManipulationAxis axis, float width, float height, float depth)
        {
            PieceData original = Block(IdA);
            var session = new ManipulationSession(original, ManipulationMode.Resize, axis,
                Vector2.zero, Vector2.right, 100f);
            PieceData resized = session.Evaluate(new Vector2(200f, 0f));
            Assert.That(resized.BlockDimensions, Is.EqualTo(new BlockDimensions(width, height, depth)));
            Assert.That(resized.Transform, Is.EqualTo(original.Transform));
            Assert.That(resized.Id, Is.EqualTo(IdA));
            var geometry = BlockGeometryGenerator.Generate(resized.BlockDimensions);
            Assert.That(geometry.Bounds.min, Is.EqualTo(new Vector3(-width / 2f, 0f, -depth / 2f)));
            Assert.That(geometry.Bounds.max, Is.EqualTo(new Vector3(width / 2f, height, depth / 2f)));
        }

        [Test]
        public void ResizeClampsToToolMinimumWithoutChangingBase()
        {
            var session = new ManipulationSession(Block(IdA), ManipulationMode.Resize, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 100f);
            PieceData resized = session.Evaluate(new Vector2(-1000f, 0f));
            Assert.That(resized.BlockDimensions.Height, Is.EqualTo(ManipulationSession.MinimumDimension));
            Assert.That(BlockGeometryGenerator.Generate(resized.BlockDimensions).Bounds.min.y, Is.Zero);
        }

        [Test]
        public void SessionKeepsInitialStateForAnUndoableGesture()
        {
            PieceData initial = Block(IdA);
            var session = new ManipulationSession(initial, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f);
            session.Evaluate(new Vector2(50f, 0f));
            PieceData final = session.Evaluate(new Vector2(100f, 0f));
            Assert.That(session.InitialPiece, Is.SameAs(initial));
            Assert.That(final.Transform.Position.x, Is.EqualTo(2f));
        }
    }
}
