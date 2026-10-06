using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PieceViewPlayModeTests
    {
        [Test]
        public void ViewRebuildsFromReplacementDataWithUnitScaleAndDerivedCollider()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var gameObject = new GameObject("Test Piece View");
            try
            {
                PieceId id = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
                var initial = new PieceData(id, new PieceTransform(Vector3.zero, Quaternion.identity),
                    new BlockDimensions(2f, 1f, 3f));
                PieceView view = gameObject.AddComponent<PieceView>();
                view.Initialize(initial, material);
                Mesh originalMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
                var world = new ConstructionWorld();
                world.Add(initial);
                var moved = initial.WithTransform(new PieceTransform(new Vector3(4f, 2f, 1f), Quaternion.Euler(0f, 30f, 0f)));
                Assert.That(world.Replace(id, moved), Is.True);
                Assert.That(world.TryGet(id, out PieceData current), Is.True);
                view.Refresh(current);
                Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
                var replacement = moved.WithBlockDimensions(new BlockDimensions(4f, 5f, 2f));
                Assert.That(world.Replace(id, replacement), Is.True);
                Assert.That(world.TryGet(id, out current), Is.True);
                view.Refresh(current);
                BoxCollider collider = gameObject.GetComponent<BoxCollider>();
                Assert.That(view.Id, Is.EqualTo(id));
                Assert.That(gameObject.transform.position, Is.EqualTo(replacement.Transform.Position));
                Assert.That(Quaternion.Angle(gameObject.transform.rotation, replacement.Transform.Rotation), Is.LessThan(0.001f));
                Assert.That(gameObject.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.Not.SameAs(originalMesh));
                Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size, Is.EqualTo(new Vector3(4f, 5f, 2f)));
                Assert.That(collider.center, Is.EqualTo(new Vector3(0f, 2.5f, 0f)));
                Assert.That(collider.size, Is.EqualTo(new Vector3(4f, 5f, 2f)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(material);
            }
        }
    }
}
