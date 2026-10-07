using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class CirculationFoundationTests
    {
        private static readonly PieceId Id = PieceId.Parse("40000000000000000000000000000011");
        private static PieceData Stair(int steps = 10, float yaw = 0f) => new PieceData(Id,
            new PieceTransform(new Vector3(3f, 1f, -4f), Quaternion.Euler(0f, yaw, 0f)),
            new StairDimensions(2f, 2f, 3f, steps));
        private static PieceData Ramp(float yaw = 0f) => new PieceData(Id,
            new PieceTransform(new Vector3(3f, 1f, -4f), Quaternion.Euler(0f, yaw, 0f)),
            new RampDimensions(2f, 2f, 4f, 0.2f));

        [Test]
        public void SemanticParametersAreAuthoritativeAndDerivedValuesAreCorrect()
        {
            PieceData stair = Stair().WithMaterial(LabMaterialIds.Stone);
            Assert.That(stair.Type, Is.EqualTo(PieceType.Stair));
            Assert.That(stair.Dimensions.AsStair().RiserHeight, Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(stair.Dimensions.AsStair().TreadDepth, Is.EqualTo(0.3f).Within(0.00001f));
            PieceData fewer = stair.WithStepCount(9);
            Assert.That(fewer.Dimensions.StepCount, Is.EqualTo(9));
            Assert.That(fewer.MaterialId, Is.EqualTo(stair.MaterialId));
            Assert.That(fewer.Transform.Position, Is.EqualTo(stair.Transform.Position));
            Assert.That(stair.Dimensions.StepCount, Is.EqualTo(10));
            PieceData ramp = Ramp().WithMaterial(LabMaterialIds.Brick);
            Assert.That(ramp.Type, Is.EqualTo(PieceType.Ramp));
            Assert.That(ramp.Dimensions.AsRamp().PitchDegrees, Is.EqualTo(26.56505f).Within(0.0001f));
            Assert.That(ramp.Dimensions.Y, Is.EqualTo(2.2f).Within(0.00001f));
            Assert.That(ramp.MaterialId, Is.EqualTo(LabMaterialIds.Brick));
        }

        [Test]
        public void InvalidDimensionsAndStepCountsAreRejected()
        {
            foreach (float invalid in new[] { 0f, 0.09f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new StairDimensions(invalid, 2f, 3f, 10));
                Assert.Throws<ArgumentOutOfRangeException>(() => new StairDimensions(2f, invalid, 3f, 10));
                Assert.Throws<ArgumentOutOfRangeException>(() => new StairDimensions(2f, 2f, invalid, 10));
                Assert.Throws<ArgumentOutOfRangeException>(() => new RampDimensions(invalid, 2f, 4f, 0.2f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new RampDimensions(2f, invalid, 4f, 0.2f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new RampDimensions(2f, 2f, invalid, 0.2f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new RampDimensions(2f, 2f, 4f, invalid));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new StairDimensions(2f, 2f, 3f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StairDimensions(2f, 2f, 3f, 129));
            Assert.Throws<ArgumentOutOfRangeException>(() => Stair().WithStepCount(0));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(10)]
        [TestCase(128)]
        public void StairMeshHasUniformStepsValidBoundsAndNondegenerateTriangles(int steps)
        {
            PieceData piece = Stair(steps);
            CirculationProfile profile = CirculationProfile.Create(piece.Dimensions);
            Assert.That(profile.Sections.Length, Is.EqualTo(steps));
            Assert.That(profile.Sections[0].StartZ, Is.EqualTo(-1.5f));
            Assert.That(profile.Sections[steps - 1].EndZ, Is.EqualTo(1.5f));
            Assert.That(profile.Sections[0].TopStart, Is.EqualTo(2f / steps).Within(0.00001f));
            Assert.That(profile.Sections[steps - 1].TopEnd, Is.EqualTo(2f));
            Assert.That(profile.Sections[0].EndZ - profile.Sections[0].StartZ, Is.EqualTo(3f / steps).Within(0.00001f));
            CheckGeometry(CirculationGeometryGenerator.Generate(piece.Dimensions), new Vector3(2f, 2f, 3f));
        }

        [Test]
        public void RampMeshHasSlopedSolidProfile()
        {
            PieceData piece = Ramp();
            CirculationSection section = CirculationProfile.Create(piece.Dimensions).Sections[0];
            Assert.That(section.BottomStart, Is.EqualTo(0f));
            Assert.That(section.BottomEnd, Is.EqualTo(2f));
            Assert.That(section.TopStart, Is.EqualTo(0.2f));
            Assert.That(section.TopEnd, Is.EqualTo(2.2f));
            CheckGeometry(CirculationGeometryGenerator.Generate(piece.Dimensions), new Vector3(2f, 2.2f, 4f));
        }

        private static void CheckGeometry(BlockGeometry geometry, Vector3 expectedSize)
        {
            var edgeUses = new Dictionary<string, int>();
            Assert.That(geometry.Bounds.min.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(geometry.Bounds.size.x, Is.EqualTo(expectedSize.x).Within(0.00001f));
            Assert.That(geometry.Bounds.size.y, Is.EqualTo(expectedSize.y).Within(0.00001f));
            Assert.That(geometry.Bounds.size.z, Is.EqualTo(expectedSize.z).Within(0.00001f));
            for (int i = 0; i < geometry.Triangles.Length; i += 3)
            {
                Vector3 a = geometry.Vertices[geometry.Triangles[i]];
                Vector3 b = geometry.Vertices[geometry.Triangles[i + 1]];
                Vector3 c = geometry.Vertices[geometry.Triangles[i + 2]];
                Assert.That(float.IsNaN(a.x) || float.IsInfinity(a.x), Is.False);
                Assert.That(float.IsNaN(a.y) || float.IsInfinity(a.y), Is.False);
                Assert.That(float.IsNaN(a.z) || float.IsInfinity(a.z), Is.False);
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-16f));
                CountEdge(edgeUses, a, b);
                CountEdge(edgeUses, b, c);
                CountEdge(edgeUses, c, a);
            }
            foreach (KeyValuePair<string, int> edge in edgeUses)
                Assert.That(edge.Value, Is.EqualTo(2), $"Open/nonmanifold mesh edge {edge.Key}");
        }

        private static void CountEdge(Dictionary<string, int> counts, Vector3 a, Vector3 b)
        {
            string first = $"{Mathf.RoundToInt(a.x * 100000f)},{Mathf.RoundToInt(a.y * 100000f)},{Mathf.RoundToInt(a.z * 100000f)}";
            string second = $"{Mathf.RoundToInt(b.x * 100000f)},{Mathf.RoundToInt(b.y * 100000f)},{Mathf.RoundToInt(b.z * 100000f)}";
            string key = string.CompareOrdinal(first, second) < 0 ? first + "|" + second : second + "|" + first;
            counts.TryGetValue(key, out int uses);
            counts[key] = uses + 1;
        }

        [TestCase(false, 0f)]
        [TestCase(false, 45f)]
        [TestCase(false, 90f)]
        [TestCase(true, 0f)]
        [TestCase(true, 45f)]
        [TestCase(true, 90f)]
        public void MoveRotateAndFaceResizePreserveSemanticStateAndOppositeFace(bool ramp, float yaw)
        {
            PieceData piece = ramp ? Ramp(yaw) : Stair(10, yaw);
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            foreach (int sign in new[] { -1, 1 })
            {
                float original = axis == ManipulationAxis.X ? piece.Dimensions.X : axis == ManipulationAxis.Y ? piece.Dimensions.Y : piece.Dimensions.Z;
                foreach (float desired in new[] { original + 0.5f, ramp && axis == ManipulationAxis.Y ? 0.3f : 0.1f })
                {
                    var session = new ManipulationSession(piece, ManipulationMode.Resize, axis,
                        Vector2.zero, Vector2.right, 100f, resizeMode: ResizeMode.Face, faceSign: sign);
                    PieceData result = session.ResizeToDimension(desired);
                    float actual = axis == ManipulationAxis.X ? result.Dimensions.X : axis == ManipulationAxis.Y ? result.Dimensions.Y : result.Dimensions.Z;
                    Vector3 direction = piece.Transform.Rotation * ManipulationSession.AxisVector(axis);
                    Vector3 oldOpposite = piece.Transform.Position + direction * (axis == ManipulationAxis.Y && sign > 0 ? 0f : -sign * original * 0.5f);
                    Vector3 newOpposite = result.Transform.Position + direction * (axis == ManipulationAxis.Y && sign > 0 ? 0f : -sign * actual * 0.5f);
                    if (axis == ManipulationAxis.Y && sign < 0)
                    {
                        oldOpposite = piece.Transform.Position + direction * original;
                        newOpposite = result.Transform.Position + direction * actual;
                    }
                    Assert.That(Vector3.Distance(oldOpposite, newOpposite), Is.LessThan(0.0001f),
                        $"{piece.Type} {axis} {sign} yaw={yaw} desired={desired}");
                    Assert.That(result.MaterialId, Is.EqualTo(piece.MaterialId));
                    Assert.That(result.Transform.Rotation, Is.EqualTo(piece.Transform.Rotation));
                    if (!ramp) Assert.That(result.Dimensions.StepCount, Is.EqualTo(10));
                    else Assert.That(result.Dimensions.RampThickness, Is.EqualTo(0.2f));
                }
            }
            var move = new ManipulationSession(piece, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f).Evaluate(new Vector2(100f, 0f));
            Assert.That(move.Transform.Position.x, Is.EqualTo(piece.Transform.Position.x + 1f));
            var rotate = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 1f).Evaluate(new Vector2(90f, 0f));
            Assert.That(rotate.Transform.Position, Is.EqualTo(piece.Transform.Position));
            Assert.That(rotate.Dimensions, Is.EqualTo(piece.Dimensions));
        }

        [Test]
        public void SnapFeatureCountIsBoundedAcrossStepCountsAndFollowsYaw()
        {
            var shortFeatures = new List<SnapFeature>();
            var longFeatures = new List<SnapFeature>();
            SnapGeometry.Collect(Stair(2), shortFeatures);
            SnapGeometry.Collect(Stair(128), longFeatures);
            Assert.That(longFeatures.Count, Is.EqualTo(shortFeatures.Count));
            foreach (GeometricSnapKind kind in new[] { GeometricSnapKind.Endpoint, GeometricSnapKind.Edge, GeometricSnapKind.Surface })
                Assert.That(longFeatures.Exists(f => f.Kind == kind), Is.True, kind.ToString());
            var rampFeatures = new List<SnapFeature>();
            SnapGeometry.Collect(Ramp(45f), rampFeatures);
            Assert.That(rampFeatures.Exists(f => f.Kind == GeometricSnapKind.Surface && f.Normal.y > 0f && f.Normal.z < 0f), Is.True);
        }

        [TestCase(false, PieceType.Slab)]
        [TestCase(false, PieceType.Wall)]
        [TestCase(true, PieceType.Slab)]
        [TestCase(true, PieceType.Wall)]
        [TestCase(false, PieceType.Ramp)]
        public void CirculationEndpointsSnapToNearbyArchitecturalPieces(bool movingRamp, PieceType targetType)
        {
            PieceData moving = movingRamp ? Ramp() : Stair();
            PieceId targetId = PieceId.Parse("40000000000000000000000000000012");
            float targetHeight = movingRamp ? 2.2f : 2f;
            var pose = new PieceTransform(moving.Transform.Position + Vector3.right * 0.1f, Quaternion.identity);
            PieceData target = targetType == PieceType.Slab
                ? new PieceData(targetId, pose, new SlabDimensions(2f, targetHeight, moving.Dimensions.Z))
                : targetType == PieceType.Wall
                    ? new PieceData(targetId, pose, new WallDimensions(2f, targetHeight, moving.Dimensions.Z))
                    : new PieceData(targetId, pose, new RampDimensions(2f, 1.8f, moving.Dimensions.Z, 0.2f));
            var sourceFeatures = new List<SnapFeature>();
            var targetFeatures = new List<SnapFeature>();
            SnapGeometry.Collect(moving, sourceFeatures);
            SnapGeometry.Collect(target, targetFeatures);
            bool withinCapture = false;
            foreach (SnapFeature source in sourceFeatures)
            foreach (SnapFeature candidate in targetFeatures)
                if (source.Kind == GeometricSnapKind.Endpoint && candidate.Kind == GeometricSnapKind.Endpoint &&
                    Vector3.Distance(source.A, candidate.A) <= 0.25f)
                    withinCapture = true;
            Assert.That(withinCapture, Is.True, $"Invalid fixture: {moving.Type} to {targetType}");
            var settings = new SnapSettings { EndpointSnapEnabled = true };
            var session = new ManipulationSession(moving, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings);
            var resolver = new SnapResolver();
            Assert.That(resolver.Resolve(moving, session, settings, new[] { moving }).Transform.Position,
                Is.EqualTo(moving.Transform.Position));
            Assert.That(resolver.HasTarget, Is.False);
            PieceData snapped = resolver.Resolve(moving, session, settings, new[] { moving, target });
            Assert.That(resolver.HasTarget, Is.True, $"{moving.Type} to {targetType}");
            Assert.That(resolver.TargetId, Is.EqualTo(targetId));
            Assert.That(Vector3.Distance(snapped.Transform.Position, moving.Transform.Position), Is.LessThanOrEqualTo(0.25f));
            Assert.That(snapped.Dimensions, Is.EqualTo(moving.Dimensions));
        }

        [TestCase(false, GeometricSnapKind.Endpoint)]
        [TestCase(false, GeometricSnapKind.Edge)]
        [TestCase(false, GeometricSnapKind.Surface)]
        [TestCase(true, GeometricSnapKind.Endpoint)]
        [TestCase(true, GeometricSnapKind.Edge)]
        [TestCase(true, GeometricSnapKind.Surface)]
        public void CirculationSupportsEachGeometricSnapKind(bool ramp, GeometricSnapKind kind)
        {
            PieceData moving = ramp ? Ramp() : Stair();
            PieceId targetId = PieceId.Parse("40000000000000000000000000000012");
            PieceData target = new PieceData(targetId,
                new PieceTransform(moving.Transform.Position + Vector3.right * 2.1f, Quaternion.identity),
                moving.Dimensions);
            var settings = new SnapSettings { EndpointSnapEnabled = kind == GeometricSnapKind.Endpoint,
                EdgeSnapEnabled = kind == GeometricSnapKind.Edge,
                SurfaceSnapEnabled = kind == GeometricSnapKind.Surface };
            var session = new ManipulationSession(moving, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings);
            var resolver = new SnapResolver();
            resolver.Resolve(moving, session, settings, new[] { target });
            Assert.That(resolver.HasTarget, Is.True, $"{moving.Type} {kind}");
            Assert.That(resolver.ActiveKind, Is.EqualTo(kind));
        }
    }
}
