using System.Collections;
using Aedifica.Construction;
using Aedifica.Interaction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class SnapLabPlayModeTests
    {
        [UnityTest]
        public IEnumerator SnapUpdatesWorldAndViewWithoutRebuildingMeshOrMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var labObject = new GameObject("Snap Test Lab");
            labObject.SetActive(false);
            try
            {
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                lab.ConfigureMaterial(material);
                labObject.SetActive(true);
                yield return null;

                foreach (string textId in new[] { "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "dddddddddddddddddddddddddddddddd" })
                {
                    PieceId id = PieceId.Parse(textId);
                    Assert.That(lab.World.TryGet(id, out PieceData initial), Is.True);
                    Assert.That(lab.TryGetView(id, out PieceView view), Is.True);
                    Mesh originalMesh = view.GetComponent<MeshFilter>().sharedMesh;
                    Material originalMaterial = view.GetComponent<MeshRenderer>().sharedMaterial;
                    var settings = new SnapSettings { PositionSnapEnabled = true, RotationSnapEnabled = true };
                    var move = new ManipulationSession(initial, ManipulationMode.Move, ManipulationAxis.X,
                        Vector2.zero, Vector2.right, 100f, settings);
                    PieceData snappedMove = move.Evaluate(Vector2.right * 36f);
                    Assert.That(lab.Apply(snappedMove), Is.True);
                    Assert.That(lab.World.TryGet(id, out PieceData stored), Is.True);
                    Assert.That(stored.Transform, Is.EqualTo(snappedMove.Transform));
                    Assert.That(view.transform.position, Is.EqualTo(stored.Transform.Position));
                    Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
                    Assert.That(view.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(originalMaterial));

                    settings.PositionSnapEnabled = false;
                    var freeMove = new ManipulationSession(initial, ManipulationMode.Move, ManipulationAxis.X,
                        Vector2.zero, Vector2.right, 100f, settings);
                    PieceData free = freeMove.Evaluate(Vector2.right * 36f);
                    Assert.That(lab.Apply(free), Is.True);
                    Assert.That(free.Transform.Position.x, Is.EqualTo(initial.Transform.Position.x + 0.36f).Within(0.0001f));

                    var rotate = new ManipulationSession(initial, ManipulationMode.Rotate, ManipulationAxis.Y,
                        Vector2.zero, Vector2.right, 1f, settings);
                    PieceData snappedRotation = rotate.Evaluate(Vector2.right * 16f);
                    Assert.That(lab.Apply(snappedRotation), Is.True);
                    Assert.That(Quaternion.Angle(view.transform.rotation, Quaternion.Euler(0f, 15f, 0f)), Is.LessThan(0.001f));
                    settings.RotationSnapEnabled = false;
                    var freeRotate = new ManipulationSession(initial, ManipulationMode.Rotate, ManipulationAxis.Y,
                        Vector2.zero, Vector2.right, 1f, settings);
                    PieceData freeRotation = freeRotate.Evaluate(Vector2.right * 16f);
                    Assert.That(lab.Apply(freeRotation), Is.True);
                    Assert.That(Quaternion.Angle(view.transform.rotation, Quaternion.Euler(0f, 8f, 0f)), Is.LessThan(0.001f));
                    Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
                    Assert.That(view.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(originalMaterial));
                    Assert.That(view.MaterialId, Is.EqualTo(initial.MaterialId));
                }
            }
            finally
            {
                Object.DestroyImmediate(labObject);
                Object.DestroyImmediate(material);
            }
        }
    }
}
