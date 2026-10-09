using System.Collections;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class VisualChunkPlayModeTests
    {
        private static PieceData Block(int id, Vector3 position, float width = 2f) =>
            new PieceData(PieceId.Parse(id.ToString("x32")),
                new PieceTransform(position, Quaternion.identity), new BlockDimensions(width, 2f, 2f));

        [UnityTest]
        public IEnumerator CullingKeepsVisibleLongPieceAndLogicalEditabilityAcrossMovement()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var root = new GameObject("Chunk visual test");
            var cameraObject = new GameObject("Chunk test camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 3f, -20f);
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));
            camera.farClipPlane = 100f;
            var world = new ConstructionWorld();
            var manager = new VisualChunkManager(10f, 0.2f, root.transform);
            var views = new Dictionary<PieceId, PieceView>();
            try
            {
                PieceData near = Block(1, Vector3.zero);
                PieceData far = Block(2, new Vector3(200f, 0f, 0f));
                PieceData longPiece = Block(3, new Vector3(35f, 0f, 0f), 80f);
                foreach (PieceData piece in new[] { near, far, longPiece })
                {
                    Assert.That(world.Create(piece).Changed, Is.True);
                    var go = new GameObject("Piece " + piece.Id);
                    go.transform.SetParent(root.transform, false);
                    PieceView view = go.AddComponent<PieceView>();
                    view.Initialize(piece, material);
                    manager.Register(piece, view);
                    views.Add(piece.Id, view);
                    Assert.That(manager.Contains(piece.Id), Is.True);
                    Assert.That(world.TryGet(piece.Id, out PieceData actual) && actual.Id == view.Id, Is.True);
                }
                Assert.That(manager.CellsFor(longPiece.Id).Count, Is.GreaterThan(4));
                manager.SetCulling(true);
                manager.Evaluate(camera, 1f);
                Assert.That(manager.IsRendererEnabled(near.Id), Is.True);
                Assert.That(manager.IsRendererEnabled(longPiece.Id), Is.True,
                    "A long piece whose center is outside the frustum must stay visible.");
                Assert.That(manager.IsRendererEnabled(far.Id), Is.False);
                Assert.That(views[far.Id].GetComponent<BoxCollider>().enabled, Is.True,
                    "Hidden renderers retain colliders for explicit selection and editing.");
                Assert.That(world.SpatialIndex.Query(ConstructionChangeSet.WorldBounds(far)), Does.Contain(far.Id),
                    "Visual culling must not change logical spatial queries used by Snap.");

                PieceData moved = near.WithTransform(new PieceTransform(new Vector3(300f, 0f, 0f), Quaternion.identity));
                Assert.That(world.Update(near.Id, moved).Changed, Is.True);
                manager.Update(moved);
                manager.Evaluate(camera, 2f);
                Assert.That(manager.IsRendererEnabled(near.Id), Is.False);
                Assert.That(manager.Contains(near.Id), Is.True);
                Assert.That(world.TryGet(near.Id, out PieceData stored) && stored.Transform.Equals(moved.Transform), Is.True);
                Assert.That(views[near.Id].GetComponent<BoxCollider>().enabled, Is.True);

                PieceData returned = moved.WithTransform(near.Transform);
                Assert.That(world.Update(near.Id, returned).Changed, Is.True);
                manager.Update(returned);
                manager.Evaluate(camera, 3f);
                Assert.That(manager.IsRendererEnabled(near.Id), Is.True);
                Assert.That(world.Delete(far.Id).Changed, Is.True);
                Assert.That(manager.Remove(far.Id), Is.True);
                Assert.That(manager.Contains(far.Id), Is.False);
                Assert.That(manager.PieceCount, Is.EqualTo(world.Count));
                Assert.That(world.SpatialIndex.Query(ConstructionChangeSet.WorldBounds(returned)),
                    Does.Contain(near.Id));

                camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
                manager.Evaluate(camera, 3.1f);
                Assert.That(manager.IsRendererEnabled(near.Id), Is.True, "Short camera oscillation must retain visibility.");
                manager.Evaluate(camera, 3.3f);
                Assert.That(manager.IsRendererEnabled(near.Id), Is.False);
                camera.transform.LookAt(new Vector3(0f, 1f, 0f));
                manager.Evaluate(camera, 3.4f);
                Assert.That(manager.IsRendererEnabled(near.Id), Is.True);
            }
            finally
            {
                manager.Dispose();
                Object.Destroy(root);
                Object.Destroy(cameraObject);
                Object.Destroy(material);
            }
            yield return null;
            Assert.That(manager.PieceCount, Is.Zero);
            Assert.That(manager.ChunkCount, Is.Zero);
        }
    }
}
