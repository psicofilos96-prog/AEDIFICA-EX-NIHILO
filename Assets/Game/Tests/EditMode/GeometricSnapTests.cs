using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class GeometricSnapTests
    {
        private static readonly PieceId A = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        private static readonly PieceId B = PieceId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        private static readonly PieceId C = PieceId.Parse("cccccccccccccccccccccccccccccccc");

        private static PieceData Piece(PieceId id, PieceType type, Vector3 position, float yaw = 0f)
        {
            var pose = new PieceTransform(position, Quaternion.Euler(0f, yaw, 0f));
            switch (type)
            {
                case PieceType.Block: return new PieceData(id, pose, new BlockDimensions(1f, 1f, 1f));
                case PieceType.Wall: return new PieceData(id, pose, new WallDimensions(1f, 1f, 1f));
                case PieceType.Slab: return new PieceData(id, pose, new SlabDimensions(1f, 1f, 1f));
                case PieceType.Column: return new PieceData(id, pose, new ColumnDimensions(1f, 1f, 1f));
                case PieceType.Beam: return new PieceData(id, pose, new BeamDimensions(1f, 1f, 1f));
                case PieceType.Parapet: return new PieceData(id, pose, new ParapetDimensions(1f, 1f, 1f));
                default: throw new System.ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static SnapSettings Settings(GeometricSnapKind kind) => new SnapSettings
        {
            CaptureDistance = 0.25f, ReleaseDistance = 0.4f,
            EndpointSnapEnabled = kind == GeometricSnapKind.Endpoint,
            EdgeSnapEnabled = kind == GeometricSnapKind.Edge,
            SurfaceSnapEnabled = kind == GeometricSnapKind.Surface
        };

        private static ManipulationSession Move(PieceData piece, SnapSettings settings) =>
            new ManipulationSession(piece, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings);

        [TestCase(GeometricSnapKind.Surface)]
        [TestCase(GeometricSnapKind.Edge)]
        [TestCase(GeometricSnapKind.Endpoint)]
        public void EachKindCapturesLocallyAndIgnoresDistantCandidates(GeometricSnapKind kind)
        {
            PieceData moving = Piece(A, PieceType.Block, Vector3.zero);
            PieceData target = Piece(B, PieceType.Block, new Vector3(1.1f, 0f, 0f));
            var settings = Settings(kind);
            var resolver = new SnapResolver();
            PieceData snapped = resolver.Resolve(moving, Move(moving, settings), settings, new[] { moving, target });
            Assert.That(resolver.HasTarget, Is.True, kind.ToString());
            Assert.That(resolver.ActiveKind, Is.EqualTo(kind));
            Assert.That(snapped.Transform.Position.x, Is.EqualTo(0.1f).Within(0.0001f), $"kind={kind}, target={target.Id}");
            Assert.That(snapped.Dimensions, Is.EqualTo(moving.Dimensions));
            resolver.Reset();
            PieceData distant = Piece(B, PieceType.Block, new Vector3(1.6f, 0f, 0f));
            Assert.That(resolver.Resolve(moving, Move(moving, settings), settings, new[] { distant }).Transform.Position,
                Is.EqualTo(moving.Transform.Position), kind.ToString());
            Assert.That(resolver.HasTarget, Is.False);
        }

        [Test]
        public void HysteresisRetainsTargetUntilReleaseDistance()
        {
            PieceData moving = Piece(A, PieceType.Block, Vector3.zero);
            PieceData target = Piece(B, PieceType.Block, new Vector3(1.1f, 0f, 0f));
            var settings = Settings(GeometricSnapKind.Endpoint);
            var session = Move(moving, settings);
            var resolver = new SnapResolver();
            resolver.Resolve(moving, session, settings, new[] { target });
            Assert.That(resolver.HasTarget, Is.True);
            PieceData withinRelease = session.Evaluate(new Vector2(-20f, 0f));
            resolver.Resolve(withinRelease, session, settings, new[] { target });
            Assert.That(resolver.HasTarget, Is.True);
            PieceData beyondRelease = session.Evaluate(new Vector2(-50f, 0f));
            resolver.Resolve(beyondRelease, session, settings, new[] { target });
            Assert.That(resolver.HasTarget, Is.False);
        }

        [Test]
        public void SurfaceSnapRemovesSmallGapAndSmallPenetration()
        {
            PieceData moving = Piece(A, PieceType.Block, Vector3.zero);
            var settings = Settings(GeometricSnapKind.Surface);
            foreach (float centerX in new[] { 1.1f, 0.9f })
            {
                PieceData target = Piece(B, PieceType.Block, new Vector3(centerX, 0f, 0f));
                PieceData result = new SnapResolver().Resolve(moving, Move(moving, settings), settings, new[] { target });
                Assert.That(result.Transform.Position.x + 0.5f, Is.EqualTo(centerX - 0.5f).Within(0.0001f),
                    $"target={target.Id}, centerX={centerX}");
            }
        }

        [Test]
        public void PriorityAndStableIdBreakEqualDistanceTiesRegardlessOfCandidateOrder()
        {
            PieceData moving = Piece(C, PieceType.Block, Vector3.zero);
            PieceData left = Piece(A, PieceType.Block, new Vector3(-1.1f, 0f, 0f));
            PieceData right = Piece(B, PieceType.Block, new Vector3(1.1f, 0f, 0f));
            var settings = new SnapSettings { EndpointSnapEnabled = true, EdgeSnapEnabled = true,
                SurfaceSnapEnabled = true, CaptureDistance = 0.25f, ReleaseDistance = 0.4f };
            var one = new SnapResolver();
            var two = new SnapResolver();
            PieceData first = one.Resolve(moving, Move(moving, settings), settings, new[] { right, left });
            PieceData second = two.Resolve(moving, Move(moving, settings), settings, new[] { left, right });
            Assert.That(one.ActiveKind, Is.EqualTo(GeometricSnapKind.Endpoint));
            Assert.That(one.TargetId, Is.EqualTo(A));
            Assert.That(two.TargetId, Is.EqualTo(A));
            Assert.That(Vector3.Distance(first.Transform.Position, second.Transform.Position), Is.LessThan(0.0001f));
        }

        [Test]
        public void MoreDistantEndpointDoesNotOverrideCloserEdgeOrSurface()
        {
            PieceData moving = Piece(A, PieceType.Block, Vector3.zero);
            PieceData target = Piece(B, PieceType.Block, new Vector3(1.04f, 0.15f, 0f));
            var settings = new SnapSettings { EndpointSnapEnabled = true, EdgeSnapEnabled = true,
                SurfaceSnapEnabled = true };
            var resolver = new SnapResolver();
            PieceData result = resolver.Resolve(moving, Move(moving, settings), settings, new[] { target });
            Assert.That(resolver.HasTarget, Is.True);
            Assert.That(resolver.ActiveKind, Is.Not.EqualTo(GeometricSnapKind.Endpoint));
            Assert.That(result.Transform.Position.x, Is.EqualTo(0.04f).Within(0.0001f));
        }

        [TestCase(PieceType.Block, PieceType.Block, 0f)]
        [TestCase(PieceType.Block, PieceType.Wall, 45f)]
        [TestCase(PieceType.Wall, PieceType.Wall, 90f)]
        [TestCase(PieceType.Wall, PieceType.Slab, 0f)]
        [TestCase(PieceType.Slab, PieceType.Slab, 45f)]
        [TestCase(PieceType.Column, PieceType.Beam, 0f)]
        [TestCase(PieceType.Column, PieceType.Slab, 45f)]
        [TestCase(PieceType.Beam, PieceType.Beam, 90f)]
        [TestCase(PieceType.Beam, PieceType.Column, 0f)]
        [TestCase(PieceType.Parapet, PieceType.Slab, 45f)]
        [TestCase(PieceType.Parapet, PieceType.Wall, 90f)]
        [TestCase(PieceType.Parapet, PieceType.Parapet, 0f)]
        public void AllSnapKindsUseRotatedParametricGeometryAcrossPieceTypes(PieceType movingType, PieceType targetType, float yaw)
        {
            Vector3 axis = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            PieceData moving = Piece(A, movingType, Vector3.zero, yaw);
            PieceData target = Piece(B, targetType, axis * 1.1f, yaw);
            foreach (GeometricSnapKind kind in new[] { GeometricSnapKind.Surface, GeometricSnapKind.Edge, GeometricSnapKind.Endpoint })
            {
                var settings = Settings(kind);
                var resolver = new SnapResolver();
                PieceData result = resolver.Resolve(moving, Move(moving, settings), settings, new[] { target });
                string context = $"kind={kind}, moving={movingType}, target={targetType}, yaw={yaw}, error={Vector3.Distance(result.Transform.Position, axis * 0.1f):R}";
                Assert.That(resolver.HasTarget, Is.True, context);
                Assert.That(Vector3.Distance(result.Transform.Position, axis * 0.1f), Is.LessThan(0.0001f), context);
                Assert.That(result.Dimensions, Is.EqualTo(moving.Dimensions), context);
            }
        }

        [Test]
        public void GridResultIsRefinedByGeometricSnapAndDisabledModesDoNothing()
        {
            PieceData moving = Piece(A, PieceType.Block, Vector3.zero);
            PieceData target = Piece(B, PieceType.Block, new Vector3(1.6f, 0f, 0f));
            var settings = Settings(GeometricSnapKind.Endpoint);
            settings.PositionSnapEnabled = true;
            var session = Move(moving, settings);
            PieceData grid = session.Evaluate(new Vector2(49f, 0f));
            Assert.That(grid.Transform.Position.x, Is.EqualTo(0.5f));
            PieceData refined = new SnapResolver().Resolve(grid, session, settings, new[] { target });
            Assert.That(refined.Transform.Position.x, Is.EqualTo(0.6f).Within(0.0001f));
            settings.EndpointSnapEnabled = false;
            Assert.That(new SnapResolver().Resolve(grid, session, settings, new[] { target }).Transform.Position,
                Is.EqualTo(grid.Transform.Position));
            Assert.That(new SnapResolver().Resolve(grid, session, Settings(GeometricSnapKind.Endpoint), new[] { moving }).Transform.Position,
                Is.EqualTo(grid.Transform.Position));
        }

        [Test]
        public void FaceResizeSnapsMovingFaceWithoutMovingOppositeFaceOrDrifting()
        {
            PieceData moving = Piece(A, PieceType.Wall, Vector3.zero);
            PieceData target = Piece(B, PieceType.Wall, new Vector3(1.1f, 0f, 0f));
            var settings = Settings(GeometricSnapKind.Endpoint);
            var session = new ManipulationSession(moving, ManipulationMode.Resize, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings, ResizeMode.Face, 1);
            var resolver = new SnapResolver();
            PieceData result = resolver.Resolve(session.Evaluate(Vector2.zero), session, settings, new[] { target });
            Assert.That(result.Dimensions.X, Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(result.Transform.Position.x - result.Dimensions.X * 0.5f, Is.EqualTo(-0.5f).Within(0.0001f));
            resolver.Resolve(session.Evaluate(new Vector2(-1000f, 0f)), session, settings, new[] { target });
            PieceData repeated = resolver.Resolve(session.Evaluate(Vector2.zero), session, settings, new[] { target });
            Assert.That(Vector3.Distance(repeated.Transform.Position, result.Transform.Position), Is.LessThan(0.0001f));
            Assert.That(repeated.Dimensions, Is.EqualTo(result.Dimensions));
            PieceData nearOpposite = Piece(B, PieceType.Wall, new Vector3(0.05f, 0f, 0f));
            var clampSession = new ManipulationSession(moving, ManipulationMode.Resize, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings, ResizeMode.Face, 1);
            PieceData clamped = clampSession.Evaluate(new Vector2(-1000f, 0f));
            PieceData limited = new SnapResolver().Resolve(clamped, clampSession, settings, new[] { nearOpposite });
            Assert.That(limited.Dimensions.X, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(limited.Transform.Position.x - limited.Dimensions.X * 0.5f, Is.EqualTo(-0.5f).Within(0.0001f));
        }

        [TestCase(GeometricSnapKind.Surface)]
        [TestCase(GeometricSnapKind.Edge)]
        [TestCase(GeometricSnapKind.Endpoint)]
        public void FaceResizeSnapsAllLocalFacesAtRotatedYaw(GeometricSnapKind kind)
        {
            foreach (float yaw in new[] { 0f, 45f, 90f })
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            foreach (int sign in new[] { -1, 1 })
            {
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 outward = rotation * ManipulationSession.AxisVector(axis) * sign;
                PieceData moving = Piece(A, PieceType.Wall, Vector3.zero, yaw);
                PieceData target = Piece(B, PieceType.Wall, outward * 1.1f, yaw);
                var settings = Settings(kind);
                var session = new ManipulationSession(moving, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f, settings, ResizeMode.Face, sign);
                PieceData result = new SnapResolver().Resolve(moving, session, settings, new[] { target });
                float initialDimension = axis == ManipulationAxis.X ? moving.Dimensions.X
                    : axis == ManipulationAxis.Y ? moving.Dimensions.Y : moving.Dimensions.Z;
                float finalDimension = axis == ManipulationAxis.X ? result.Dimensions.X
                    : axis == ManipulationAxis.Y ? result.Dimensions.Y : result.Dimensions.Z;
                Vector3 initialCenter = moving.Transform.Position + rotation * Vector3.up * (moving.Dimensions.Y * 0.5f);
                Vector3 finalCenter = result.Transform.Position + rotation * Vector3.up * (result.Dimensions.Y * 0.5f);
                Vector3 fixedBefore = initialCenter - outward * initialDimension * 0.5f;
                Vector3 fixedAfter = finalCenter - outward * finalDimension * 0.5f;
                string context = $"kind={kind}, axis={axis}, sign={sign}, yaw={yaw}, expectedFace={fixedBefore}, actualFace={fixedAfter}";
                Assert.That(finalDimension, Is.EqualTo(1.1f).Within(0.0001f), context);
                Assert.That(Vector3.Distance(fixedBefore, fixedAfter), Is.LessThan(0.0001f), context);
            }
        }
    }
}
