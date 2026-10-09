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
    public sealed class SelectedPieceDeletionPlayModeTests
    {
        [UnityTest]
        public IEnumerator BackspaceDeletesOnlyIdleSelectionAndHistoryRestoresWall()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var cameraObject = new GameObject("Deletion test camera");
            var labObject = new GameObject("Deletion test lab");
            labObject.SetActive(false);
            Keyboard keyboard = null;
            Mouse mouse = null;
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
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                keyboard.MakeCurrent();
                mouse.MakeCurrent();
                MethodInfo update = typeof(ConstructionLabInteraction).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo sessionField = typeof(ConstructionLabInteraction).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo gizmoField = typeof(ConstructionLabInteraction).GetField("gizmo", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(update, Is.Not.Null);
                Assert.That(sessionField, Is.Not.Null);
                Assert.That(gizmoField, Is.Not.Null);
                void Press(Key key)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    InputSystem.Update();
                    update.Invoke(interaction, null);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                }

                PieceId wallId = PieceId.Parse("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");
                Assert.That(lab.World.TryGet(wallId, out PieceData wall), Is.True);
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
                Assert.That(interaction.AddOpening(WallOpeningKind.Passage), Is.True);
                Assert.That(interaction.SelectedOpeningId, Is.Not.Null);

                Press(Key.Delete);
                Assert.That(lab.World.TryGet(wallId, out wall), Is.True, "Delete must keep the wall.");
                Assert.That(wall.Openings, Is.Empty);
                Assert.That(lab.Undo(), Is.True);
                Assert.That(interaction.CycleOpening(), Is.True);
                Assert.That(interaction.SelectedOpeningId, Is.Not.Null);
                int initialCount = lab.World.Count;

                interaction.BeginPlacement(PieceType.Block);
                Press(Key.Backspace);
                Assert.That(lab.World.Count, Is.EqualTo(initialCount));
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(wallId));
                Press(Key.Escape);
                Assert.That(interaction.Placement.Active, Is.False);

                sessionField.SetValue(interaction, new ManipulationSession(wall, ManipulationMode.Move,
                    ManipulationAxis.X, Vector2.zero, Vector2.right, 100f));
                Press(Key.Backspace);
                Assert.That(lab.World.Count, Is.EqualTo(initialCount));
                Assert.That(interaction.SelectedPieceId, Is.EqualTo(wallId));
                sessionField.SetValue(interaction, null);

                update.Invoke(interaction, null);
                var gizmo = (RuntimeGizmo)gizmoField.GetValue(interaction);
                Assert.That(gizmo.gameObject.activeSelf, Is.True);
                Press(Key.Backspace);
                Assert.That(lab.World.TryGet(wallId, out _), Is.False);
                Assert.That(lab.TryGetView(wallId, out _), Is.False);
                Assert.That(lab.World.Count, Is.EqualTo(initialCount - 1));
                Assert.That(interaction.SelectedPieceId, Is.Null);
                Assert.That(interaction.SelectedOpeningId, Is.Null);
                Assert.That(gizmo.gameObject.activeSelf, Is.False);
                Press(Key.Backspace);
                Assert.That(lab.World.Count, Is.EqualTo(initialCount - 1), "Backspace without selection is a no-op.");
                Assert.That(lab.Undo(), Is.True);
                Assert.That(lab.World.TryGet(wallId, out PieceData restored), Is.True);
                Assert.That(restored.Openings, Has.Count.EqualTo(1));
                Assert.That(lab.TryGetView(wallId, out _), Is.True);
                Assert.That(lab.World.Count, Is.EqualTo(initialCount));
                Assert.That(lab.World.SpatialIndex.IndexedPieceCount, Is.EqualTo(initialCount));
                Assert.That(interaction.SelectedPieceId, Is.Null, "Undo restores the piece without silently selecting it.");
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
