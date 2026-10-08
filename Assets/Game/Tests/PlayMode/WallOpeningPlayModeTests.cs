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
    public sealed class WallOpeningPlayModeTests
    {
        [UnityTest]
        public IEnumerator KeyboardEditingUpdatesWallMeshColliderWithoutChangingSelectionOrCamera()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var cameraObject = new GameObject("Opening test camera");
            var labObject = new GameObject("Opening test lab");
            labObject.SetActive(false);
            Keyboard keyboard = null;
            Mouse mouse = null;
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
                keyboard = InputSystem.AddDevice<Keyboard>();
                keyboard.MakeCurrent();
                mouse = InputSystem.AddDevice<Mouse>();
                mouse.MakeCurrent();
                var update = typeof(ConstructionLabInteraction).GetMethod("Update",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(update, Is.Not.Null);
                void Press(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                    InputSystem.Update();
                    Assert.That(Keyboard.current, Is.SameAs(keyboard));
                    update.Invoke(interaction, null);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                }
                PieceId wallId = PieceId.Parse("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
                Assert.That(lab.World.TryGet(wallId, out PieceData wall), Is.True);
                Assert.That(lab.TryGetView(wallId, out PieceView view), Is.True);
                Assert.That(view.GetComponent<BoxCollider>().enabled, Is.True);
                Vector3 solidPoint = wall.Transform.Position + new Vector3(-3f, 1f, 0f);
                camera.transform.position = solidPoint + Vector3.back * 8f;
                camera.transform.LookAt(solidPoint);
                Vector3 screen = camera.WorldToScreenPoint(solidPoint);
                Vector2 pointer = new Vector2(screen.x, screen.y);
                Assert.That(interaction.TryPickPieceAt(pointer, out PieceId picked), Is.True);
                Assert.That(picked, Is.EqualTo(wallId));
                interaction.PointerDown(pointer);
                interaction.PointerUp(pointer);
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(wallId));
                yield return null; // Let the camera restore its authoritative motion transform.
                Vector3 cameraBefore = camera.transform.position;

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Insert));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings.Count, Is.EqualTo(1));
                Assert.That(interaction.SelectedOpeningId, Is.EqualTo(wall.Openings[0].Id));
                Assert.That(view.GetComponent<BoxCollider>().enabled, Is.False);
                MeshCollider collider = view.GetComponent<MeshCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.sharedMesh, Is.SameAs(view.GetComponent<MeshFilter>().sharedMesh));
                Physics.SyncTransforms();
                Assert.That(HitsWall(view, wall, wall.Openings[0].Left + 0.5f, 1f), Is.False,
                    "The ray must cross the passage without hitting an invisible wall face.");
                Assert.That(HitsWall(view, wall, 0.4f, 1f), Is.True);
                Assert.That(Vector3.Distance(cameraBefore, camera.transform.position), Is.LessThan(0.01f));
                camera.transform.position = solidPoint + Vector3.back * 8f;
                camera.transform.LookAt(solidPoint);
                screen = camera.WorldToScreenPoint(solidPoint);
                Assert.That(interaction.TryPickPieceAt(new Vector2(screen.x, screen.y), out picked), Is.True);
                Assert.That(picked, Is.EqualTo(wallId), "Solid wall must remain selectable through its mesh collider.");
                yield return null;
                Mesh meshBeforePose = collider.sharedMesh;
                PieceData rotated = wall.WithTransform(new PieceTransform(wall.Transform.Position + Vector3.right,
                    Quaternion.Euler(0f, 45f, 0f)));
                Assert.That(lab.Apply(rotated), Is.True);
                Assert.That(collider.sharedMesh, Is.SameAs(meshBeforePose));
                Assert.That(lab.Apply(wall), Is.True);
                Assert.That(collider.sharedMesh, Is.SameAs(meshBeforePose));

                Press(Key.J);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings[0].Left, Is.EqualTo(3.3f).Within(0.0001f));
                Press(Key.L);
                Press(Key.I);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings[0].Bottom, Is.EqualTo(0.1f).Within(0.0001f));
                Press(Key.K);
                Press(Key.K); // The second descent is rejected at the floor, with feedback.
                Assert.That(interaction.LastOpeningFeedback, Does.Contain("piso"));
                Press(Key.U);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings[0].Width, Is.EqualTo(1.1f).Within(0.0001f));
                Press(Key.O);
                Press(Key.N);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings[0].Height, Is.EqualTo(1.9f).Within(0.0001f));
                Press(Key.B);
                Press(Key.L);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings[0].Left, Is.EqualTo(3.5f).Within(0.0001f));
                Assert.That(wall.Openings[0].Bottom, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(wall.Openings[0].Height, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(wallId));
                Assert.That(interaction.SelectedOpeningId, Is.EqualTo(wall.Openings[0].Id));
                Mesh beforeResize = collider.sharedMesh;
                Press(Key.O);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings[0].Width, Is.EqualTo(1.3f).Within(0.0001f));
                Assert.That(collider.sharedMesh, Is.Not.SameAs(beforeResize));

                Press(Key.LeftShift, Key.Insert);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings.Count, Is.EqualTo(2));
                Assert.That(interaction.SelectedOpeningId, Is.EqualTo(wall.Openings[1].Id));
                float windowLeftBeforeHold = wall.Openings[1].Left;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.L));
                yield return new WaitForSecondsRealtime(0.55f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings[1].Left, Is.GreaterThan(windowLeftBeforeHold + 0.15f),
                    "Holding L must repeat after the first precise 0.1 m step.");
                WallOpening window = wall.Openings[1];
                Vector3 windowCenter = wall.Transform.Position + wall.Transform.Rotation *
                    new Vector3(window.Left + window.Width * 0.5f - wall.Dimensions.X * 0.5f,
                        window.Bottom + window.Height * 0.5f, 0f);
                camera.transform.position = windowCenter + wall.Transform.Rotation * Vector3.back * 8f;
                camera.transform.LookAt(windowCenter);
                screen = camera.WorldToScreenPoint(windowCenter);
                pointer = new Vector2(screen.x, screen.y);
                interaction.PointerDown(pointer);
                interaction.PointerUp(pointer);
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(wallId),
                    "Clicking an empty opening must keep its host wall selected.");
                Assert.That(interaction.SelectedOpeningId, Is.EqualTo(window.Id));
                Assert.That(interaction.LastOpeningFeedback, Does.Contain("Abertura selecionada"));
                yield return null;
                Press(Key.Tab);
                Assert.That(interaction.SelectedOpeningId, Is.EqualTo(wall.Openings[0].Id));

                Press(Key.Delete);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings.Count, Is.EqualTo(1));
                Assert.That(wall.Openings[0].Kind, Is.EqualTo(WallOpeningKind.Window));
                Assert.That(interaction.CycleOpening(), Is.True);
                Assert.That(interaction.RemoveSelectedOpening(), Is.True);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True);
                Assert.That(wall.Openings, Is.Empty);
                Assert.That(view.GetComponent<BoxCollider>().enabled, Is.True);
                Assert.That(collider.enabled, Is.False);
                Physics.SyncTransforms();
                Assert.That(HitsWall(view, wall, 0.8f, 1f), Is.True);

                PieceId blockId = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
                Assert.That(lab.World.TryGet(blockId, out PieceData block), Is.True);
                Vector3 blockCenter = block.Transform.Position + Vector3.up * 0.5f;
                camera.transform.position = blockCenter + Vector3.back * 8f;
                camera.transform.LookAt(blockCenter);
                screen = camera.WorldToScreenPoint(blockCenter);
                pointer = new Vector2(screen.x, screen.y);
                interaction.PointerDown(pointer);
                interaction.PointerUp(pointer);
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(blockId));
                Press(Key.Insert);
                Assert.That(interaction.LastOpeningFeedback, Does.Contain("apenas em Wall"));
                Assert.That(lab.World.TryGet(blockId, out block), Is.True);
                Assert.That(block.Openings, Is.Empty);
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                Object.DestroyImmediate(labObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
            }
        }

        private static bool HitsWall(PieceView view, PieceData wall, float localLeft, float y)
        {
            Vector3 center = wall.Transform.Position + wall.Transform.Rotation *
                new Vector3(localLeft - wall.Dimensions.X * 0.5f, y, 0f);
            Ray ray = new Ray(center + wall.Transform.Rotation * Vector3.back * 2f,
                wall.Transform.Rotation * Vector3.forward);
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 4f))
                if (hit.collider.gameObject == view.gameObject) return true;
            return false;
        }
    }
}
