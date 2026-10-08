using System.Collections;
using System.Reflection;
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
    public sealed class FreePiecePlacementPlayModeTests
    {
        [UnityTest]
        public IEnumerator WallPlacementUsesWorldViewSelectionAndExistingOpeningCommand()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var cameraObject = new GameObject("Placement test camera");
            var labObject = new GameObject("Placement test lab");
            labObject.SetActive(false);
            Keyboard keyboard = null;
            Mouse mouse = null;
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.pixelRect = new Rect(0f, 0f, 1000f, 800f);
                var controller = cameraObject.AddComponent<CityBuilderCamera>();
                var lab = labObject.AddComponent<ConstructionLabBlocks>();
                var interaction = labObject.AddComponent<ConstructionLabInteraction>();
                lab.ConfigureMaterial(material);
                interaction.Configure(camera, controller);
                labObject.SetActive(true);
                yield return null;
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                keyboard.MakeCurrent();
                mouse.MakeCurrent();
                MethodInfo update = typeof(ConstructionLabInteraction).GetMethod("Update",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(update, Is.Not.Null);
                camera.transform.position = new Vector3(30f, 20f, -30f);
                camera.transform.LookAt(new Vector3(30f, 0f, 0f));
                Vector2 pointer = new Vector2(500f, 400f);
                int originalCount = lab.World.Count;

                interaction.BeginPlacement(PieceType.Wall); // Same entry point used by the catalog button.
                Assert.That(interaction.Placement.Active, Is.True);
                camera.transform.LookAt(camera.transform.position + Vector3.up);
                Assert.That(interaction.Placement.UpdatePosition(pointer), Is.False,
                    "Sky without a reachable construction plane must not create a piece.");
                Assert.That(lab.World.Count, Is.EqualTo(originalCount));
                camera.transform.LookAt(new Vector3(30f, 0f, 0f));
                Assert.That(interaction.Placement.UpdatePosition(pointer), Is.True);
                Assert.That(interaction.Placement.PreviewObject.GetComponent<PieceView>(), Is.Null);
                Assert.That(interaction.Placement.PreviewObject.GetComponent<Collider>(), Is.Null);
                Assert.That(lab.World.Count, Is.EqualTo(originalCount));
                Mesh cachedPreviewMesh = interaction.Placement.PreviewObject.GetComponent<MeshFilter>().sharedMesh;
                interaction.Placement.Rotate(1);
                Assert.That(interaction.Placement.UpdatePosition(pointer + Vector2.right * 10f), Is.True);
                Assert.That(interaction.Placement.PreviewObject.GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(cachedPreviewMesh), "Moving or rotating the preview must reuse its mesh.");
                Assert.That(Quaternion.Angle(interaction.Placement.PreviewObject.transform.rotation,
                    Quaternion.Euler(0f, 15f, 0f)), Is.LessThan(0.01f));
                interaction.Snapping.PositionSnapEnabled = true;
                Assert.That(interaction.Placement.UpdatePosition(pointer + Vector2.right * 10f), Is.True);
                Assert.That(interaction.Placement.Position.x / interaction.Snapping.PositionIncrement,
                    Is.EqualTo(Mathf.Round(interaction.Placement.Position.x / interaction.Snapping.PositionIncrement)).Within(0.001f));

                Vector2 toolbarPointer = new Vector2(100f, Screen.height - 200f);
                Assert.That(interaction.Placement.IsToolbarPoint(toolbarPointer), Is.True);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = toolbarPointer }.WithButton(MouseButton.Left));
                InputSystem.Update();
                update.Invoke(interaction, null);
                Assert.That(lab.World.Count, Is.EqualTo(originalCount),
                    "A click on the catalog must not place a piece in the scene.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = toolbarPointer });
                InputSystem.Update();

                Vector2 firstPointer = pointer + Vector2.right * 10f;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = firstPointer }.WithButton(MouseButton.Left));
                InputSystem.Update();
                update.Invoke(interaction, null);
                Assert.That(lab.World.Count, Is.EqualTo(originalCount + 1));
                PieceId firstId = interaction.SelectedPieceId.Value;
                Assert.That(lab.World.TryGet(firstId, out PieceData first), Is.True);
                Assert.That(first.Type, Is.EqualTo(PieceType.Wall));
                Assert.That(lab.TryGetView(firstId, out PieceView firstView), Is.True);
                Assert.That(firstView.GetComponent<Collider>(), Is.Not.Null);
                Assert.That(interaction.Placement.Active, Is.True);

                InputSystem.QueueStateEvent(mouse, new MouseState { position = firstPointer });
                InputSystem.Update();
                Vector2 secondPointer = firstPointer + Vector2.right * 100f;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = secondPointer }.WithButton(MouseButton.Left));
                InputSystem.Update();
                update.Invoke(interaction, null);
                Assert.That(lab.World.Count, Is.EqualTo(originalCount + 2));
                Assert.That(interaction.SelectedPieceId.Value, Is.Not.EqualTo(firstId));

                InputSystem.QueueStateEvent(mouse, new MouseState { position = secondPointer });
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                update.Invoke(interaction, null);
                Assert.That(interaction.Placement.Active, Is.False);
                Assert.That(lab.World.Count, Is.EqualTo(originalCount + 2));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Insert));
                InputSystem.Update();
                update.Invoke(interaction, null);
                Assert.That(lab.World.TryGet(interaction.SelectedPieceId.Value, out PieceData createdWall), Is.True);
                Assert.That(createdWall.Openings.Count, Is.EqualTo(1));
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                Object.Destroy(labObject);
                Object.Destroy(cameraObject);
                Object.Destroy(material);
            }
        }
    }
}
