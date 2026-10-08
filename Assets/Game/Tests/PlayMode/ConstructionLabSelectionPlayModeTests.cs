using System.Collections;
using Aedifica.Construction;
using Aedifica.Interaction;
using Aedifica.Interaction.Camera;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class ConstructionLabSelectionPlayModeTests
    {
        [UnityTest]
        public IEnumerator HomeFramesSelectedRearDomeAndDoesNothingWithoutSelection()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var cameraObject = new GameObject("Home framing test camera");
            var labObject = new GameObject("Home framing test lab");
            labObject.SetActive(false);
            Keyboard testKeyboard = null;
            Mouse testMouse = null;
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
                yield return null;

                testKeyboard = InputSystem.AddDevice<Keyboard>();
                testMouse = InputSystem.AddDevice<Mouse>();
                testKeyboard.MakeCurrent();
                testMouse.MakeCurrent();
                Vector3 originalPosition = camera.transform.position;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Home));
                yield return null;
                Assert.That(camera.transform.position, Is.EqualTo(originalPosition), "Home without selection must leave the camera unchanged.");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.C));
                yield return null;
                Assert.That(camera.transform.position, Is.EqualTo(originalPosition), "C without selection must leave the camera unchanged.");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;

                PieceId domeId = PieceId.Parse("50000000000000000000000000000003");
                Assert.That(lab.TryGetView(domeId, out PieceView dome), Is.True);
                Bounds bounds = dome.GetComponent<MeshRenderer>().bounds;
                camera.transform.position = bounds.center + new Vector3(0f, 7f, -12f);
                camera.transform.LookAt(bounds.center);
                Vector3 screen = camera.WorldToScreenPoint(bounds.center);
                var pointer = new Vector2(screen.x, screen.y);
                Assert.That(interaction.TryPickPieceAt(pointer, out PieceId picked), Is.True);
                Assert.That(picked, Is.EqualTo(domeId));
                interaction.PointerDown(pointer);
                interaction.PointerUp(pointer);
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(domeId));

                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.C));
                yield return null; // Exercise Input System and both MonoBehaviour.Update methods in their real order.
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.6f);
                Vector3 centerOnScreen = camera.WorldToViewportPoint(bounds.center);
                Assert.That(centerOnScreen.x, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(centerOnScreen.y, Is.EqualTo(0.5f).Within(0.01f));
                foreach (int x in new[] { -1, 1 })
                foreach (int y in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    Vector3 projected = camera.WorldToViewportPoint(corner);
                    Assert.That(projected.z, Is.GreaterThan(camera.nearClipPlane));
                    Assert.That(projected.x, Is.InRange(0.08f, 0.92f));
                    Assert.That(projected.y, Is.InRange(0.08f, 0.92f));
                }

                Assert.That(cityCamera.FrameBounds(new Bounds(Vector3.zero, Vector3.one)), Is.True);
                yield return new WaitForSecondsRealtime(0.6f);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Home));
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.That(camera.WorldToViewportPoint(bounds.center).x, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(camera.WorldToViewportPoint(bounds.center).y, Is.EqualTo(0.5f).Within(0.01f));
                float distanceBeforeWheel = Vector3.Distance(camera.transform.position, bounds.center);
                InputSystem.QueueStateEvent(testMouse, new MouseState { scroll = Vector2.up });
                yield return null;
                InputSystem.QueueStateEvent(testMouse, new MouseState());
                yield return new WaitForSecondsRealtime(0.3f);
                float distanceAfterWheel = Vector3.Distance(camera.transform.position, bounds.center);
                Assert.That(distanceAfterWheel, Is.LessThan(distanceBeforeWheel));
                Assert.That(distanceAfterWheel, Is.GreaterThan(0.5f));
            }
            finally
            {
                if (testMouse != null) InputSystem.RemoveDevice(testMouse);
                if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
                Object.DestroyImmediate(labObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator ScrollApproachesRearDomeAndFrontWallWithoutSelection()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var cameraObject = new GameObject("Cursor zoom camera");
            var labObject = new GameObject("Cursor zoom lab");
            labObject.SetActive(false);
            Mouse testMouse = null;
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
                var controller = cameraObject.AddComponent<CityBuilderCamera>();
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                var interaction = labObject.AddComponent<ConstructionLabInteraction>();
                lab.ConfigureMaterial(material);
                interaction.Configure(camera, controller);
                labObject.SetActive(true);
                yield return null;
                testMouse = InputSystem.AddDevice<Mouse>();
                testMouse.MakeCurrent();

                foreach (string idText in new[] { "50000000000000000000000000000003", "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" })
                {
                    PieceId id = PieceId.Parse(idText);
                    Assert.That(lab.TryGetView(id, out PieceView view), Is.True);
                    Bounds bounds = view.GetComponent<MeshRenderer>().bounds;
                    Assert.That(interaction.SelectedPieceId, Is.Null);
                    Assert.That(controller.FrameBounds(bounds), Is.True);
                    yield return new WaitForSecondsRealtime(0.6f);
                    Vector3 screen = camera.WorldToScreenPoint(bounds.center);
                    Vector2 pointer = new Vector2(screen.x, screen.y);
                    Physics.SyncTransforms();
                    Assert.That(Physics.Raycast(camera.ScreenPointToRay(pointer), out RaycastHit hit, 1000f), Is.True,
                        $"Expected a cursor target for {idText}.");
                    float before = Vector3.Distance(camera.transform.position, hit.point);
                    for (int i = 0; i < 8; i++)
                    {
                        InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointer, scroll = Vector2.up });
                        yield return null;
                        InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointer });
                        yield return null;
                    }
                    yield return new WaitForSecondsRealtime(0.4f);
                    float after = Vector3.Distance(camera.transform.position, hit.point);
                    Assert.That(after, Is.LessThan(before * 0.5f), $"Cursor zoom did not approach {idText}.");
                    Assert.That(interaction.SelectedPieceId, Is.Null);
                    Assert.That(float.IsNaN(camera.transform.position.x), Is.False);
                    InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointer, scroll = Vector2.down });
                    yield return null;
                    InputSystem.QueueStateEvent(testMouse, new MouseState { position = pointer });
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }
            finally
            {
                if (testMouse != null) InputSystem.RemoveDevice(testMouse);
                Object.DestroyImmediate(labObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator ScrollOnEmptyConstructionPlaneRemainsBounded()
        {
            var cameraObject = new GameObject("Empty zoom test camera");
            Mouse testMouse = null;
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
                var controller = cameraObject.AddComponent<CityBuilderCamera>();
                testMouse = InputSystem.AddDevice<Mouse>();
                testMouse.MakeCurrent();
                yield return null;
                var pointer = new Vector2(400f, 300f);
                Ray groundRay = camera.ScreenPointToRay(pointer);
                Assert.That(Physics.Raycast(groundRay, 1000f), Is.False);
                Assert.That(new Plane(Vector3.up, Vector3.zero).Raycast(groundRay, out float planeDistance), Is.True);
                Assert.That(planeDistance, Is.InRange(0f, 100f), "The bounded construction-plane fallback must be reachable.");
                Vector3 before = camera.transform.position;
                var update = typeof(CityBuilderCamera).GetMethod("Update",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(update, Is.Not.Null);
                void Scroll(Vector2 position, float amount)
                {
                    InputSystem.QueueStateEvent(testMouse, new MouseState
                        { position = position, scroll = new Vector2(0f, amount) });
                    InputSystem.Update();
                    Assert.That(Mouse.current, Is.SameAs(testMouse));
                    Assert.That(testMouse.scroll.ReadValue().y, Is.EqualTo(amount),
                        "The transient scroll event must be present when the camera reads input.");
                    update.Invoke(controller, null); // Exercise the real input, fallback, motion and transform path.
                    InputSystem.QueueStateEvent(testMouse, new MouseState { position = position });
                    InputSystem.Update();
                }
                Scroll(pointer, 1f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(Vector3.Distance(before, camera.transform.position), Is.GreaterThan(0.1f));
                Assert.That(Vector3.Distance(before, camera.transform.position), Is.LessThan(20f));
                Assert.That(float.IsNaN(camera.transform.position.x), Is.False);
                Vector3 afterApproach = camera.transform.position;
                Scroll(pointer, -1f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(Vector3.Distance(camera.transform.position, before),
                    Is.LessThan(Vector3.Distance(afterApproach, before)),
                    "Scrolling out over empty ground must recover the wider view.");

                camera.fieldOfView = 100f;
                var skyPointer = new Vector2(400f, 599f);
                Ray skyRay = camera.ScreenPointToRay(skyPointer);
                Assert.That(Physics.Raycast(skyRay, 1000f), Is.False);
                Assert.That(new Plane(Vector3.up, Vector3.zero).Raycast(skyRay, out _), Is.False,
                    "This ray must use orbital zoom because it points above the construction plane.");
                Vector3 beforeSky = camera.transform.position;
                Scroll(skyPointer, 1f);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(Vector3.Distance(beforeSky, camera.transform.position), Is.GreaterThan(0.1f));
                Assert.That(Vector3.Distance(beforeSky, camera.transform.position), Is.LessThan(20f));
            }
            finally
            {
                if (testMouse != null) InputSystem.RemoveDevice(testMouse);
                Object.DestroyImmediate(cameraObject);
            }
        }

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
                int negativeResizeHandles = 0;
                foreach (GizmoHandle handle in interactionGizmo.GetComponentsInChildren<GizmoHandle>(true))
                    if (handle.Mode == ManipulationMode.Resize && handle.FaceSign == -1)
                    {
                        negativeResizeHandles++;
                        Assert.That(handle.transform.parent, Is.EqualTo(interactionGizmo.transform));
                    }
                Assert.That(negativeResizeHandles, Is.EqualTo(3), "ConstructionLabInteraction must create all negative face handles.");
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
                Keyboard testKeyboard = InputSystem.AddDevice<Keyboard>();
                Mouse testMouse = InputSystem.AddDevice<Mouse>();
                try
                {
                    testKeyboard.MakeCurrent();
                    testMouse.MakeCurrent();
                    var update = typeof(ConstructionLabInteraction).GetMethod("Update",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.That(update, Is.Not.Null);
                    void Press(Key key)
                    {
                        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
                        InputSystem.Update();
                        update.Invoke(interaction, null);
                        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                        InputSystem.Update();
                    }
                    Press(Key.Digit3);
                    Press(Key.F);
                    Assert.That(interaction.Mode, Is.EqualTo(ManipulationMode.Resize));
                    int visibleFaceHandles = 0;
                    foreach (GizmoHandle handle in interactionGizmo.GetComponentsInChildren<GizmoHandle>())
                        if (handle.Mode == ManipulationMode.Resize) visibleFaceHandles++;
                    Assert.That(visibleFaceHandles, Is.EqualTo(6), "F must expose all six face handles after 3 selects Resize.");
                    Press(Key.T);
                    Press(Key.H);
                    Press(Key.P);
                    Press(Key.R);
                    Press(Key.G);
                    Assert.That(interaction.Snapping.SurfaceSnapEnabled, Is.True);
                    Assert.That(interaction.Snapping.EdgeSnapEnabled, Is.True);
                    Assert.That(interaction.Snapping.EndpointSnapEnabled, Is.True);
                    Assert.That(interaction.Snapping.RotationSnapEnabled, Is.True);
                    Assert.That(interaction.Snapping.PositionSnapEnabled, Is.True);
                    Press(Key.R);
                    Press(Key.F);
                    Assert.That(interaction.Snapping.RotationSnapEnabled, Is.False,
                        "R must toggle independently after T/H/P.");
                    Assert.That(interactionGizmo.GetComponentsInChildren<GizmoHandle>(),
                        Has.Length.EqualTo(3), "F must restore bilateral resize after T/H/P.");
                }
                finally
                {
                    InputSystem.RemoveDevice(testMouse);
                    InputSystem.RemoveDevice(testKeyboard);
                }
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
