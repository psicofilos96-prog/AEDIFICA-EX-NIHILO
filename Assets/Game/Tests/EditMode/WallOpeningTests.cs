using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class WallOpeningTests
    {
        private static readonly PieceId WallId = PieceId.Parse("1234567890abcdef1234567890abcdef");

        private static PieceData Wall() => new PieceData(WallId,
            new PieceTransform(new Vector3(2f, 0f, 3f), Quaternion.identity),
            new WallDimensions(6f, 3f, 0.3f));

        private static WallOpening Door() => new WallOpening(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WallId, WallOpeningKind.Passage, 1f, 0f, 1.2f, 2f);

        private static WallOpening Window() => new WallOpening(Guid.Parse("22222222-2222-2222-2222-222222222222"),
            WallId, WallOpeningKind.Window, 3.5f, 1f, 1.2f, 1f);

        [Test]
        public void SolidWallUsesOriginalGeometry()
        {
            PieceData wall = Wall();
            BlockGeometry a = WallOpeningGeometryGenerator.Generate(wall);
            BlockGeometry b = BlockGeometryGenerator.GeneratePiece(wall.Dimensions);
            Assert.That(a.Vertices.Length, Is.EqualTo(b.Vertices.Length));
            Assert.That(a.Bounds, Is.EqualTo(b.Bounds));
            Assert.That(wall.Openings, Is.Empty);
        }

        [Test]
        public void PassageWindowAndMultipleOpeningsHaveValidExposedFaces()
        {
            PieceData wall = Wall().WithOpening(Door()).WithOpening(Window());
            BlockGeometry mesh = WallOpeningGeometryGenerator.Generate(wall);
            Assert.That(mesh.Bounds.size, Is.EqualTo(new Vector3(6f, 3f, 0.3f)));
            Assert.That(mesh.Triangles.Length, Is.GreaterThan(36));
            for (int i = 0; i < mesh.Triangles.Length; i += 3)
            {
                int ia = mesh.Triangles[i], ib = mesh.Triangles[i + 1], ic = mesh.Triangles[i + 2];
                Vector3 a = mesh.Vertices[ia], b = mesh.Vertices[ib], c = mesh.Vertices[ic];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                Assert.That(cross.sqrMagnitude, Is.GreaterThan(0.000001f), $"Degenerate triangle {i / 3}");
                Assert.That(Vector3.Dot(cross.normalized, mesh.Normals[ia]), Is.GreaterThan(0.99f),
                    $"Incorrect normal on triangle {i / 3}");
                Assert.That(float.IsNaN(a.x) || float.IsInfinity(a.x), Is.False);
            }
            Assert.That(wall.Openings[0].Id, Is.EqualTo(Door().Id));
            Assert.That(wall.Openings[1].Kind, Is.EqualTo(WallOpeningKind.Window));
        }

        [Test]
        public void RejectsInvalidCoordinatesOverlapWrongHostAndDuplicateId()
        {
            PieceData wall = Wall().WithOpening(Door());
            Assert.Throws<ArgumentException>(() => wall.WithOpening(Door()));
            Assert.Throws<ArgumentException>(() => wall.WithOpening(Window().WithRect(1.5f, 1f, 1.2f, 1f)));
            Assert.Throws<ArgumentException>(() => wall.WithOpening(Window().WithRect(5.5f, 1f, 1.2f, 1f)));
            Assert.Throws<ArgumentException>(() => wall.WithOpening(Window().WithRect(3f, 2.5f, 1f, 1f)));
            Assert.Throws<ArgumentException>(() => wall.WithOpening(Window().WithRect(3f, 0f, 1f, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Door().WithRect(1f, 0f, 0.1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallOpening(Guid.NewGuid(), WallId,
                WallOpeningKind.Window, float.NaN, 1f, 1f, 1f));
            Assert.Throws<ArgumentException>(() => wall.WithOpening(new WallOpening(Guid.NewGuid(),
                PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), WallOpeningKind.Window, 3f, 1f, 1f, 1f)));
        }

        [Test]
        public void ResizeWallClampsAtLastOpeningAndPreservesOpenings()
        {
            PieceData wall = Wall().WithOpening(Door()).WithOpening(Window());
            var xSession = new ManipulationSession(wall, ManipulationMode.Resize, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f);
            PieceData shortWall = xSession.ResizeToDimension(1f);
            Assert.That(shortWall.Dimensions.X, Is.GreaterThan(Window().Right + WallOpening.MinimumSolid));
            Assert.That(shortWall.Openings[1], Is.EqualTo(Window()));
            var ySession = new ManipulationSession(wall, ManipulationMode.Resize, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 100f, null, ResizeMode.Face);
            PieceData lowWall = ySession.ResizeToDimension(0.1f);
            Assert.That(lowWall.Dimensions.Y, Is.GreaterThan(Door().Top + WallOpening.MinimumSolid));
            Assert.That(lowWall.Openings[0], Is.EqualTo(Door()));
            Assert.Throws<ArgumentException>(() => wall.WithWallDimensions(new WallDimensions(2f, 3f, 0.3f)));
        }

        [TestCase(45f)]
        [TestCase(90f)]
        public void FaceResizeKeepsOppositeFaceFixedWithOpening(float yaw)
        {
            PieceData initial = Wall().WithOpening(Door()).WithTransform(
                new PieceTransform(new Vector3(2f, 0f, 3f), Quaternion.Euler(0f, yaw, 0f)));
            var session = new ManipulationSession(initial, ManipulationMode.Resize, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, null, ResizeMode.Face, 1);
            PieceData resized = session.ResizeToDimension(7f);
            Vector3 before = initial.Transform.Position + initial.Transform.Rotation * Vector3.left * (initial.Dimensions.X * 0.5f);
            Vector3 after = resized.Transform.Position + resized.Transform.Rotation * Vector3.left * (resized.Dimensions.X * 0.5f);
            Assert.That(Vector3.Distance(before, after), Is.LessThan(0.00001f));
            Assert.That(resized.Openings[0], Is.EqualTo(Door()));
        }

        [Test]
        public void SnapFeaturesNeverExposeFullFrontSurfaceAcrossOpening()
        {
            PieceData wall = Wall().WithOpening(Door());
            var features = new List<SnapFeature>();
            SnapGeometry.Collect(wall, features);
            int frontBackPatches = 0;
            foreach (SnapFeature feature in features)
                if (feature.Kind == GeometricSnapKind.Surface)
                {
                    Assert.That(feature.Index, Is.Not.EqualTo(2),
                        "No whole bottom face may span a passage to the floor.");
                    if (Mathf.Abs(Vector3.Dot(feature.Normal, Vector3.forward)) > 0.99f)
                    {
                        frontBackPatches++;
                        float localX = feature.A.x - wall.Transform.Position.x + wall.Dimensions.X * 0.5f;
                        float localY = feature.A.y - wall.Transform.Position.y;
                        Assert.That(localX > Door().Left && localX < Door().Right &&
                            localY > Door().Bottom && localY < Door().Top, Is.False,
                            "Snap must not expose a surface in the passage.");
                    }
                }
            Assert.That(frontBackPatches, Is.GreaterThan(0), "Solid wall patches remain snappable.");
            Assert.That(features.Exists(feature => feature.Kind == GeometricSnapKind.Edge &&
                feature.Index >= 12), Is.True, "Opening rim edges should be available for snap.");
        }

        [Test]
        public void EditMoveRotateAndRemoveKeepStableIdentityAndRestoreSolidWall()
        {
            PieceData wall = Wall().WithOpening(Door());
            WallOpening edited = Door().WithRect(1.3f, 0f, 1.5f, 2.2f);
            wall = wall.ReplaceOpening(edited);
            Assert.That(wall.Openings[0].Id, Is.EqualTo(Door().Id));
            PieceData moved = wall.WithTransform(new PieceTransform(new Vector3(8f, 0f, 4f),
                Quaternion.Euler(0f, 45f, 0f)));
            Assert.That(moved.Openings[0], Is.EqualTo(edited));
            Vector3 local = new Vector3(edited.Left - moved.Dimensions.X * 0.5f, edited.Bottom, 0f);
            Vector3 worldPoint = moved.Transform.Position + moved.Transform.Rotation * local;
            Vector3 recovered = Quaternion.Inverse(moved.Transform.Rotation) * (worldPoint - moved.Transform.Position);
            Assert.That(Vector3.Distance(recovered, local), Is.LessThan(0.00001f));
            PieceData restored = moved.WithoutOpening(edited.Id);
            Assert.That(restored.Openings, Is.Empty);
            Assert.That(WallOpeningGeometryGenerator.Generate(restored).Triangles.Length, Is.EqualTo(36));
        }
    }
}
