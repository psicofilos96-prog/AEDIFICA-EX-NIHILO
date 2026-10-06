using System.Collections;
using Aedifica.Construction;
using Aedifica.Interaction;
using Aedifica.Interaction.Camera;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class WallSlabLabPlayModeTests
    {
        [UnityTest]
        public IEnumerator LabTypesHaveViewsCollidersSelectionAndDimensionalRefresh()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var cameraObject = new GameObject("Wall Slab Test Camera");
            var labObject = new GameObject("Wall Slab Test Lab");
            labObject.SetActive(false);
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                var cityCamera = cameraObject.AddComponent<CityBuilderCamera>();
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                var interaction = labObject.AddComponent<ConstructionLabInteraction>();
                lab.ConfigureMaterial(material);
                interaction.Configure(camera, cityCamera);
                labObject.SetActive(true);
                yield return null;

                Assert.That(lab.World.Count, Is.GreaterThanOrEqualTo(4));
                bool block = false, wall = false, slab = false;
                foreach (PieceData piece in lab.World.Pieces)
                {
                    block |= piece.Type == PieceType.Block;
                    wall |= piece.Type == PieceType.Wall;
                    slab |= piece.Type == PieceType.Slab;
                    Assert.That(lab.TryGetView(piece.Id, out PieceView view), Is.True);
                    Assert.That(view.Id, Is.EqualTo(piece.Id));
                    Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                    BoxCollider collider = view.GetComponent<BoxCollider>();
                    Assert.That(collider.enabled, Is.True);
                    Assert.That(collider.center, Is.EqualTo(new Vector3(0f, piece.Dimensions.Y * 0.5f, 0f)));
                    Assert.That(collider.size, Is.EqualTo(new Vector3(piece.Dimensions.X, piece.Dimensions.Y, piece.Dimensions.Z)));

                    // Aim at each piece, independent of screen resolution.
                    Vector3 center = view.transform.position + Vector3.up * piece.Dimensions.Y * 0.5f;
                    camera.transform.position = center + new Vector3(0f, 10f, -3f);
                    camera.transform.LookAt(center);
                    Vector3 screen = camera.WorldToScreenPoint(center);
                    var pointer = new Vector2(screen.x, screen.y);
                    Assert.That(interaction.TryPickPieceAt(pointer, out PieceId picked), Is.True);
                    Assert.That(picked, Is.EqualTo(piece.Id));
                    interaction.PointerDown(pointer);
                    interaction.PointerUp(pointer);
                    Assert.That(interaction.SelectedPieceId, Is.EqualTo(piece.Id));
                }
                Assert.That(block && wall && slab, Is.True);

                PieceId wallId = PieceId.Parse("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
                Assert.That(lab.World.TryGet(wallId, out PieceData original), Is.True);
                Assert.That(lab.TryGetView(wallId, out PieceView wallView), Is.True);
                PieceData replacement = original.WithWallDimensions(new WallDimensions(6f, 4f, 0.4f));
                Assert.That(lab.Apply(replacement), Is.True);
                Assert.That(lab.World.TryGet(wallId, out PieceData current), Is.True);
                Assert.That(current, Is.SameAs(replacement));
                Assert.That(wallView.GetComponent<MeshFilter>().sharedMesh.bounds.size, Is.EqualTo(new Vector3(6f, 4f, 0.4f)));
                Assert.That(wallView.GetComponent<BoxCollider>().size, Is.EqualTo(new Vector3(6f, 4f, 0.4f)));
                Assert.That(wallView.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(labObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
            }
        }
    }
}
