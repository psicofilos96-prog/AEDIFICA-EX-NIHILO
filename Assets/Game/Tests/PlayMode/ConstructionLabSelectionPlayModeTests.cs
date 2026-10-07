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
    public sealed class ConstructionLabSelectionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ControllerPicksAndSelectsRuntimeBlockThroughCameraRayAndCollider()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var cameraObject = new GameObject("Test Camera");
            var labObject = new GameObject("Test Construction Lab");
            labObject.SetActive(false);
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
                var cityCamera = cameraObject.AddComponent<CityBuilderCamera>();
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                var interaction = labObject.AddComponent<ConstructionLabInteraction>();
                lab.ConfigureMaterial(material);
                interaction.Configure(camera, cityCamera);
                labObject.SetActive(true);
                yield return null; // Allow Start to create the runtime gizmo.

                PieceId id = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
                Assert.That(lab.World.TryGet(id, out PieceData piece), Is.True);
                Assert.That(lab.TryGetView(id, out PieceView view), Is.True);
                BoxCollider collider = view.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.center, Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
                Assert.That(collider.size, Is.EqualTo(new Vector3(2f, 1f, 3f)));
                Vector3 screen = camera.WorldToScreenPoint(piece.Transform.Position + new Vector3(0f, 0.5f, 0f));
                var pointer = new Vector2(screen.x, screen.y);
                Assert.That(interaction.TryPickPieceAt(pointer, out PieceId picked), Is.True);
                Assert.That(picked, Is.EqualTo(id));
                interaction.PointerDown(pointer);
                interaction.PointerUp(pointer);
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(id));
                yield return null; // The interaction displays the selected piece's gizmo in Update.
                GameObject interactionGizmo = GameObject.Find("Construction Gizmo");
                Assert.That(interactionGizmo, Is.Not.Null);
                GizmoHandle moveHandle = null;
                foreach (GizmoHandle handle in interactionGizmo.GetComponentsInChildren<GizmoHandle>())
                    if (handle.Mode == ManipulationMode.Move && handle.Axis == ManipulationAxis.X) moveHandle = handle;
                Assert.That(moveHandle, Is.Not.Null);
                Vector3 handleScreen = camera.WorldToScreenPoint(moveHandle.GetComponent<Renderer>().bounds.center);
                var handlePointer = new Vector2(handleScreen.x, handleScreen.y);
                interaction.PointerDown(handlePointer);
                var sessionField = typeof(ConstructionLabInteraction).GetField("session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var panField = typeof(ConstructionLabInteraction).GetField("draggingPan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(sessionField.GetValue(interaction), Is.Not.Null);
                Assert.That(panField.GetValue(interaction), Is.False);
                interaction.PointerUp(handlePointer);
                var faceSession = new ManipulationSession(piece, ManipulationMode.Resize, ManipulationAxis.X,
                    Vector2.zero, Vector2.right, 100f, null, ResizeMode.Face, -1);
                PieceData resizedPiece = faceSession.Evaluate(new Vector2(100f, 0f));
                Assert.That(lab.Apply(resizedPiece), Is.True);
                Assert.That(lab.World.TryGet(id, out PieceData stored), Is.True);
                Assert.That(stored, Is.SameAs(resizedPiece));
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
                var snapSettings = new SnapSettings { EndpointSnapEnabled = true };
                var moveSession = new ManipulationSession(resizedPiece, ManipulationMode.Move, ManipulationAxis.X,
                    Vector2.zero, Vector2.right, 100f, snapSettings);
                PieceData nearbyTarget = new PieceData(PieceId.Parse("99999999999999999999999999999999"),
                    new PieceTransform(resizedPiece.Transform.Position + new Vector3(resizedPiece.Dimensions.X + 0.1f, 0f, 0f),
                        resizedPiece.Transform.Rotation), resizedPiece.Dimensions);
                PieceData geometricallySnapped = new SnapResolver().Resolve(resizedPiece, moveSession, snapSettings,
                    new[] { nearbyTarget });
                Assert.That(lab.Apply(geometricallySnapped), Is.True);
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
                var gizmoObject = new GameObject("Test Rotation Gizmo");
                try
                {
                    var gizmo = gizmoObject.AddComponent<RuntimeGizmo>();
                    gizmo.Initialize(camera, material);
                    gizmo.Show(piece, ManipulationMode.Rotate);
                    GizmoHandle rotate = gizmo.GetComponentInChildren<GizmoHandle>();
                    Assert.That(rotate, Is.Not.Null);
                    Assert.That(rotate.Mode, Is.EqualTo(ManipulationMode.Rotate));
                    Assert.That(rotate.Axis, Is.EqualTo(ManipulationAxis.Y));
                    Assert.That(rotate.gameObject.activeInHierarchy, Is.True);
                    Assert.That(rotate.GetComponent<Collider>().enabled, Is.True);
                    GizmoHandle[] visibleHandles = gizmo.GetComponentsInChildren<GizmoHandle>();
                    Assert.That(visibleHandles.Length, Is.EqualTo(25));
                    foreach (GizmoHandle visible in visibleHandles)
                    {
                        Assert.That(visible.Mode, Is.EqualTo(ManipulationMode.Rotate));
                        Assert.That(visible.Axis, Is.EqualTo(ManipulationAxis.Y));
                        Assert.That(visible.GetComponent<Collider>().enabled, Is.True);
                    }
                    gizmo.Show(piece, ManipulationMode.Resize, ResizeMode.Face);
                    GizmoHandle[] faceHandles = gizmo.GetComponentsInChildren<GizmoHandle>();
                    Assert.That(faceHandles.Length, Is.EqualTo(6));
                    foreach (ManipulationAxis axis in new[] { ManipulationAxis.X, ManipulationAxis.Y, ManipulationAxis.Z })
                    foreach (int sign in new[] { -1, 1 })
                    {
                        int count = 0;
                        foreach (GizmoHandle face in faceHandles)
                            if (face.Axis == axis && face.FaceSign == sign)
                            {
                                count++;
                                Assert.That(face.Mode, Is.EqualTo(ManipulationMode.Resize));
                                Assert.That(face.GetComponent<Collider>().enabled, Is.True);
                            }
                        Assert.That(count, Is.EqualTo(1));
                    }
                    foreach (PieceType type in new[] { PieceType.Block, PieceType.Wall, PieceType.Slab })
                    {
                        PieceData typedPiece = type == PieceType.Block ? piece
                            : type == PieceType.Wall
                                ? new PieceData(id, piece.Transform, new WallDimensions(3f, 2f, 0.2f))
                                : new PieceData(id, piece.Transform, new SlabDimensions(3f, 0.2f, 2f));
                        gizmo.Show(typedPiece, ManipulationMode.Resize, ResizeMode.Face);
                        foreach (GizmoHandle face in gizmo.GetComponentsInChildren<GizmoHandle>())
                        {
                            string expectedDimension = type == PieceType.Wall
                                ? (face.Axis == ManipulationAxis.X ? "Length" : face.Axis == ManipulationAxis.Y ? "Height" : "Thickness")
                                : type == PieceType.Slab
                                    ? (face.Axis == ManipulationAxis.X ? "Width" : face.Axis == ManipulationAxis.Y ? "Thickness" : "Depth")
                                    : (face.Axis == ManipulationAxis.X ? "Width" : face.Axis == ManipulationAxis.Y ? "Height" : "Depth");
                            Assert.That(face.SemanticDimension, Is.EqualTo(expectedDimension));
                        }
                    }
                    gizmo.Show(piece, ManipulationMode.Resize, ResizeMode.Center);
                    Assert.That(gizmo.GetComponentsInChildren<GizmoHandle>().Length, Is.EqualTo(3));
                }
                finally { Object.DestroyImmediate(gizmoObject); }
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
