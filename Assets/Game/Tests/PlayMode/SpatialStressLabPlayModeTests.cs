using System.Collections;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class SpatialStressLabPlayModeTests
    {
        [UnityTest]
        public IEnumerator LogicalScenarioRunsWithoutPieceViewsAndRemainsQueryable()
        {
            var world = new ConstructionWorld(SpatialStressScenario.ChunkMeters);
            var reference = new Dictionary<PieceId, PieceData>();
            foreach (PieceData piece in SpatialStressScenario.Generate(1000, SpatialStressScenario.Seed))
            {
                world.Create(piece);
                reference.Add(piece.Id, piece);
            }
            yield return null;
            SpatialStressScenario.Validate(world, reference);
            Assert.That(world.Count, Is.EqualTo(1000));
            Assert.That(world.SpatialIndex.OccupiedChunkCount, Is.GreaterThan(1));
            var history = new ConstructionCommandHistory(world);
            PieceData chosen = null;
            foreach (PieceData piece in reference.Values) { chosen = piece; break; }
            Assert.That(history.Delete(chosen.Id).Changed, Is.True);
            reference.Remove(chosen.Id);
            SpatialStressScenario.Validate(world, reference);
            Assert.That(history.TryUndo(out _), Is.True);
            reference.Add(chosen.Id, chosen);
            SpatialStressScenario.Validate(world, reference);
        }
    }
}
