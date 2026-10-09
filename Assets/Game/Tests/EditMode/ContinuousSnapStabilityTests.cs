using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class ContinuousSnapStabilityTests
    {
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 0)]
        [TestCase(0, 1000)]
        [TestCase(1, 1000)]
        [TestCase(2, 1000)]
        [TestCase(3, 1000)]
        public void MoveTrajectoryRetainsEquivalentSnapStateAcrossEveryFrame(int scenario, int stressPieces)
        {
            ConstructionWorld world = ContinuousSnapWorkload.World(stressPieces);
            PieceData initial = ContinuousSnapWorkload.Initial(scenario);
            ManipulationSession session = ContinuousSnapWorkload.Session(initial, false);
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            var linear = new SnapResolver();
            var spatial = new SnapResolver();
            int linearTransitions = 0, spatialTransitions = 0;
            int matchedFrames = 0;
            bool oldLinearHit = false, oldSpatialHit = false;
            PieceId oldLinearId = default, oldSpatialId = default;
            for (int frame = 0; frame < ContinuousSnapWorkload.Frames; frame++)
            {
                PieceData raw = ContinuousSnapWorkload.Raw(initial, frame, false);
                PieceData expected = linear.Resolve(raw, session, settings, world.Pieces);
                PieceData actual = spatial.Resolve(raw, session, settings,
                    SpatialSnapCandidates.ForMove(world, raw, settings));
                AssertEquivalent(expected, actual, linear, spatial, scenario, frame);
                if (linear.HasTarget) matchedFrames++;
                if (frame > 0 && (oldLinearHit != linear.HasTarget ||
                    oldLinearHit && linear.HasTarget && oldLinearId != linear.TargetId)) linearTransitions++;
                if (frame > 0 && (oldSpatialHit != spatial.HasTarget ||
                    oldSpatialHit && spatial.HasTarget && oldSpatialId != spatial.TargetId)) spatialTransitions++;
                oldLinearHit = linear.HasTarget;
                oldSpatialHit = spatial.HasTarget;
                oldLinearId = linear.TargetId;
                oldSpatialId = spatial.TargetId;
            }
            Assert.That(spatialTransitions, Is.EqualTo(linearTransitions));
            Assert.That(matchedFrames, Is.GreaterThan(0), "Trajectory must exercise actual snap targets.");
            if (stressPieces == 0)
                Assert.That(linearTransitions, Is.GreaterThan(0),
                    "The isolated trajectory must exercise target retention or release.");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FaceResizeTrajectoryIsDeterministicWithOriginalFullScan(int scenario)
        {
            ConstructionWorld world = ContinuousSnapWorkload.World(0);
            PieceData initial = ContinuousSnapWorkload.Initial(scenario);
            ManipulationSession session = ContinuousSnapWorkload.Session(initial, true);
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            var first = new SnapResolver();
            var repeat = new SnapResolver();
            for (int frame = 0; frame < ContinuousSnapWorkload.Frames; frame++)
            {
                PieceData raw = ContinuousSnapWorkload.Raw(initial, frame, true);
                PieceData expected = first.Resolve(raw, session, settings, world.Pieces);
                PieceData actual = repeat.Resolve(raw, session, settings, world.Pieces);
                AssertEquivalent(expected, actual, first, repeat, scenario, frame);
                Assert.That(float.IsNaN(actual.Dimensions.X) || float.IsInfinity(actual.Dimensions.X), Is.False);
            }
        }

        [Test]
        public void StationaryFramesDoNotOscillateAndTargetRemovalReleasesLock()
        {
            ConstructionWorld world = ContinuousSnapWorkload.World(0);
            PieceData initial = ContinuousSnapWorkload.Initial(0);
            ManipulationSession session = ContinuousSnapWorkload.Session(initial, false);
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            var linear = new SnapResolver();
            var spatial = new SnapResolver();
            PieceData raw = ContinuousSnapWorkload.Raw(initial, 0, false);
            PieceId captured = default;
            for (int frame = 0; frame < 12; frame++)
            {
                PieceData expected = linear.Resolve(raw, session, settings, world.Pieces);
                PieceData actual = spatial.Resolve(raw, session, settings,
                    SpatialSnapCandidates.ForMove(world, raw, settings));
                AssertEquivalent(expected, actual, linear, spatial, 0, frame);
                Assert.That(linear.HasTarget, Is.True);
                if (frame == 0) captured = linear.TargetId;
                else Assert.That(linear.TargetId, Is.EqualTo(captured), $"stationary frame={frame}");
            }
            Assert.That(world.Delete(captured).Changed, Is.True);
            PieceData afterRemoval = linear.Resolve(raw, session, settings, world.Pieces);
            PieceData spatialAfterRemoval = spatial.Resolve(raw, session, settings,
                SpatialSnapCandidates.ForMove(world, raw, settings));
            AssertEquivalent(afterRemoval, spatialAfterRemoval, linear, spatial, 0, 12);
            Assert.That(linear.HasTarget && linear.TargetId == captured, Is.False);
        }

        private static void AssertEquivalent(PieceData expected, PieceData actual, SnapResolver linear,
            SnapResolver compared, int scenario, int frame)
        {
            string context = $"scenario={scenario}, frame={frame}";
            Assert.That(actual.Transform.Equals(expected.Transform), Is.True, context + " transform");
            Assert.That(actual.Dimensions.Equals(expected.Dimensions), Is.True, context + " dimensions");
            Assert.That(compared.HasTarget, Is.EqualTo(linear.HasTarget), context + " target presence");
            if (!linear.HasTarget) return;
            Assert.That(compared.TargetId, Is.EqualTo(linear.TargetId), context + " target ID");
            Assert.That(compared.ActiveKind, Is.EqualTo(linear.ActiveKind), context + " kind");
            Assert.That(compared.TargetPoint, Is.EqualTo(linear.TargetPoint), context + " point");
        }
    }
}
