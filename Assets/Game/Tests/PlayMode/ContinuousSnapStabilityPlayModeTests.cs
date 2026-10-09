using System.Collections;
using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class ContinuousSnapStabilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator ChunkBoundaryGestureKeepsLinearAndSpatialResultsEqualAcrossFrames()
        {
            ConstructionWorld world = ContinuousSnapWorkload.World(0);
            PieceData initial = ContinuousSnapWorkload.Initial(1);
            ManipulationSession session = ContinuousSnapWorkload.Session(initial, false);
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            var linear = new SnapResolver();
            var spatial = new SnapResolver();
            for (int frame = 0; frame < ContinuousSnapWorkload.Frames; frame++)
            {
                PieceData raw = ContinuousSnapWorkload.Raw(initial, frame, false);
                PieceData expected = linear.Resolve(raw, session, settings, world.Pieces);
                PieceData actual = spatial.Resolve(raw, session, settings,
                    SpatialSnapCandidates.ForMove(world, raw, settings));
                Assert.That(actual.Transform.Equals(expected.Transform), Is.True, $"frame={frame}");
                Assert.That(actual.Dimensions.Equals(expected.Dimensions), Is.True, $"frame={frame}");
                Assert.That(spatial.HasTarget, Is.EqualTo(linear.HasTarget), $"frame={frame}");
                if (linear.HasTarget)
                {
                    Assert.That(spatial.TargetId, Is.EqualTo(linear.TargetId), $"frame={frame}");
                    Assert.That(spatial.ActiveKind, Is.EqualTo(linear.ActiveKind), $"frame={frame}");
                    Assert.That(spatial.TargetPoint, Is.EqualTo(linear.TargetPoint), $"frame={frame}");
                }
                yield return null;
            }
        }
    }
}
