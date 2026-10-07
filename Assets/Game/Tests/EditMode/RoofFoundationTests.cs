using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class RoofFoundationTests
    {
        private static readonly PieceId A = PieceId.Parse("30000000000000000000000000000001");
        private static readonly PieceId B = PieceId.Parse("30000000000000000000000000000002");

        private static PieceData Roof(PieceType type, float width = 4f, float depth = 3f, float rise = 1f,
            float yaw = 0f, PieceId? id = null, Vector3? position = null)
        {
            var pose = new PieceTransform(position ?? Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            PieceId pieceId = id ?? A;
            switch (type)
            {
                case PieceType.FlatRoof: return new PieceData(pieceId, pose, new FlatRoofDimensions(width, depth, 0.2f));
                case PieceType.ShedRoof: return new PieceData(pieceId, pose, new ShedRoofDimensions(width, depth, 0.2f, rise));
                case PieceType.GableRoof: return new PieceData(pieceId, pose, new GableRoofDimensions(width, depth, 0.2f, rise));
                case PieceType.HipRoof: return new PieceData(pieceId, pose, new HipRoofDimensions(width, depth, 0.2f, rise));
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        [Test]
        public void ValidSlopedParametersProduceValidTaggedDimensions()
        {
            foreach (PieceDimensions dimensions in new[] {
                new PieceDimensions(new ShedRoofDimensions(4f, 3f, 0.2f, 1f)),
                new PieceDimensions(new GableRoofDimensions(4f, 3f, 0.2f, 1f)),
                new PieceDimensions(new HipRoofDimensions(4f, 3f, 0.2f, 1f)) })
            {
                Assert.That(dimensions.IsSlopedRoof, Is.True, dimensions.Type.ToString());
                Assert.That(dimensions.IsValid, Is.True, dimensions.Type.ToString());
                Assert.That(dimensions.RoofThickness, Is.EqualTo(0.2f), dimensions.Type.ToString());
                Assert.That(dimensions.Rise, Is.EqualTo(1f), dimensions.Type.ToString());
                Assert.That(dimensions.Y, Is.EqualTo(dimensions.RoofThickness + dimensions.Rise), dimensions.Type.ToString());
                Assert.That(new PieceData(A, new PieceTransform(Vector3.zero, Quaternion.identity), dimensions).Dimensions,
                    Is.EqualTo(dimensions));
            }
        }

        [TestCase(PieceType.FlatRoof)]
        [TestCase(PieceType.ShedRoof)]
        [TestCase(PieceType.GableRoof)]
        [TestCase(PieceType.HipRoof)]
        public void RoofDataPreservesTypeMaterialAndSingleRiseSource(PieceType type)
        {
            PieceData piece = Roof(type).WithMaterial(LabMaterialIds.Stone);
            Assert.That(piece.Type, Is.EqualTo(type));
            Assert.That(piece.Dimensions.X, Is.EqualTo(4f));
            Assert.That(piece.Dimensions.Z, Is.EqualTo(3f));
            Assert.That(piece.Dimensions.RoofThickness, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(piece.Dimensions.Y, Is.EqualTo(type == PieceType.FlatRoof ? 0.2f : 1.2f).Within(0.00001f));
            Assert.That(piece.MaterialId, Is.EqualTo(LabMaterialIds.Stone));
            Assert.That(piece.WithTransform(new PieceTransform(Vector3.right, Quaternion.identity)).Dimensions,
                Is.EqualTo(piece.Dimensions));
            if (type == PieceType.FlatRoof)
                Assert.Throws<InvalidOperationException>(() => piece.WithRise(2f));
            else
            {
                PieceData taller = piece.WithRise(2f);
                Assert.That(taller.Dimensions.Rise, Is.EqualTo(2f));
                Assert.That(taller.Dimensions.Y, Is.EqualTo(2.2f).Within(0.00001f));
                Assert.That(taller.Dimensions.RoofThickness, Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(taller.MaterialId, Is.EqualTo(piece.MaterialId));
                Assert.That(piece.Dimensions.Rise, Is.EqualTo(1f));
                Assert.That(taller.Dimensions, Is.Not.EqualTo(piece.Dimensions));
                Assert.Throws<InvalidOperationException>(() => piece.Dimensions.Resize(1, 2f));
            }
            Assert.Throws<ArgumentException>(() => piece.WithDimensions(new PieceDimensions(new SlabDimensions(4f, 0.2f, 3f))));
        }

        [Test]
        public void RoofParametersRejectValuesBelowMinimumAndNonfinite()
        {
            foreach (float invalid in new[] { 0f, 0.09f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new FlatRoofDimensions(invalid, 2f, 0.2f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ShedRoofDimensions(2f, invalid, 0.2f, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new GableRoofDimensions(2f, 2f, invalid, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new HipRoofDimensions(2f, 2f, 0.2f, invalid));
            }
        }

        [Test]
        public void FlatRoofUsesValidPrismGeometry()
        {
            BlockGeometry geometry = BlockGeometryGenerator.GeneratePiece(Roof(PieceType.FlatRoof).Dimensions);
            Assert.That(geometry.Bounds.min, Is.EqualTo(new Vector3(-2f, 0f, -1.5f)));
            Assert.That(geometry.Bounds.max, Is.EqualTo(new Vector3(2f, 0.2f, 1.5f)));
            Assert.That(geometry.Triangles.Length, Is.EqualTo(36));
        }

        [Test]
        public void GridAndAngleSnapContinueToApplyToRoofs()
        {
            PieceData piece = Roof(PieceType.GableRoof);
            var settings = new SnapSettings { PositionSnapEnabled = true, PositionIncrement = 0.5f,
                RotationSnapEnabled = true, RotationIncrementDegrees = 15f };
            var move = new ManipulationSession(piece, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings).Evaluate(new Vector2(49f, 0f));
            Assert.That(move.Transform.Position.x, Is.EqualTo(0.5f));
            Assert.That(move.Dimensions, Is.EqualTo(piece.Dimensions));
            var rotate = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 1f, settings).Evaluate(new Vector2(28f, 0f));
            Assert.That(Quaternion.Angle(rotate.Transform.Rotation, Quaternion.Euler(0f, 15f, 0f)),
                Is.LessThan(0.001f));
            Assert.That(rotate.Dimensions, Is.EqualTo(piece.Dimensions));
        }

        [TestCase(PieceType.ShedRoof, 4f, 3f)]
        [TestCase(PieceType.GableRoof, 4f, 3f)]
        [TestCase(PieceType.HipRoof, 4f, 3f)]
        [TestCase(PieceType.HipRoof, 3f, 4f)]
        [TestCase(PieceType.HipRoof, 4f, 4f)]
        public void RoofMeshIsFiniteClosedAndHasExpectedRidge(PieceType type, float width, float depth)
        {
            PieceData piece = Roof(type, width, depth);
            RoofProfile profile = RoofProfile.Create(piece.Dimensions);
            BlockGeometry geometry = RoofGeometryGenerator.Generate(piece.Dimensions);
            Assert.That(geometry.Bounds.min, Is.EqualTo(new Vector3(-width / 2f, 0f, -depth / 2f)));
            Assert.That(geometry.Bounds.max, Is.EqualTo(new Vector3(width / 2f, 1.2f, depth / 2f)));
            Assert.That(geometry.Vertices.Length, Is.EqualTo(geometry.Normals.Length));
            Assert.That(geometry.Vertices.Length, Is.EqualTo(geometry.UVs.Length));
            Assert.That(geometry.Triangles.Length, Is.EqualTo(geometry.Vertices.Length));
            var edgeCounts = new Dictionary<string, int>();
            string VertexKey(Vector3 vertex) => $"{vertex.x:R},{vertex.y:R},{vertex.z:R}";
            void CountEdge(Vector3 start, Vector3 end)
            {
                string aKey = VertexKey(start), bKey = VertexKey(end);
                string key = string.CompareOrdinal(aKey, bKey) < 0 ? aKey + "/" + bKey : bKey + "/" + aKey;
                edgeCounts.TryGetValue(key, out int count);
                edgeCounts[key] = count + 1;
            }
            for (int i = 0; i < geometry.Vertices.Length; i += 3)
            {
                Vector3 a = geometry.Vertices[i], b = geometry.Vertices[i + 1], c = geometry.Vertices[i + 2];
                Assert.That(float.IsNaN(a.x) || float.IsNaN(a.y) || float.IsNaN(a.z), Is.False);
                Assert.That(float.IsInfinity(a.x) || float.IsInfinity(a.y) || float.IsInfinity(a.z), Is.False);
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-10f));
                Assert.That(Mathf.Abs(geometry.Normals[i].magnitude - 1f), Is.LessThan(0.0001f));
                Assert.That(geometry.Triangles[i], Is.EqualTo(i));
                CountEdge(a, b); CountEdge(b, c); CountEdge(c, a);
            }
            foreach (int count in edgeCounts.Values)
                Assert.That(count, Is.EqualTo(2), $"type={type}, width={width}, depth={depth}: roof boundary must be closed");
            Assert.That(profile.Eaves[0].y, Is.EqualTo(0.2f));
            Assert.That(profile.Eaves[1].y, Is.EqualTo(0.2f));
            if (type == PieceType.ShedRoof)
            {
                Assert.That(profile.Eaves[2].y, Is.EqualTo(1.2f));
                Assert.That(profile.Eaves[3].y, Is.EqualTo(1.2f));
            }
            else
            {
                Assert.That(profile.Eaves[2].y, Is.EqualTo(0.2f));
                Assert.That(profile.Eaves[3].y, Is.EqualTo(0.2f));
                Assert.That(profile.Ridge.Length, Is.EqualTo(width == depth && type == PieceType.HipRoof ? 1 : 2));
                foreach (Vector3 ridge in profile.Ridge) Assert.That(ridge.y, Is.EqualTo(1.2f));
                if (type == PieceType.GableRoof) Assert.That(profile.Ridge[0].z, Is.EqualTo(0f));
                if (type == PieceType.HipRoof && width > depth)
                    Assert.That(profile.Ridge[1].x - profile.Ridge[0].x, Is.EqualTo(width - depth).Within(0.00001f));
                if (type == PieceType.HipRoof && depth > width)
                    Assert.That(profile.Ridge[1].z - profile.Ridge[0].z, Is.EqualTo(depth - width).Within(0.00001f));
                if (type == PieceType.HipRoof && width == depth)
                    Assert.That(profile.Ridge[0], Is.EqualTo(new Vector3(0f, 1.2f, 0f)));
            }
        }

        [TestCase(PieceType.FlatRoof)]
        [TestCase(PieceType.ShedRoof)]
        [TestCase(PieceType.GableRoof)]
        [TestCase(PieceType.HipRoof)]
        public void RoofMoveRotateAndFootprintFaceResizeStayDeterministic(PieceType type)
        {
            foreach (float yaw in new[] { 0f, 45f, 90f })
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Z })
            foreach (int sign in new[] { -1, 1 })
            {
                PieceData initial = Roof(type, yaw: yaw);
                var move = new ManipulationSession(initial, ManipulationMode.Move, ManipulationAxis.X,
                    Vector2.zero, Vector2.right, 100f).Evaluate(new Vector2(100f, 0f));
                Assert.That(move.Transform.Position.x, Is.EqualTo(1f));
                Assert.That(move.Dimensions, Is.EqualTo(initial.Dimensions));
                var rotate = new ManipulationSession(initial, ManipulationMode.Rotate, ManipulationAxis.Y,
                    Vector2.zero, Vector2.right, 1f).Evaluate(new Vector2(90f, 0f));
                Assert.That(rotate.Dimensions, Is.EqualTo(initial.Dimensions));
                var bilateral = new ManipulationSession(initial, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f).Evaluate(new Vector2(100f, 0f));
                Assert.That(Dimension(bilateral, axis), Is.EqualTo(Dimension(initial, axis) + 1f));
                Assert.That(bilateral.Transform.Position, Is.EqualTo(initial.Transform.Position));
                var session = new ManipulationSession(initial, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f, null, ResizeMode.Face, sign);
                Vector3 fixedBefore = OppositeFootprintEdge(initial, axis, sign);
                foreach (float drag in new[] { 50f, -50f, -10000f, 50f })
                {
                    PieceData changed = session.Evaluate(new Vector2(drag, 0f));
                    Assert.That(Dimension(changed, axis), Is.GreaterThanOrEqualTo(0.1f));
                    Assert.That(Vector3.Distance(OppositeFootprintEdge(changed, axis, sign), fixedBefore),
                        Is.LessThan(0.0001f), $"type={type}, yaw={yaw}, axis={axis}, sign={sign}, drag={drag}");
                    Assert.That(changed.Dimensions.Rise, Is.EqualTo(initial.Dimensions.Rise));
                    Assert.That(changed.Dimensions.RoofThickness, Is.EqualTo(initial.Dimensions.RoofThickness).Within(0.00001f));
                }
                PieceData repeated = session.Evaluate(new Vector2(50f, 0f));
                Assert.That(repeated.Dimensions, Is.EqualTo(session.Evaluate(new Vector2(50f, 0f)).Dimensions));
            }
        }

        private static float Dimension(PieceData piece, ManipulationAxis axis) =>
            axis == ManipulationAxis.X ? piece.Dimensions.X : piece.Dimensions.Z;

        private static Vector3 OppositeFootprintEdge(PieceData piece, ManipulationAxis axis, int sign) =>
            piece.Transform.Position - piece.Transform.Rotation * ManipulationSession.AxisVector(axis) *
            (sign * Dimension(piece, axis) * 0.5f);

        [TestCase(PieceType.ShedRoof)]
        [TestCase(PieceType.GableRoof)]
        [TestCase(PieceType.HipRoof)]
        public void RoofSnapFeaturesUseRealEavesAndRidge(PieceType type)
        {
            PieceData piece = Roof(type);
            var features = new List<SnapFeature>();
            SnapGeometry.Collect(piece, features);
            RoofProfile profile = RoofProfile.Create(piece.Dimensions);
            foreach (Vector3 local in profile.Eaves)
                Assert.That(features.Exists(feature => feature.Kind == GeometricSnapKind.Endpoint &&
                    Vector3.Distance(feature.A, local) < 0.00001f), Is.True);
            foreach (Vector3 local in profile.Ridge)
                Assert.That(features.Exists(feature => feature.Kind == GeometricSnapKind.Endpoint &&
                    Vector3.Distance(feature.A, local) < 0.00001f), Is.True);
            Assert.That(features.Exists(feature => feature.Kind == GeometricSnapKind.Surface && feature.Triangle), Is.True);
            features.Clear();
            SnapGeometry.Collect(piece, features, true, ManipulationAxis.X, 1);
            Assert.That(features.TrueForAll(feature => feature.Kind == GeometricSnapKind.Surface ||
                Mathf.Abs(feature.A.x - piece.Dimensions.X * 0.5f) < 0.0001f), Is.True);
        }

        [TestCase(PieceType.ShedRoof, ManipulationAxis.X, 0f)]
        [TestCase(PieceType.GableRoof, ManipulationAxis.Z, 45f)]
        [TestCase(PieceType.HipRoof, ManipulationAxis.X, 90f)]
        public void FaceSnapMovesRoofEdgeAndKeepsOppositeEdgeFixed(
            PieceType type, ManipulationAxis axis, float yaw)
        {
            PieceData moving = Roof(type, 1f, 1f, yaw: yaw);
            Vector3 outward = moving.Transform.Rotation * ManipulationSession.AxisVector(axis);
            PieceData target = Roof(type, 1f, 1f, yaw: yaw, id: B, position: outward * 1.1f);
            var settings = new SnapSettings { EndpointSnapEnabled = true };
            var session = new ManipulationSession(moving, ManipulationMode.Resize, axis,
                Vector2.zero, Vector2.right, 100f, settings, ResizeMode.Face, 1);
            PieceData changed = new SnapResolver().Resolve(moving, session, settings, new[] { target });
            Assert.That(Dimension(changed, axis), Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(Vector3.Distance(OppositeFootprintEdge(changed, axis, 1),
                OppositeFootprintEdge(moving, axis, 1)), Is.LessThan(0.0001f));
            Assert.That(changed.Dimensions.Rise, Is.EqualTo(moving.Dimensions.Rise));
        }

        [TestCase(PieceType.ShedRoof, PieceType.Wall)]
        [TestCase(PieceType.GableRoof, PieceType.Slab)]
        [TestCase(PieceType.HipRoof, PieceType.Column)]
        [TestCase(PieceType.GableRoof, PieceType.HipRoof)]
        public void RoofSnapUsesExistingResolverAcrossArchitecturalTypes(PieceType roofType, PieceType targetType)
        {
            PieceData moving = Roof(roofType, 1f, 1f, id: A);
            var pose = new PieceTransform(new Vector3(1.1f, 0f, 0f), Quaternion.identity);
            PieceData target;
            switch (targetType)
            {
                // Shed/Gable end fascia is centered at y=0.6 m. A 0.2 m target
                // ends at y=0.2 m, outside the 0.25 m Surface capture radius.
                case PieceType.Wall: target = new PieceData(B, pose, new WallDimensions(1f, 1f, 1f)); break;
                case PieceType.Slab: target = new PieceData(B, pose, new SlabDimensions(1f, 1f, 1f)); break;
                case PieceType.Column: target = new PieceData(B, pose, new ColumnDimensions(1f, 0.2f, 1f)); break;
                // Raise the Hip's underside so its west fascia spans y=0.4..0.8 m.
                // Its lower eave stays within 0.25 m of the Gable's upper eave.
                case PieceType.HipRoof: target = new PieceData(B,
                    new PieceTransform(new Vector3(1.1f, 0.4f, 0f), Quaternion.identity),
                    new HipRoofDimensions(1f, 1f, 0.4f, 1f)); break;
                default: throw new ArgumentOutOfRangeException(nameof(targetType));
            }
            foreach (GeometricSnapKind kind in new[] { GeometricSnapKind.Endpoint, GeometricSnapKind.Edge, GeometricSnapKind.Surface })
            {
                var settings = new SnapSettings { CaptureDistance = 0.25f, ReleaseDistance = 0.4f,
                    EndpointSnapEnabled = kind == GeometricSnapKind.Endpoint,
                    EdgeSnapEnabled = kind == GeometricSnapKind.Edge,
                    SurfaceSnapEnabled = kind == GeometricSnapKind.Surface };
                var session = new ManipulationSession(moving, ManipulationMode.Move, ManipulationAxis.X,
                    Vector2.zero, Vector2.right, 100f, settings);
                var resolver = new SnapResolver();
                if (kind == GeometricSnapKind.Surface)
                {
                    var sourceFeatures = new List<SnapFeature>();
                    var targetFeatures = new List<SnapFeature>();
                    SnapGeometry.Collect(moving, sourceFeatures);
                    SnapGeometry.Collect(target, targetFeatures);
                    bool facingFasciaWithinCapture = false;
                    foreach (SnapFeature source in sourceFeatures)
                    foreach (SnapFeature surface in targetFeatures)
                    {
                        if (source.Kind != GeometricSnapKind.Surface || source.Triangle ||
                            surface.Kind != GeometricSnapKind.Surface || surface.Triangle ||
                            Vector3.Dot(source.Normal, Vector3.right) < 0.999f ||
                            Vector3.Dot(surface.Normal, Vector3.left) < 0.999f) continue;
                        Vector3 offset = source.A - surface.A;
                        Vector3 nearest = surface.A + surface.U * Mathf.Clamp(
                            Vector3.Dot(offset, surface.U) / surface.U.sqrMagnitude, -1f, 1f)
                            + surface.V * Mathf.Clamp(
                                Vector3.Dot(offset, surface.V) / surface.V.sqrMagnitude, -1f, 1f);
                        if (Vector3.Distance(source.A, nearest) <= settings.CaptureDistance)
                            facingFasciaWithinCapture = true;
                    }
                    Assert.That(facingFasciaWithinCapture, Is.True,
                        $"Fixture must expose opposing surfaces within capture: roof={roofType}, target={targetType}");
                }
                Assert.That(resolver.Resolve(moving, session, settings, new[] { moving }).Transform.Position,
                    Is.EqualTo(moving.Transform.Position));
                Assert.That(resolver.HasTarget, Is.False);
                PieceData snapped = resolver.Resolve(moving, session, settings, new[] { moving, target });
                Assert.That(resolver.HasTarget, Is.True, $"roof={roofType}, target={targetType}, kind={kind}");
                Assert.That(snapped.Transform.Position.x, Is.EqualTo(0.1f).Within(0.0001f),
                    $"roof={roofType}, target={targetType}, kind={kind}");
                Assert.That(snapped.Dimensions, Is.EqualTo(moving.Dimensions));
            }
        }
    }
}
