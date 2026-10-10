using System.Collections;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PvtFinalPlayModeTests
    {
        [UnityTest]
        public IEnumerator TerrainSampleEditUpdatesTerrainAndRestoresValue()
        {
            Shader lit=Shader.Find("Universal Render Pipeline/Lit");
            Shader terrainShader=Shader.Find("Universal Render Pipeline/Terrain/Lit");
            Assert.That(lit,Is.Not.Null);
            Assert.That(terrainShader,Is.Not.Null);
            var material=new Material(lit);
            var terrainMaterial=new Material(terrainShader);
            var root=new GameObject("PVT-Final terrain test");
            Pvt2Environment environment=null;
            try
            {
                environment=new Pvt2Environment(root.transform,PvtFinalScenario.Seed,terrainMaterial,
                    material,material,material,material,material);
                Assert.That(environment.TerrainResolution,Is.EqualTo(257));
                float before=environment.TerrainHeight(200,190);
                environment.SetTerrainHeight(200,190,before+0.25f);
                Assert.That(environment.TerrainHeight(200,190),Is.EqualTo(before+0.25f).Within(0.001f));
                environment.SetTerrainHeight(200,190,before);
                Assert.That(environment.TerrainHeight(200,190),Is.EqualTo(before).Within(0.001f));
            }
            finally
            {
                environment?.Dispose();
                Object.Destroy(root);
                Object.Destroy(material);
                Object.Destroy(terrainMaterial);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MixedBuildingPagesRemainEditableAndReversible()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            var root=new GameObject("PVT-Final test");
            var world=new ConstructionWorld();
            for (int i=0;i<10;i++) Assert.That(world.Create(PvtFinalScenario.PieceAt(i,50000)).Changed,Is.True);
            for (int i=34000;i<34080;i++) Assert.That(world.Create(PvtFinalScenario.PieceAt(i,50000)).Changed,Is.True);
            var engine=new PvtChunkVisualEngine(world,new MaterialRegistry(material),root.transform);
            try
            {
                Assert.That(engine.RebuildDirty(),Is.GreaterThan(0));
                Assert.That(engine.PieceCount,Is.EqualTo(world.Count));
                var before=PvtIntegrity.Capture(world);
                var session=new PvtFinalEditWorkload.Session(world,engine,PvtFinalScenario.Seed,1,50000);
                while (!session.Complete)
                {
                    PvtFinalEditWorkload.Sample sample=session.Next();
                    Assert.That(sample.RebuiltRegions,Is.GreaterThanOrEqualTo(0));
                    Assert.That(sample.AllocatedBytes,Is.GreaterThanOrEqualTo(0));
                    yield return null;
                }
                Assert.That(PvtIntegrity.Matches(world,before),Is.True);
                Assert.That(engine.PieceCount,Is.EqualTo(world.Count));
            }
            finally
            {
                engine.Dispose();
                Object.Destroy(root);
                Object.Destroy(material);
            }
            yield return null;
        }
    }
}
