using System;
using System.Collections.Generic;
using System.Linq;
using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class SpatialFaceResizeTests
    {
        private static PieceId Id(int value) => PieceId.Parse(value.ToString("x32"));
        private static readonly float[] Deltas = { 0f, 0.05f, 0.2f, 0.35f, 0.55f, 0.35f, 0.05f, -0.2f, -5f };

        [Test]
        public void EveryCatalogTypeAxisSignAndRotationMatchesFullScanAcrossFrames()
        {
            int serial = 1;
            foreach (PieceType type in FreePieceCatalog.Types)
            foreach (float yaw in new[] { 0f, 45f, 90f })
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            foreach (int sign in new[] { -1, 1 })
            {
                Vector3 position = serial % 2 == 0 ? new Vector3(-64f, 0f, 64f)
                    : new Vector3(1000000f, 0f, -1000000f);
                PieceData initial = FreePieceCatalog.Create(type, Id(serial++), position, yaw);
                if (type == PieceType.Wall)
                    initial = initial.WithOpening(new WallOpening(Guid.Parse("00000000-0000-0000-0000-000000000001"),
                        initial.Id, WallOpeningKind.Passage, 1f, 0f, 1f, 2f));
                var world = new ConstructionWorld();
                PieceData target = FreePieceCatalog.Create(PieceType.Block, Id(900000 + serial),
                    position + initial.Transform.Rotation * new Vector3(2.1f, 0f, 0f), yaw);
                world.Create(target);
                world.Create(initial); // own ID must never appear among candidates
                var session = new ManipulationSession(initial, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f, null, ResizeMode.Face, sign);
                SnapSettings settings = ContinuousSnapWorkload.Settings();
                var full = new SnapResolver();
                var spatial = new SnapResolver();
                float dimension = axis == ManipulationAxis.X ? initial.Dimensions.X
                    : axis == ManipulationAxis.Y ? initial.Dimensions.Y : initial.Dimensions.Z;
                for (int frame = 0; frame < Deltas.Length; frame++)
                {
                    PieceData raw = session.ResizeToDimension(dimension + Deltas[frame]);
                    AssertFaceFeaturesInsideIndexedEnvelope(raw, axis, sign);
                    IReadOnlyList<PieceData> candidates = SpatialSnapCandidates.ForFaceResize(world, raw, session, settings);
                    Assert.That(candidates.Select(piece => piece.Id).Distinct().Count(), Is.EqualTo(candidates.Count));
                    Assert.That(candidates.Any(piece => piece.Id == initial.Id), Is.False);
                    PieceData expected = full.Resolve(raw, session, settings, world.Pieces);
                    PieceData actual = spatial.Resolve(raw, session, settings, candidates);
                    Equal(expected, actual, full, spatial, $"{type} axis={axis} sign={sign} yaw={yaw} frame={frame}");
                }
            }
        }

        [Test]
        public void ConcurrentTargetsAndRemovalKeepPersistentFaceLockEquivalent()
        {
            ConstructionWorld world = ContinuousSnapWorkload.World(0);
            PieceData initial = ContinuousSnapWorkload.Initial(1);
            ManipulationSession session = ContinuousSnapWorkload.Session(initial, true);
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            var full = new SnapResolver();
            var spatial = new SnapResolver();
            int hits = 0;
            for (int frame = 0; frame < ContinuousSnapWorkload.Frames; frame++)
            {
                PieceData raw = ContinuousSnapWorkload.Raw(initial, frame, true);
                PieceData expected = full.Resolve(raw, session, settings, world.Pieces);
                PieceData actual = spatial.Resolve(raw, session, settings,
                    SpatialSnapCandidates.ForFaceResize(world, raw, session, settings));
                Equal(expected, actual, full, spatial, $"before removal frame={frame}");
                if (full.HasTarget) hits++;
                if (frame != 0 || !full.HasTarget) continue;
                Assert.That(world.Delete(full.TargetId).Changed, Is.True);
            }
            Assert.That(hits, Is.GreaterThan(0), "The trajectory must exercise real Face Resize targets.");
        }

        [Test]
        public void LongTargetWithDistantCenterIsReturnedNearFace()
        {
            var world = new ConstructionWorld();
            PieceData initial = ContinuousSnapWorkload.Initial(3).WithTransform(
                new PieceTransform(ContinuousSnapWorkload.Origin(3), Quaternion.Euler(0f, 45f, 0f)));
            Vector3 alongFace = initial.Transform.Rotation * Vector3.right;
            PieceData longTarget = new PieceData(Id(990001),
                new PieceTransform(initial.Transform.Position + alongFace * 75.6f, initial.Transform.Rotation),
                new BlockDimensions(150f, 1f, 1f));
            world.Create(longTarget);
            world.Create(initial);
            var session = ContinuousSnapWorkload.Session(initial, true);
            PieceData raw = ContinuousSnapWorkload.Raw(initial, 0, true);
            IReadOnlyList<PieceData> candidates = SpatialSnapCandidates.ForFaceResize(world, raw,
                session, ContinuousSnapWorkload.Settings());
            Assert.That(candidates.Any(piece => piece.Id == longTarget.Id), Is.True);
            Assert.That(candidates.Any(piece => piece.Id == initial.Id), Is.False);
        }

        [Test]
        public void TargetMovementRotationAndResizeRefreshFaceCandidates()
        {
            ConstructionWorld world = ContinuousSnapWorkload.World(0);
            PieceData initial = ContinuousSnapWorkload.Initial(0);
            var session = ContinuousSnapWorkload.Session(initial, true);
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            var full = new SnapResolver();
            var spatial = new SnapResolver();
            PieceId targetId = Id(700001);
            Assert.That(world.TryGet(targetId, out PieceData target), Is.True);
            Compare();
            PieceData distant = target.WithTransform(new PieceTransform(new Vector3(128f, 0f, 0f),
                Quaternion.identity));
            Assert.That(world.Update(targetId, distant).Changed, Is.True);
            Compare();
            PieceData returned = target.WithTransform(new PieceTransform(target.Transform.Position,
                Quaternion.Euler(0f, 45f, 0f))).WithDimensions(new PieceDimensions(new BlockDimensions(2f, 1f, 1f)));
            Assert.That(world.Update(targetId, returned).Changed, Is.True);
            Compare();

            void Compare()
            {
                PieceData raw = ContinuousSnapWorkload.Raw(initial, 0, true);
                PieceData expected = full.Resolve(raw, session, settings, world.Pieces);
                PieceData actual = spatial.Resolve(raw, session, settings,
                    SpatialSnapCandidates.ForFaceResize(world, raw, session, settings));
                Equal(expected, actual, full, spatial, "target update");
            }
        }

        [Test]
        public void AllBlockFacesCaptureAndTrackAnAdjacentTarget()
        {
            int serial = 1;
            foreach (float yaw in new[] { 0f, 45f, 90f })
            foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
            foreach (int sign in new[] { -1, 1 })
            {
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                PieceData initial = new PieceData(Id(950000 + serial++),
                    new PieceTransform(new Vector3(-64f, 0f, 64f), rotation),
                    new BlockDimensions(1f, 1f, 1f));
                var sourceFeatures = new List<SnapFeature>();
                SnapGeometry.Collect(initial, sourceFeatures, true, axis, sign);
                Vector3 sourcePoint = sourceFeatures.First(feature => feature.Kind == GeometricSnapKind.Endpoint).A;
                Vector3 outward = rotation * ManipulationSession.AxisVector(axis) * sign;
                Vector3 targetCorner = new Vector3(axis == ManipulationAxis.X ? -sign * 0.5f : -0.5f,
                    axis == ManipulationAxis.Y && sign < 0 ? 1f : 0f,
                    axis == ManipulationAxis.Z ? -sign * 0.5f : -0.5f);
                Vector3 targetCenter = sourcePoint + outward * 0.1f - rotation * targetCorner;
                PieceData target = new PieceData(Id(960000 + serial),
                    new PieceTransform(targetCenter, rotation), new BlockDimensions(1f, 1f, 1f));
                var world = new ConstructionWorld();
                world.Create(target);
                var session = new ManipulationSession(initial, ManipulationMode.Resize, axis,
                    Vector2.zero, Vector2.right, 100f, null, ResizeMode.Face, sign);
                var full = new SnapResolver();
                var spatial = new SnapResolver();
                SnapSettings settings = ContinuousSnapWorkload.Settings();
                foreach (float delta in new[] { 0f, 0.1f, 0.3f, 0.5f, 0.3f, 0f })
                {
                    PieceData raw = session.ResizeToDimension(1f + delta);
                    PieceData expected = full.Resolve(raw, session, settings, world.Pieces);
                    PieceData actual = spatial.Resolve(raw, session, settings,
                        SpatialSnapCandidates.ForFaceResize(world, raw, session, settings));
                    Equal(expected, actual, full, spatial, $"axis={axis} sign={sign} yaw={yaw} delta={delta}");
                    if (delta == 0f) Assert.That(full.HasTarget, Is.True,
                        $"The adjacent target must be eligible on {axis}/{sign} yaw={yaw}.");
                }
            }
        }

        private static void Equal(PieceData expected, PieceData actual, SnapResolver full,
            SnapResolver spatial, string context)
        {
            Assert.That(actual.Transform.Equals(expected.Transform), Is.True, context + " transform");
            Assert.That(actual.Dimensions.Equals(expected.Dimensions), Is.True, context + " dimensions");
            Assert.That(spatial.HasTarget, Is.EqualTo(full.HasTarget), context + " has target");
            if (!full.HasTarget) return;
            Assert.That(spatial.TargetId, Is.EqualTo(full.TargetId), context + " ID");
            Assert.That(spatial.ActiveKind, Is.EqualTo(full.ActiveKind), context + " kind");
            Assert.That(spatial.TargetPoint, Is.EqualTo(full.TargetPoint), context + " point");
        }

        private static void AssertFaceFeaturesInsideIndexedEnvelope(PieceData raw, ManipulationAxis axis, int sign)
        {
            Bounds bounds = ConstructionChangeSet.WorldBounds(raw);
            Vector3 center = bounds.center, extent = bounds.extents;
            float magnitude = Mathf.Max(Mathf.Max(Mathf.Abs(center.x) + extent.x,
                Mathf.Abs(center.y) + extent.y), Mathf.Abs(center.z) + extent.z);
            // Half the production query margin, including large-coordinate float rounding.
            bounds.Expand(2f * (0.0005f + magnitude * 0.0000005f));
            var features = new List<SnapFeature>();
            SnapGeometry.Collect(raw, features, true, axis, sign);
            foreach (SnapFeature feature in features)
            {
                string context = $"{raw.Type} {axis}/{sign} {raw.Transform.Position} {feature.Kind}/{feature.Index}";
                Assert.That(bounds.Contains(feature.A), Is.True, context + " A");
                if (feature.Kind == GeometricSnapKind.Edge)
                    Assert.That(bounds.Contains(feature.B), Is.True, context + " B");
                if (feature.Kind != GeometricSnapKind.Surface) continue;
                Vector3 origin = feature.Triangle ? feature.A - (feature.U + feature.V) / 3f : feature.A;
                Assert.That(bounds.Contains(origin), Is.True, context + " origin");
                if (feature.Triangle)
                {
                    Assert.That(bounds.Contains(origin + feature.U), Is.True, context + " U");
                    Assert.That(bounds.Contains(origin + feature.V), Is.True, context + " V");
                }
                else
                    foreach (int u in new[] { -1, 1 }) foreach (int v in new[] { -1, 1 })
                        Assert.That(bounds.Contains(origin + u * feature.U + v * feature.V), Is.True,
                            context + $" corner=({u},{v})");
            }
        }
    }
}
