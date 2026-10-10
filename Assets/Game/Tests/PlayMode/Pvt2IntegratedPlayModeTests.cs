using System;
using System.Collections;
using System.IO;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class Pvt2IntegratedPlayModeTests
    {
        [UnityTest]
        public IEnumerator StreamingEditsPickingAndSnapshotRetainLogicalIdentity()
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            Shader terrainShader=Shader.Find("Universal Render Pipeline/Terrain/Lit");
            Assert.That(terrainShader,Is.Not.Null);
            var terrainMaterial=new Material(terrainShader);
            var root=new GameObject("PVT-2 integration test");
            var world=new ConstructionWorld();
            for (int i=0;i<10;i++) Assert.That(world.Create(Pvt2Scenario.PieceAt(i)).Changed,Is.True);
            var engine=new PvtChunkVisualEngine(world,new MaterialRegistry(material),root.transform);
            Pvt2Environment environment=null;
            try
            {
                Assert.That(engine.RebuildDirty(),Is.GreaterThan(0));
                environment=new Pvt2Environment(root.transform,Pvt2Scenario.Seed,terrainMaterial,
                    material,material,material,material,material);
                Assert.That(root.GetComponentInChildren<TerrainCollider>(),Is.Not.Null);
                Assert.That(root.GetComponentInChildren<Terrain>().materialTemplate,Is.SameAs(terrainMaterial));
                Assert.That(environment.UpdateStreaming(new Vector3(-250f,30f,-250f),1),Is.True);
                Assert.That(environment.LoadedRegions,Is.GreaterThan(0));
                Assert.That(environment.VegetationRenderers,Is.GreaterThan(0));
                Vector2Int firstCell=Pvt2Environment.Cell(new Vector3(-250f,30f,-250f));
                Assert.That(environment.IsLoaded(firstCell),Is.True);
                Assert.That(environment.UpdateStreaming(new Vector3(250f,30f,250f),1),Is.True);
                Assert.That(environment.LoadedRegions,Is.GreaterThan(0));
                Assert.That(environment.IsLoaded(firstCell),Is.False);
                var before=PvtIntegrity.Capture(world);
                var edits=new Pvt2EditWorkload.Session(world,engine,Pvt2Scenario.Seed,1);
                int steps=0;
                while (!edits.Complete)
                {
                    Pvt2EditWorkload.Sample sample=edits.Next();
                    steps++;
                    if (sample.Operation=="create")
                        Assert.That(engine.PieceCount,Is.EqualTo(before.Count+1));
                    if (sample.Operation=="recolor")
                    {
                        Assert.That(world.TryGet(edits.CreatedId,out PieceData recolored),Is.True);
                        Assert.That(recolored.MaterialId,Is.EqualTo(LabMaterialIds.Brick));
                    }
                    if (sample.Operation=="delete")
                        Assert.That(world.TryGet(edits.CreatedId,out _),Is.False);
                    yield return null; // each intermediate visual state reaches a rendered frame
                }
                Assert.That(steps,Is.EqualTo(15));
                Assert.That(PvtIntegrity.Matches(world,before),Is.True);
                Assert.That(engine.PieceCount,Is.EqualTo(world.Count));
                Assert.That(Pvt2Snapshot.Matches(Pvt2Snapshot.Deserialize(Pvt2Snapshot.Serialize(world)),before),Is.True);
                string path=Path.Combine(Application.temporaryCachePath,"pvt2_test_"+Guid.NewGuid().ToString("N")+".pvt2");
                try
                {
                    Assert.That(Pvt2Snapshot.Save(world,path),Is.GreaterThan(0));
                    Assert.That(Pvt2Snapshot.Matches(Pvt2Snapshot.Load(path),before),Is.True);
                }
                finally { if (File.Exists(path)) File.Delete(path); }
            }
            finally
            {
                environment?.Dispose();
                engine.Dispose();
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(material);
                UnityEngine.Object.Destroy(terrainMaterial);
            }
            yield return null;
        }
    }
}
