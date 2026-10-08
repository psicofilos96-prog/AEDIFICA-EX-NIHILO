using System.Collections;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class ConstructionCommandHistoryPlayModeTests
    {
        [UnityTest]
        public IEnumerator LabUndoRedoKeepsViewsAndLogicalIndexAligned()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var labObject = new GameObject("E1b history lab test");
            labObject.SetActive(false);
            try
            {
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                lab.ConfigureMaterial(material);
                labObject.SetActive(true);
                int initial = lab.World.Count;
                PieceId id = PieceIdGenerator.New();
                PieceData piece = new PieceData(id, new PieceTransform(new Vector3(80f, 0f, 80f), Quaternion.identity),
                    new BlockDimensions(2f, 2f, 2f));
                PieceData moved = piece.WithTransform(new PieceTransform(new Vector3(100f, 0f, 80f), Quaternion.identity));
                Assert.That(lab.Add(piece), Is.True);
                Assert.That(lab.Apply(moved), Is.True);
                Assert.That(lab.TryGetView(id, out PieceView view), Is.True);
                Assert.That(view.transform.position, Is.EqualTo(moved.Transform.Position));
                Assert.That(lab.Undo(), Is.True);
                Assert.That(view.transform.position, Is.EqualTo(piece.Transform.Position));
                Assert.That(lab.World.SpatialIndex.QueryNearby(new Vector3(80f, 1f, 80f), 1f), Is.EquivalentTo(new[] { id }));
                Assert.That(lab.Redo(), Is.True);
                Assert.That(view.transform.position, Is.EqualTo(moved.Transform.Position));
                Assert.That(lab.Delete(id), Is.True);
                Assert.That(lab.TryGetView(id, out _), Is.False);
                Assert.That(lab.Undo(), Is.True);
                Assert.That(lab.TryGetView(id, out PieceView restored), Is.True);
                Assert.That(restored.transform.position, Is.EqualTo(moved.Transform.Position));
                Assert.That(lab.Redo(), Is.True);
                Assert.That(lab.World.Count, Is.EqualTo(initial));
                Assert.That(lab.World.SpatialIndex.IndexedPieceCount, Is.EqualTo(initial));
                yield return null;
                Assert.That(lab.TryGetView(id, out _), Is.False);
            }
            finally
            {
                Object.Destroy(labObject);
                Object.Destroy(material);
            }
        }
    }
}
