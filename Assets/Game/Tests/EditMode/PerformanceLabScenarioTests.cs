using System.Linq;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;

namespace Aedifica.Tests.EditMode
{
    public sealed class PerformanceLabScenarioTests
    {
        [TestCase(1, 8)]
        [TestCase(10, 80)]
        public void ScenarioHasStableIdsGeometryAndCount(int houses, int expected)
        {
            PieceData[] first = PerformanceLabScenario.Generate(houses, 1202).ToArray();
            PieceData[] second = PerformanceLabScenario.Generate(houses, 1202).ToArray();
            Assert.That(first, Has.Length.EqualTo(expected));
            Assert.That(first.Select(p => p.Id).Distinct().Count(), Is.EqualTo(expected));
            var world = new ConstructionWorld();
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(first[i].Id, Is.EqualTo(second[i].Id));
                Assert.That(first[i].Transform, Is.EqualTo(second[i].Transform));
                Assert.That(first[i].Dimensions.IsValid, Is.True);
                Assert.That(world.Create(first[i]).Changed, Is.True);
            }
            Assert.That(world.Count, Is.EqualTo(expected));
            Assert.That(PerformanceLabScenario.Generate(houses, 1203).First().Id, Is.Not.EqualTo(first[0].Id));
        }
    }
}
