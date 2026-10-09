using System.Collections;
using Aedifica.Construction;
using Aedifica.Interaction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class SpatialSnapCandidatesPlayModeTests
    {
        [UnityTest]
        public IEnumerator LogicalWorldMoveSnapMatchesFullScanAfterIndexMutation()
        {
            var world = new ConstructionWorld();
            foreach (PieceData piece in SpatialStressScenario.Generate(1000, SpatialStressScenario.Seed)) world.Create(piece);
            PieceId targetId = PieceId.Parse("11111111111111111111111111111111");
            PieceId movingId = PieceId.Parse("22222222222222222222222222222222");
            PieceData target = new PieceData(targetId,
                new PieceTransform(new Vector3(-63f, 0f, 64f), Quaternion.identity), new BlockDimensions(1f, 1f, 1f));
            PieceData moving = new PieceData(movingId,
                new PieceTransform(new Vector3(-64.1f, 0f, 64f), Quaternion.identity), new BlockDimensions(1f, 1f, 1f));
            world.Create(target);
            var settings = new SnapSettings { EndpointSnapEnabled = true };
            var session = new ManipulationSession(moving, ManipulationMode.Move, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f);
            yield return null;
            Compare();
            world.Update(targetId, target.WithTransform(new PieceTransform(new Vector3(200f, 0f, 64f), Quaternion.identity)));
            Compare();
            world.Delete(targetId);
            Compare();

            void Compare()
            {
                var full = new SnapResolver();
                var indexed = new SnapResolver();
                PieceData expected = full.Resolve(moving, session, settings, world.Pieces);
                PieceData actual = indexed.Resolve(moving, session, settings, SpatialSnapCandidates.ForMove(world, moving, settings));
                Assert.That(actual.Transform.Equals(expected.Transform), Is.True);
                Assert.That(indexed.HasTarget, Is.EqualTo(full.HasTarget));
                if (full.HasTarget) Assert.That(indexed.TargetId, Is.EqualTo(full.TargetId));
            }
        }
    }
}
