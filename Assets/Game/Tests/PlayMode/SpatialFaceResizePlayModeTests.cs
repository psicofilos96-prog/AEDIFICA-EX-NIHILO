using System.Collections;
using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class SpatialFaceResizePlayModeTests
    {
        [UnityTest]
        public IEnumerator FaceResizeAcrossChunkBoundaryMatchesFullScanEveryFrame()
        {
            ConstructionWorld world = ContinuousSnapWorkload.World(0);
            PieceData initial = ContinuousSnapWorkload.Initial(1);
            ManipulationSession session = ContinuousSnapWorkload.Session(initial, true);
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            var linear = new SnapResolver();
            var spatial = new SnapResolver();
            int hits = 0;
            for (int frame = 0; frame < ContinuousSnapWorkload.Frames; frame++)
            {
                PieceData raw = ContinuousSnapWorkload.Raw(initial, frame, true);
                PieceData expected = linear.Resolve(raw, session, settings, world.Pieces);
                PieceData actual = spatial.Resolve(raw, session, settings,
                    SpatialSnapCandidates.ForFaceResize(world, raw, session, settings));
                Assert.That(actual.Transform.Equals(expected.Transform), Is.True, $"frame={frame} transform");
                Assert.That(actual.Dimensions.Equals(expected.Dimensions), Is.True, $"frame={frame} dimensions");
                Assert.That(spatial.HasTarget, Is.EqualTo(linear.HasTarget), $"frame={frame} target presence");
                if (linear.HasTarget)
                {
                    hits++;
                    Assert.That(spatial.TargetId, Is.EqualTo(linear.TargetId), $"frame={frame} ID");
                    Assert.That(spatial.ActiveKind, Is.EqualTo(linear.ActiveKind), $"frame={frame} kind");
                    Assert.That(spatial.TargetPoint, Is.EqualTo(linear.TargetPoint), $"frame={frame} point");
                }
                yield return null;
            }
            Assert.That(hits, Is.GreaterThan(0));
        }
    }
}
