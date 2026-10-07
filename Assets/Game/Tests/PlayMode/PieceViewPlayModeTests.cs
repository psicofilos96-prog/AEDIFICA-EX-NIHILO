using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PieceViewPlayModeTests
    {
        [TestCase(PieceType.Stair)]
        [TestCase(PieceType.Ramp)]
        public void CirculationViewUsesOneColliderAndRebuildsOnlyForDimensions(PieceType type)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var viewObject = new GameObject("Circulation View Test");
            try
            {
                PieceId id = PieceId.Parse("40000000000000000000000000000013");
                var pose = new PieceTransform(Vector3.zero, Quaternion.identity);
                PieceData original = type == PieceType.Stair
                    ? new PieceData(id, pose, new StairDimensions(2f, 2f, 3f, 10))
                    : new PieceData(id, pose, new RampDimensions(2f, 2f, 4f, 0.2f));
                PieceView view = viewObject.AddComponent<PieceView>();
                view.Initialize(original, material);
                Mesh originalMesh = viewObject.GetComponent<MeshFilter>().sharedMesh;
                MeshCollider collider = viewObject.GetComponent<MeshCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.sharedMesh, Is.SameAs(originalMesh));
                Assert.That(viewObject.GetComponent<BoxCollider>().enabled, Is.False);
                Assert.That(viewObject.transform.localScale, Is.EqualTo(Vector3.one));
                view.Refresh(original.WithMaterial(LabMaterialIds.Stone));
                Assert.That(viewObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
                PieceData resized = type == PieceType.Stair ? original.WithStepCount(11)
                    : original.WithRampDimensions(new RampDimensions(2f, 2.5f, 4f, 0.2f));
                view.Refresh(resized);
                Assert.That(viewObject.GetComponent<MeshFilter>().sharedMesh, Is.Not.SameAs(originalMesh));
                Assert.That(collider.sharedMesh, Is.SameAs(viewObject.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(viewObject.GetComponents<MeshCollider>().Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(material);
            }
        }

        [TestCase(PieceType.ShedRoof)]
        [TestCase(PieceType.GableRoof)]
        [TestCase(PieceType.HipRoof)]
        public void SlopedRoofViewCreatesMeshColliderBeforeRefresh(PieceType type)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var gameObject = new GameObject("Test Sloped Roof View");
            try
            {
                PieceId id = PieceId.Parse("cccccccccccccccccccccccccccccccc");
                var transform = new PieceTransform(Vector3.zero, Quaternion.identity);
                PieceData piece;
                switch (type)
                {
                    case PieceType.ShedRoof: piece = new PieceData(id, transform, new ShedRoofDimensions(4f, 3f, 0.2f, 1f)); break;
                    case PieceType.GableRoof: piece = new PieceData(id, transform, new GableRoofDimensions(4f, 3f, 0.2f, 1f)); break;
                    default: piece = new PieceData(id, transform, new HipRoofDimensions(4f, 3f, 0.2f, 1f)); break;
                }
                PieceView view = gameObject.AddComponent<PieceView>();
                view.Initialize(piece, material);
                MeshCollider roofCollider = gameObject.GetComponent<MeshCollider>();
                Mesh mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(roofCollider, Is.Not.Null);
                Assert.That(roofCollider.enabled, Is.True);
                Assert.That(roofCollider.sharedMesh, Is.SameAs(mesh));
                Assert.That(gameObject.GetComponent<BoxCollider>().enabled, Is.False);
                Assert.That(gameObject.transform.localScale, Is.EqualTo(Vector3.one));
                view.Refresh(piece.WithMaterial(LabMaterialIds.Stone));
                Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(roofCollider.sharedMesh, Is.SameAs(mesh));
                view.Refresh(piece.WithRise(1.2f));
                Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.Not.SameAs(mesh));
                Assert.That(roofCollider.sharedMesh, Is.SameAs(gameObject.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(gameObject.GetComponents<MeshCollider>().Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(material);
            }
        }

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
