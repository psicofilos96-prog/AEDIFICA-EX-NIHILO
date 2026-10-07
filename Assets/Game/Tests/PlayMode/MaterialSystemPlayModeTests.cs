using System.Collections;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class MaterialSystemPlayModeTests
    {
        [UnityTest]
        public IEnumerator LabMaterialsShareAssetsAndChangeWithoutMeshRebuild()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var neutral = new Material(shader);
            var stone = new Material(shader);
            var brick = new Material(shader);
            var plaster = new Material(shader);
            var labObject = new GameObject("Material Test Lab");
            labObject.SetActive(false);
            try
            {
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                lab.ConfigureMaterials(neutral, stone, brick, plaster);
                labObject.SetActive(true);
                yield return null;

                PieceId blockA = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
                PieceId blockB = PieceId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
                PieceId wall = PieceId.Parse("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
                PieceId otherWall = PieceId.Parse("ffffffffffffffffffffffffffffffff");
                PieceId slab = PieceId.Parse("dddddddddddddddddddddddddddddddd");
                Assert.That(lab.TryGetView(blockA, out PieceView a), Is.True);
                Assert.That(lab.TryGetView(blockB, out PieceView b), Is.True);
                Assert.That(lab.TryGetView(wall, out PieceView w), Is.True);
                Assert.That(lab.TryGetView(otherWall, out PieceView w2), Is.True);
                Assert.That(lab.TryGetView(slab, out PieceView s), Is.True);
                Assert.That(a.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(neutral));
                Assert.That(b.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(neutral));
                Assert.That(w.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(stone));
                Assert.That(w2.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(brick));
                Assert.That(s.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(plaster));
                foreach (PieceId newId in new[] {
                    PieceId.Parse("10000000000000000000000000000001"),
                    PieceId.Parse("10000000000000000000000000000003"),
                    PieceId.Parse("10000000000000000000000000000004") })
                {
                    Assert.That(lab.TryGetView(newId, out PieceView newView), Is.True);
                    Mesh originalMesh = newView.GetComponent<MeshFilter>().sharedMesh;
                    newView.SetSelected(true);
                    Assert.That(lab.CycleMaterial(newId), Is.True);
                    Assert.That(newView.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
                    Assert.That(newView.transform.localScale, Is.EqualTo(Vector3.one));
                    newView.SetSelected(false);
                }
                foreach (PieceId roofId in new[] {
                    PieceId.Parse("20000000000000000000000000000001"),
                    PieceId.Parse("20000000000000000000000000000002"),
                    PieceId.Parse("20000000000000000000000000000003"),
                    PieceId.Parse("20000000000000000000000000000004") })
                {
                    Assert.That(lab.TryGetView(roofId, out PieceView roofView), Is.True);
                    Mesh roofMesh = roofView.GetComponent<MeshFilter>().sharedMesh;
                    roofView.SetSelected(true);
                    Assert.That(lab.CycleMaterial(roofId), Is.True);
                    Assert.That(roofView.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(roofMesh));
                    roofView.SetSelected(false);
                }

                Mesh mesh = w.GetComponent<MeshFilter>().sharedMesh;
                w.SetSelected(true);
                Assert.That(lab.CycleMaterial(wall), Is.True);
                Assert.That(lab.World.TryGet(wall, out PieceData changed), Is.True);
                Assert.That(changed.MaterialId, Is.EqualTo(LabMaterialIds.Brick));
                Assert.That(w.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(brick));
                Assert.That(w.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                w.SetSelected(false);
                Assert.That(w.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(brick));
                Assert.That(lab.Apply(changed.WithMaterial(MaterialId.Parse("99999999999949998999999999999999"))), Is.True);
                Assert.That(w.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(neutral));
                Assert.That(w.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(lab.Apply(changed.WithMaterial(default)), Is.True);
                Assert.That(w.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(neutral));
                var absentObject = new GameObject("Absent Material View");
                try
                {
                    var absentView = absentObject.AddComponent<PieceView>();
                    absentView.Initialize(changed.WithMaterial(default), lab.Registry);
                    Assert.That(absentObject.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(neutral));
                }
                finally { Object.DestroyImmediate(absentObject); }
            }
            finally
            {
                Object.DestroyImmediate(labObject);
                Object.DestroyImmediate(neutral);
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(brick);
                Object.DestroyImmediate(plaster);
            }
        }
    }
}
