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
