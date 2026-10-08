using System.Collections;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class ConstructionChangeSetPlayModeTests
    {
        [UnityTest]
        public IEnumerator LabCreateUpdateNoOpAndDeleteKeepWorldAndViewAligned()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var labObject = new GameObject("ChangeSet lab test");
            labObject.SetActive(false);
            try
            {
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                lab.ConfigureMaterial(material);
                labObject.SetActive(true);
                int baseline = lab.World.Count;
                PieceId id = PieceIdGenerator.New();
                PieceData piece = new PieceData(id,
                    new PieceTransform(new Vector3(50f, 0f, 50f), Quaternion.identity),
                    new BlockDimensions(2f, 1f, 3f));
                Assert.That(lab.Add(piece), Is.True);
                Assert.That(lab.World.Count, Is.EqualTo(baseline + 1));
                Assert.That(lab.TryGetView(id, out PieceView view), Is.True);
                Mesh originalMesh = view.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(lab.Apply(piece.WithTransform(piece.Transform)), Is.True);
                Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
                PieceData moved = piece.WithTransform(new PieceTransform(new Vector3(55f, 0f, 50f), Quaternion.identity));
                Assert.That(lab.Apply(moved), Is.True);
                Assert.That(view.transform.position, Is.EqualTo(moved.Transform.Position));
                Assert.That(lab.Delete(id), Is.True);
                Assert.That(lab.TryGetView(id, out _), Is.False);
                Assert.That(lab.World.Count, Is.EqualTo(baseline));
                yield return null;
                Assert.That(view == null, Is.True);
            }
            finally
            {
                Object.Destroy(labObject);
                Object.Destroy(material);
            }
        }
    }
}
