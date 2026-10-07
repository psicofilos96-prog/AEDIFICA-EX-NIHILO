using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class CurvedArchitectureTests
    {
        private static readonly PieceId A = PieceId.Parse("50000000000000000000000000000011");
        private static readonly PieceId B = PieceId.Parse("50000000000000000000000000000012");
        private const float RotationToleranceDegrees = 0.1f; // float dot/acos near unity, as in P0.10
        private static PieceData Piece(PieceType type, float yaw = 0f)
        {
            var pose = new PieceTransform(new Vector3(3f, 0f, 4f), Quaternion.Euler(0f, yaw, 0f));
            switch (type)
            {
                case PieceType.Arch: return new PieceData(A, pose, new ArchDimensions(4f, 3f, 0.7f, 0.6f, 1.2f, 0.25f));
                case PieceType.Vault: return new PieceData(A, pose, new VaultDimensions(4f, 2.8f, 5f, 0.25f));
                case PieceType.Dome: return new PieceData(A, pose, new DomeDimensions(4f, 2.5f, 0.25f));
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        [Test]
        public void SemanticParametersAndDerivedValuesAreConsistent()
        {
            ArchDimensions arch = Piece(PieceType.Arch).ArchDimensions;
            Assert.That(arch.OpeningWidth, Is.EqualTo(2.8f).Within(0.00001f));
            Assert.That(arch.SpringHeight, Is.EqualTo(1.55f).Within(0.00001f));
            Assert.That(arch.Height - arch.CrownThickness, Is.EqualTo(2.75f));
            VaultDimensions vault = Piece(PieceType.Vault).VaultDimensions;
            Assert.That(vault.InnerWidth, Is.EqualTo(3.5f));
            Assert.That(vault.InnerRise, Is.EqualTo(2.55f));
            DomeDimensions dome = Piece(PieceType.Dome).DomeDimensions;
            Assert.That(dome.InnerDiameter, Is.EqualTo(3.5f));
            Assert.That(dome.InnerRise, Is.EqualTo(2.25f));
            foreach (PieceType type in new[] { PieceType.Arch, PieceType.Vault, PieceType.Dome })
            {
                PieceData piece = Piece(type).WithMaterial(LabMaterialIds.Stone);
                Assert.That(piece.Type, Is.EqualTo(type));
                Assert.That(piece.MaterialId, Is.EqualTo(LabMaterialIds.Stone));
                Assert.That(piece.WithDimensions(piece.Dimensions.Resize(1, piece.Dimensions.Y + 0.2f)).MaterialId,
                    Is.EqualTo(LabMaterialIds.Stone));
            }
        }

        [Test]
        public void InvalidCurvedParametersAreRejected()
        {
            foreach (float invalid in new[] { 0f, 0.09f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new ArchDimensions(invalid, 3f, 0.7f, 0.6f, 1.2f, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ArchDimensions(4f, invalid, 0.7f, 0.6f, 1.2f, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ArchDimensions(4f, 3f, invalid, 0.6f, 1.2f, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ArchDimensions(4f, 3f, 0.7f, invalid, 1.2f, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ArchDimensions(4f, 3f, 0.7f, 0.6f, invalid, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ArchDimensions(4f, 3f, 0.7f, 0.6f, 1.2f, invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => new VaultDimensions(invalid, 2.8f, 5f, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new VaultDimensions(4f, invalid, 5f, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new VaultDimensions(4f, 2.8f, invalid, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new VaultDimensions(4f, 2.8f, 5f, invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => new DomeDimensions(invalid, 2.5f, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new DomeDimensions(4f, invalid, 0.25f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new DomeDimensions(4f, 2.5f, invalid));
            }
            Assert.Throws<ArgumentException>(() => new ArchDimensions(1.2f, 3f, 1f, 0.6f, 1f, 0.2f));
            Assert.Throws<ArgumentException>(() => new ArchDimensions(4f, 1.2f, 1f, 0.6f, 1f, 0.2f));
            Assert.Throws<ArgumentException>(() => new VaultDimensions(0.5f, 2f, 3f, 0.25f));
            Assert.Throws<ArgumentException>(() => new DomeDimensions(4f, 0.25f, 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArchDimensions(10001f, 3f, 1f, 0.6f, 1f, 0.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VaultDimensions(4f, 2f, 10001f, 0.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DomeDimensions(4f, 10001f, 0.2f));
        }

        [Test]
        public void LargeValidCurvedPiecesKeepFiniteGeometry()
        {
            foreach (PieceDimensions dimensions in new[] {
                new PieceDimensions(new ArchDimensions(1000f, 800f, 500f, 100f, 300f, 100f)),
                new PieceDimensions(new VaultDimensions(1000f, 800f, 500f, 100f)),
                new PieceDimensions(new DomeDimensions(1000f, 800f, 100f)) })
            {
                BlockGeometry mesh = CurvedGeometryGenerator.Generate(dimensions);
                Assert.That(mesh.Vertices.Length, Is.GreaterThan(0));
                foreach (Vector3 vertex in mesh.Vertices)
                    Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) ||
                        float.IsNaN(vertex.y) || float.IsInfinity(vertex.y) ||
                        float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False);
            }
        }

        [TestCase(PieceType.Arch, 4f, 3f, 0.7f)]
        [TestCase(PieceType.Vault, 4f, 2.8f, 5f)]
        [TestCase(PieceType.Dome, 4f, 2.5f, 4f)]
        public void CurvedMeshIsFiniteClosedAndBounded(PieceType type, float x, float y, float z)
        {
            BlockGeometry geometry = CurvedGeometryGenerator.Generate(Piece(type).Dimensions);
            Assert.That(geometry.Bounds.min.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(geometry.Bounds.size.x, Is.EqualTo(x).Within(0.00001f));
            Assert.That(geometry.Bounds.size.y, Is.EqualTo(y).Within(0.00001f));
            Assert.That(geometry.Bounds.size.z, Is.EqualTo(z).Within(0.00001f));
            Assert.That(geometry.Triangles.Length / 3, Is.LessThan(1600));
            var edges = new Dictionary<string, int>();
            bool foundDownwardBase = false;
            for (int i = 0; i < geometry.Triangles.Length; i += 3)
            {
                Vector3 a = geometry.Vertices[geometry.Triangles[i]];
                Vector3 b = geometry.Vertices[geometry.Triangles[i + 1]];
                Vector3 c = geometry.Vertices[geometry.Triangles[i + 2]];
                foreach (Vector3 vertex in new[] { a, b, c })
                    Assert.That(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                        float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z), Is.False);
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-18f));
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a).normalized,
                    geometry.Normals[geometry.Triangles[i]]), Is.GreaterThan(0.999f));
                if (Mathf.Abs(a.y) < 0.00001f && Mathf.Abs(b.y) < 0.00001f &&
                    Mathf.Abs(c.y) < 0.00001f &&
                    Vector3.Dot(geometry.Normals[geometry.Triangles[i]], Vector3.down) > 0.99f)
                    foundDownwardBase = true;
                Count(edges, a, b); Count(edges, b, c); Count(edges, c, a);
            }
            Assert.That(foundDownwardBase, Is.True, $"{type} requires outward base normals");
            foreach (KeyValuePair<string, int> edge in edges)
                Assert.That(edge.Value, Is.EqualTo(2), $"{type} open/nonmanifold edge {edge.Key}");
        }

        private static void Count(Dictionary<string, int> edges, Vector3 a, Vector3 b)
        {
            string first = $"{Mathf.RoundToInt(a.x * 10000f)},{Mathf.RoundToInt(a.y * 10000f)},{Mathf.RoundToInt(a.z * 10000f)}";
            string second = $"{Mathf.RoundToInt(b.x * 10000f)},{Mathf.RoundToInt(b.y * 10000f)},{Mathf.RoundToInt(b.z * 10000f)}";
            string key = string.CompareOrdinal(first, second) < 0 ? first + "|" + second : second + "|" + first;
            edges.TryGetValue(key, out int uses);
            edges[key] = uses + 1;
        }

        [Test]
        public void ProfilesKeepArchitecturalOpeningsAndPositiveThickness()
        {
            ArchDimensions arch = Piece(PieceType.Arch).ArchDimensions;
            Vector2[] outline = CurvedProfile.ArchOutline(arch);
            Assert.That(outline[5 + CurvedProfile.ArchSegments / 2].y,
                Is.EqualTo(arch.Height - arch.CrownThickness).Within(0.00001f));
            Assert.That(outline[5].x - outline[5 + CurvedProfile.ArchSegments].x,
                Is.EqualTo(arch.OpeningWidth).Within(0.00001f));
            VaultDimensions vault = Piece(PieceType.Vault).VaultDimensions;
            Assert.That(CurvedProfile.VaultOuter(vault, 0).y, Is.EqualTo(0f));
            Assert.That(CurvedProfile.VaultInner(vault, 0).x, Is.LessThan(CurvedProfile.VaultOuter(vault, 0).x));
            DomeDimensions dome = Piece(PieceType.Dome).DomeDimensions;
            Assert.That(CurvedProfile.DomeOuter(dome, 0, 0).y - CurvedProfile.DomeInner(dome, 0, 0).y,
                Is.EqualTo(dome.Thickness).Within(0.00001f));
            Assert.That(CurvedProfile.DomeOuter(dome, CurvedProfile.DomeLatitudeSegments, 0).y, Is.EqualTo(0f));
        }

        [TestCase(PieceType.Arch, ManipulationAxis.X)]
        [TestCase(PieceType.Arch, ManipulationAxis.Y)]
        [TestCase(PieceType.Arch, ManipulationAxis.Z)]
        [TestCase(PieceType.Vault, ManipulationAxis.X)]
        [TestCase(PieceType.Vault, ManipulationAxis.Y)]
        [TestCase(PieceType.Vault, ManipulationAxis.Z)]
        [TestCase(PieceType.Dome, ManipulationAxis.X)]
        [TestCase(PieceType.Dome, ManipulationAxis.Y)]
        [TestCase(PieceType.Dome, ManipulationAxis.Z)]
        public void FaceResizeClampsAtEachSemanticMinimum(PieceType type, ManipulationAxis axis)
        {
            PieceData piece = Piece(type);
            var session = new ManipulationSession(piece, ManipulationMode.Resize, axis,
                Vector2.zero, Vector2.right, 100f, resizeMode: ResizeMode.Face);
            PieceData resized = session.ResizeToDimension(0f);
            float dimension = axis == ManipulationAxis.X ? resized.Dimensions.X :
                axis == ManipulationAxis.Y ? resized.Dimensions.Y : resized.Dimensions.Z;
            Assert.That(dimension, Is.EqualTo(piece.Dimensions.MinimumForAxis((int)axis)).Within(0.00001f),
                $"{type} {axis}: Thickness={Thickness(piece)}");
            Assert.That(resized.Dimensions.IsValid, Is.True, $"{type} {axis}");
            Assert.That(Thickness(resized), Is.EqualTo(Thickness(piece)), $"{type} {axis}");
            AssertFiniteNondegenerate(CurvedGeometryGenerator.Generate(resized.Dimensions));
        }

        private static float Thickness(PieceData piece) => piece.Dimensions.IsArch
            ? piece.ArchDimensions.CrownThickness : piece.Dimensions.IsVault
                ? piece.VaultDimensions.Thickness : piece.DomeDimensions.Thickness;

        private static void AssertFiniteNondegenerate(BlockGeometry mesh)
        {
            for (int i = 0; i < mesh.Triangles.Length; i += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Triangles[i]];
                Vector3 b = mesh.Vertices[mesh.Triangles[i + 1]];
                Vector3 c = mesh.Vertices[mesh.Triangles[i + 2]];
                foreach (Vector3 vertex in new[] { a, b, c })
                    Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) ||
                        float.IsNaN(vertex.y) || float.IsInfinity(vertex.y) ||
                        float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False);
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-18f));
            }
        }

        [TestCase(PieceType.Vault, 0.1f)]
        [TestCase(PieceType.Vault, 0.25f)]
        [TestCase(PieceType.Vault, 1f)]
        [TestCase(PieceType.Dome, 0.1f)]
        [TestCase(PieceType.Dome, 0.25f)]
        [TestCase(PieceType.Dome, 1f)]
        public void ThicknessDependentBoundsAcceptSafeProfilesAndRejectInsufficientClearance(PieceType type, float thickness)
        {
            float minimumHorizontal = type == PieceType.Vault
                ? VaultDimensions.MinimumWidth(thickness) : DomeDimensions.MinimumDiameter(thickness);
            float minimumVertical = type == PieceType.Vault
                ? VaultDimensions.MinimumHeight(thickness) : DomeDimensions.MinimumRise(thickness);
            if (type == PieceType.Vault)
            {
                var valid = new VaultDimensions(minimumHorizontal, minimumVertical, 1f, thickness);
                Assert.That(valid.InnerWidth, Is.GreaterThanOrEqualTo(0.1f));
                Assert.That(valid.InnerRise, Is.GreaterThanOrEqualTo(0.1f));
                Assert.Throws<ArgumentException>(() => new VaultDimensions(minimumHorizontal - 0.001f, thickness + 1f, 1f, thickness));
                Assert.Throws<ArgumentException>(() => new VaultDimensions(2f * thickness + 1f, minimumVertical - 0.001f, 1f, thickness));
                if (thickness == 0.25f)
                    Assert.Throws<ArgumentException>(() => new VaultDimensions(4f, thickness + 0.1f, 1f, thickness),
                        "The old nominal minimum rounded to an inner rise below 0.1 m.");
                for (int step = 0; step <= CurvedProfile.VaultSegments; step++)
                    Assert.That(Vector2.Distance(CurvedProfile.VaultOuter(valid, step),
                        CurvedProfile.VaultInner(valid, step)), Is.GreaterThan(0f), $"Vault section {step}");
                AssertFiniteNondegenerate(CurvedGeometryGenerator.Generate(new PieceDimensions(valid)));
            }
            else
            {
                var valid = new DomeDimensions(minimumHorizontal, minimumVertical, thickness);
                Assert.That(valid.InnerDiameter, Is.GreaterThanOrEqualTo(0.1f));
                Assert.That(valid.InnerRise, Is.GreaterThanOrEqualTo(0.1f));
                Assert.Throws<ArgumentException>(() => new DomeDimensions(minimumHorizontal - 0.001f, thickness + 1f, thickness));
                Assert.Throws<ArgumentException>(() => new DomeDimensions(2f * thickness + 1f, minimumVertical - 0.001f, thickness));
                if (thickness == 0.25f)
                    Assert.Throws<ArgumentException>(() => new DomeDimensions(4f, thickness + 0.1f, thickness),
                        "The old nominal minimum rounded to an inner rise below 0.1 m.");
                for (int latitude = 0; latitude <= CurvedProfile.DomeLatitudeSegments; latitude++)
                    Assert.That(Vector3.Distance(CurvedProfile.DomeOuter(valid, latitude, 0),
                        CurvedProfile.DomeInner(valid, latitude, 0)), Is.GreaterThan(0f),
                        $"Dome latitude {latitude}");
                AssertFiniteNondegenerate(CurvedGeometryGenerator.Generate(new PieceDimensions(valid)));
            }
        }

        [Test]
        public void ArchOpeningAndSpringUseTheirOwnSemanticBounds()
        {
            const float pier = 0.6f, rise = 1.2f, crown = 0.25f;
            float width = ArchDimensions.MinimumWidth(pier);
            float height = ArchDimensions.MinimumHeight(rise, crown);
            var valid = new ArchDimensions(width, height, 0.7f, pier, rise, crown);
            Assert.That(valid.OpeningWidth, Is.GreaterThanOrEqualTo(0.1f));
            Assert.That(valid.SpringHeight, Is.GreaterThanOrEqualTo(0.1f));
            Assert.Throws<ArgumentException>(() => new ArchDimensions(width - 0.001f, 3f, 0.7f, pier, rise, crown));
            Assert.Throws<ArgumentException>(() => new ArchDimensions(4f, height - 0.001f, 0.7f, pier, rise, crown));
            AssertFiniteNondegenerate(CurvedGeometryGenerator.Generate(new PieceDimensions(valid)));
            PieceData original = Piece(PieceType.Arch);
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            {
                var center = new ManipulationSession(original, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f);
                PieceData clamped = center.Evaluate(Vector2.left * 10000f);
                Assert.That(clamped.Transform.Position, Is.EqualTo(original.Transform.Position));
                Assert.That(clamped.Dimensions.IsValid, Is.True, $"Arch bilateral {axis}");
                Assert.That(clamped.ArchDimensions.PierWidth, Is.EqualTo(pier));
                Assert.That(clamped.ArchDimensions.CrownThickness, Is.EqualTo(crown));
            }
        }

        [TestCase(PieceType.Vault, 45f)]
        [TestCase(PieceType.Vault, 90f)]
        [TestCase(PieceType.Dome, 45f)]
        [TestCase(PieceType.Dome, 90f)]
        public void DragBeyondMinimumKeepsValidProfileAndOppositeFace(PieceType type, float yaw)
        {
            PieceData piece = Piece(type, yaw);
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            {
                float initial = axis == ManipulationAxis.X ? piece.Dimensions.X :
                    axis == ManipulationAxis.Y ? piece.Dimensions.Y : piece.Dimensions.Z;
                float minimum = piece.Dimensions.MinimumForAxis((int)axis);
                var bilateral = new ManipulationSession(piece, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f);
                PieceData centerResult = bilateral.Evaluate(Vector2.left * 10000f);
                Assert.That(centerResult.Transform.Position, Is.EqualTo(piece.Transform.Position));
                Assert.That(centerResult.Dimensions.IsValid, Is.True);
                Assert.That(Thickness(centerResult), Is.EqualTo(Thickness(piece)));
                foreach (int sign in new[] { -1, 1 })
                {
                    var face = new ManipulationSession(piece, ManipulationMode.Resize, axis,
                        Vector2.zero, Vector2.right, 100f, resizeMode: ResizeMode.Face, faceSign: sign);
                    PieceData resized = face.Evaluate(Vector2.left * 10000f);
                    float actual = axis == ManipulationAxis.X ? resized.Dimensions.X :
                        axis == ManipulationAxis.Y ? resized.Dimensions.Y : resized.Dimensions.Z;
                    Assert.That(actual, Is.EqualTo(minimum).Within(0.00001f), $"{type} {axis} sign={sign} yaw={yaw}");
                    Assert.That(resized.Dimensions.IsValid, Is.True);
                    Assert.That(Thickness(resized), Is.EqualTo(Thickness(piece)));
                    Vector3 direction = piece.Transform.Rotation * ManipulationSession.AxisVector(axis);
                    float oldOffset = axis == ManipulationAxis.Y ? (sign > 0 ? 0f : initial) : -sign * initial * 0.5f;
                    float newOffset = axis == ManipulationAxis.Y ? (sign > 0 ? 0f : actual) : -sign * actual * 0.5f;
                    Vector3 before = piece.Transform.Position + direction * oldOffset;
                    Vector3 after = resized.Transform.Position + direction * newOffset;
                    Assert.That(Vector3.Distance(before, after), Is.LessThan(0.0001f),
                        $"{type} {axis} sign={sign} yaw={yaw}");
                    Assert.That(Quaternion.Angle(piece.Transform.Rotation, resized.Transform.Rotation),
                        Is.LessThan(RotationToleranceDegrees));
                    AssertFiniteNondegenerate(CurvedGeometryGenerator.Generate(resized.Dimensions));
                }
            }
        }

        [TestCase(PieceType.Arch, 0f)]
        [TestCase(PieceType.Arch, 45f)]
        [TestCase(PieceType.Arch, 90f)]
        [TestCase(PieceType.Vault, 0f)]
        [TestCase(PieceType.Vault, 45f)]
        [TestCase(PieceType.Vault, 90f)]
        [TestCase(PieceType.Dome, 0f)]
        [TestCase(PieceType.Dome, 45f)]
        [TestCase(PieceType.Dome, 90f)]
        public void ManipulationRetainsOppositeFaceAndOrientation(PieceType type, float yaw)
        {
            PieceData piece = Piece(type, yaw);
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            foreach (int sign in new[] { -1, 1 })
            {
                float initial = axis == ManipulationAxis.X ? piece.Dimensions.X : axis == ManipulationAxis.Y ? piece.Dimensions.Y : piece.Dimensions.Z;
                float requested = initial + 0.4f;
                var session = new ManipulationSession(piece, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f, resizeMode: ResizeMode.Face, faceSign: sign);
                PieceData changed = session.ResizeToDimension(requested);
                float actual = axis == ManipulationAxis.X ? changed.Dimensions.X : axis == ManipulationAxis.Y ? changed.Dimensions.Y : changed.Dimensions.Z;
                Vector3 direction = piece.Transform.Rotation * ManipulationSession.AxisVector(axis);
                float oldOffset = axis == ManipulationAxis.Y ? (sign > 0 ? 0f : initial) : -sign * initial * 0.5f;
                float newOffset = axis == ManipulationAxis.Y ? (sign > 0 ? 0f : actual) : -sign * actual * 0.5f;
                Vector3 before = piece.Transform.Position + direction * oldOffset;
                Vector3 after = changed.Transform.Position + direction * newOffset;
                Assert.That(Vector3.Distance(before, after), Is.LessThan(0.0001f), $"{type} {axis} {sign} yaw={yaw}");
                Assert.That(Quaternion.Angle(piece.Transform.Rotation, changed.Transform.Rotation),
                    Is.LessThan(RotationToleranceDegrees));
            }
            var move = new ManipulationSession(piece, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f).Evaluate(Vector2.right * 100f);
            Assert.That(move.Transform.Position.x, Is.EqualTo(piece.Transform.Position.x + 1f));
            Assert.That(Quaternion.Angle(piece.Transform.Rotation, move.Transform.Rotation),
                Is.LessThan(RotationToleranceDegrees));
            var rotate = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 1f).Evaluate(Vector2.right * 90f);
            Assert.That(Quaternion.Angle(rotate.Transform.Rotation, piece.Transform.Rotation), Is.GreaterThan(40f));
            Assert.That(rotate.Transform.Position, Is.EqualTo(piece.Transform.Position));
        }

        [TestCase(PieceType.Arch)]
        [TestCase(PieceType.Vault)]
        [TestCase(PieceType.Dome)]
        public void SnapFeaturesAreBoundedDeterministicAndCanCaptureNearbyPiece(PieceType type)
        {
            PieceData moving = Piece(type);
            var first = new List<SnapFeature>();
            var second = new List<SnapFeature>();
            SnapGeometry.Collect(moving, first);
            SnapGeometry.Collect(moving, second);
            Assert.That(first.Count, Is.EqualTo(second.Count));
            Assert.That(first.Count, Is.LessThan(130));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(first[i].Kind, Is.EqualTo(second[i].Kind));
                Assert.That(first[i].A, Is.EqualTo(second[i].A));
            }
            foreach (GeometricSnapKind kind in new[] { GeometricSnapKind.Endpoint, GeometricSnapKind.Edge, GeometricSnapKind.Surface })
                Assert.That(first.Exists(feature => feature.Kind == kind), Is.True, $"{type} {kind}");
            PieceData target = new PieceData(B, new PieceTransform(
                moving.Transform.Position + Vector3.right * (moving.Dimensions.X + 0.1f), Quaternion.identity), moving.Dimensions);
            var targetFeatures = new List<SnapFeature>();
            SnapGeometry.Collect(target, targetFeatures);
            bool withinCapture = false;
            foreach (SnapFeature a in first)
            foreach (SnapFeature b in targetFeatures)
                if (a.Kind == GeometricSnapKind.Endpoint && b.Kind == GeometricSnapKind.Endpoint &&
                    Vector3.Distance(a.A, b.A) <= 0.25f) withinCapture = true;
            Assert.That(withinCapture, Is.True, $"Fixture {type} must contain an endpoint within 0.25 m");
            var settings = new SnapSettings { EndpointSnapEnabled = true };
            var session = new ManipulationSession(moving, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings);
            var resolver = new SnapResolver();
            resolver.Resolve(moving, session, settings, new[] { moving });
            Assert.That(resolver.HasTarget, Is.False);
            PieceData snapped = resolver.Resolve(moving, session, settings, new[] { moving, target });
            Assert.That(resolver.HasTarget, Is.True);
            Assert.That(resolver.TargetId, Is.EqualTo(B));
            Assert.That(Vector3.Distance(snapped.Transform.Position, moving.Transform.Position), Is.LessThanOrEqualTo(0.25f));
        }

        [TestCase(PieceType.Arch)]
        [TestCase(PieceType.Vault)]
        [TestCase(PieceType.Dome)]
        public void CurvedLowerSurfaceCanSnapToSlabWithoutChangingCaptureRadius(PieceType type)
        {
            PieceData moving = Piece(type);
            PieceData slab = new PieceData(B,
                new PieceTransform(moving.Transform.Position - Vector3.up * 0.2f, Quaternion.identity),
                new SlabDimensions(5f, 0.2f, 5f));
            var settings = new SnapSettings { SurfaceSnapEnabled = true };
            var session = new ManipulationSession(moving, ManipulationMode.Move, ManipulationAxis.Y,
                Vector2.zero, Vector2.up, 100f, settings);
            var resolver = new SnapResolver();
            PieceData snapped = resolver.Resolve(moving, session, settings, new[] { slab });
            Assert.That(resolver.HasTarget, Is.True, $"{type} lower shell to Slab");
            Assert.That(resolver.ActiveKind, Is.EqualTo(GeometricSnapKind.Surface));
            Assert.That(snapped.Transform.Position.y, Is.EqualTo(moving.Transform.Position.y).Within(0.0001f));
        }

        [TestCase(PieceType.Arch)]
        [TestCase(PieceType.Vault)]
        [TestCase(PieceType.Dome)]
        public void GridAndAngleSnapUseExistingManipulationRules(PieceType type)
        {
            PieceData piece = Piece(type);
            var settings = new SnapSettings { PositionSnapEnabled = true, RotationSnapEnabled = true };
            var move = new ManipulationSession(piece, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings).Evaluate(Vector2.right * 42f);
            Assert.That(move.Transform.Position.x, Is.EqualTo(3.5f).Within(0.00001f));
            var rotate = new ManipulationSession(piece, ManipulationMode.Rotate, ManipulationAxis.Y,
                Vector2.zero, Vector2.right, 1f, settings).Evaluate(Vector2.right * 28f);
            Assert.That(Quaternion.Angle(rotate.Transform.Rotation, Quaternion.Euler(0f, 15f, 0f)),
                Is.LessThan(RotationToleranceDegrees));
        }

        [TestCase(PieceType.Arch)]
        [TestCase(PieceType.Vault)]
        [TestCase(PieceType.Dome)]
        public void CurvedEdgeSnapCapturesNearbyActualEdge(PieceType type)
        {
            PieceData moving = Piece(type);
            PieceData target = new PieceData(B, new PieceTransform(
                moving.Transform.Position + Vector3.right * 0.1f, Quaternion.identity), moving.Dimensions);
            var settings = new SnapSettings { EdgeSnapEnabled = true };
            var session = new ManipulationSession(moving, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings);
            var resolver = new SnapResolver();
            resolver.Resolve(moving, session, settings, new[] { target });
            Assert.That(resolver.HasTarget, Is.True, $"{type} has matching edges within 0.25 m");
            Assert.That(resolver.ActiveKind, Is.EqualTo(GeometricSnapKind.Edge));
        }
    }
}
