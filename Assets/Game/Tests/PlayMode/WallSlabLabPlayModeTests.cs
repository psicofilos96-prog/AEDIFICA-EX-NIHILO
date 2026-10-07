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

                Assert.That(lab.World.Count, Is.GreaterThanOrEqualTo(14));
                bool block = false, wall = false, slab = false, column = false, beam = false, parapet = false;
                bool flatRoof = false, shedRoof = false, gableRoof = false, hipRoof = false;
                foreach (PieceData piece in lab.World.Pieces)
                {
                    block |= piece.Type == PieceType.Block;
                    wall |= piece.Type == PieceType.Wall;
                    slab |= piece.Type == PieceType.Slab;
                    column |= piece.Type == PieceType.Column;
                    beam |= piece.Type == PieceType.Beam;
                    parapet |= piece.Type == PieceType.Parapet;
                    flatRoof |= piece.Type == PieceType.FlatRoof;
                    shedRoof |= piece.Type == PieceType.ShedRoof;
                    gableRoof |= piece.Type == PieceType.GableRoof;
                    hipRoof |= piece.Type == PieceType.HipRoof;
                    Assert.That(lab.TryGetView(piece.Id, out PieceView view), Is.True);
                    Assert.That(view.Id, Is.EqualTo(piece.Id));
                    Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(view.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                    BoxCollider collider = view.GetComponent<BoxCollider>();
                    Assert.That(collider.enabled, Is.EqualTo(!piece.Dimensions.IsSlopedRoof));
                    Assert.That(collider.center, Is.EqualTo(new Vector3(0f, piece.Dimensions.Y * 0.5f, 0f)));
                    Assert.That(collider.size, Is.EqualTo(new Vector3(piece.Dimensions.X, piece.Dimensions.Y, piece.Dimensions.Z)));
                    if (piece.Dimensions.IsSlopedRoof)
                    {
                        MeshCollider roofCollider = view.GetComponent<MeshCollider>();
                        Assert.That(roofCollider, Is.Not.Null);
                        Assert.That(roofCollider.enabled, Is.True);
                        Assert.That(roofCollider.sharedMesh, Is.SameAs(view.GetComponent<MeshFilter>().sharedMesh));
                    }

                    // Aim at each piece, independent of screen resolution.
                    Vector3 center = view.transform.position + Vector3.up * piece.Dimensions.Y * 0.5f;
                    camera.transform.position = center + (piece.Type == PieceType.Column
                        ? new Vector3(0f, 1f, -10f) : new Vector3(0f, 10f, -3f));
                    camera.transform.LookAt(center);
                    Vector3 screen = camera.WorldToScreenPoint(center);
                    var pointer = new Vector2(screen.x, screen.y);
                    Assert.That(interaction.TryPickPieceAt(pointer, out PieceId picked), Is.True);
                    Assert.That(picked, Is.EqualTo(piece.Id));
                    interaction.PointerDown(pointer);
                    interaction.PointerUp(pointer);
                    Assert.That(interaction.SelectedPieceId, Is.EqualTo(piece.Id));
                }
                Assert.That(block && wall && slab && column && beam && parapet &&
                    flatRoof && shedRoof && gableRoof && hipRoof, Is.True);

                PieceId hipId = PieceId.Parse("20000000000000000000000000000004");
                Assert.That(lab.World.TryGet(hipId, out PieceData hipBefore), Is.True);
                Assert.That(lab.TryGetView(hipId, out PieceView hipView), Is.True);
                Vector3 hipCenter = hipView.transform.position + Vector3.up * hipBefore.Dimensions.Y * 0.5f;
                camera.transform.position = hipCenter + new Vector3(0f, 10f, -3f);
                camera.transform.LookAt(hipCenter);
                Vector3 hipScreen = camera.WorldToScreenPoint(hipCenter);
                Vector2 hipPointer = new Vector2(hipScreen.x, hipScreen.y);
                interaction.PointerDown(hipPointer);
                interaction.PointerUp(hipPointer);
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(hipId));
                Mesh oldRoofMesh = hipView.GetComponent<MeshFilter>().sharedMesh;
                var adjustRise = typeof(ConstructionLabInteraction).GetMethod("AdjustSelectedRoofRise",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(adjustRise, Is.Not.Null);
                adjustRise.Invoke(interaction, new object[] { 0.1f });
                Assert.That(lab.World.TryGet(hipId, out PieceData hipAfter), Is.True);
                Assert.That(hipAfter.Dimensions.Rise, Is.EqualTo(hipBefore.Dimensions.Rise + 0.1f).Within(0.00001f));
                Assert.That(hipView.GetComponent<MeshFilter>().sharedMesh, Is.Not.SameAs(oldRoofMesh));
                Assert.That(hipView.GetComponent<MeshCollider>().sharedMesh,
                    Is.SameAs(hipView.GetComponent<MeshFilter>().sharedMesh));
                Assert.That(hipView.transform.localScale, Is.EqualTo(Vector3.one));

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
