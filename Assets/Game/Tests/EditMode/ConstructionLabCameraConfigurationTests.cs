using System.Collections.Generic;
using Aedifica.Interaction;
using Aedifica.Interaction.Camera;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aedifica.Tests.EditMode
{
    public sealed class ConstructionLabCameraConfigurationTests
    {
        [Test]
        public void VersionedConstructionLabUsesApprovedRmbConfiguration()
        {
            const string path = "Assets/Game/Scenes/ConstructionLab.unity";
            Scene scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var controllers = new List<CityBuilderCamera>();
                var interactions = new List<ConstructionLabInteraction>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    controllers.AddRange(root.GetComponentsInChildren<CityBuilderCamera>(true));
                    interactions.AddRange(root.GetComponentsInChildren<ConstructionLabInteraction>(true));
                }
                Assert.That(controllers.Count, Is.EqualTo(1), "ConstructionLab must use exactly one city camera.");
                Assert.That(interactions.Count, Is.EqualTo(1), "ConstructionLab must use exactly one interaction controller.");
                CityBuilderCamera controller = controllers[0];
                Assert.That(controller.enabled && controller.gameObject.activeInHierarchy, Is.True);
                UnityEngine.Camera sceneCamera = controller.GetComponent<UnityEngine.Camera>();
                Assert.That(sceneCamera, Is.Not.Null);
                Assert.That(sceneCamera.enabled, Is.True);

                var cameraObject = new SerializedObject(controller);
                SerializedProperty settings = cameraObject.FindProperty("settings");
                Assert.That(settings, Is.Not.Null, "CameraSettings must be serialized on the real lab camera.");
                Assert.That(settings.FindPropertyRelative("moveSpeedMin").floatValue, Is.EqualTo(8f).Within(0.000001f));
                Assert.That(settings.FindPropertyRelative("zoomMinDistance").floatValue, Is.EqualTo(0.5f).Within(0.000001f));
                Assert.That(settings.FindPropertyRelative("zoomSpeed").floatValue, Is.EqualTo(0.30f).Within(0.000001f));
                Assert.That(settings.FindPropertyRelative("orbitYawSensitivity").floatValue, Is.EqualTo(0.216f).Within(0.000001f));
                Assert.That(settings.FindPropertyRelative("orbitPitchSensitivity").floatValue, Is.EqualTo(0.216f).Within(0.000001f));
                Assert.That(settings.FindPropertyRelative("invertHorizontal").boolValue, Is.False);
                Assert.That(settings.FindPropertyRelative("invertVertical").boolValue, Is.False);
                Assert.That(cameraObject.FindProperty("debugHome").boolValue, Is.False);
                Assert.That(new CameraSettings().moveSpeedMin, Is.EqualTo(3f));
                Assert.That(new CameraSettings().zoomMinDistance, Is.EqualTo(0.5f));
                Assert.That(new CameraSettings().orbitYawSensitivity, Is.EqualTo(0.216f));
                Assert.That(new CameraSettings().orbitPitchSensitivity, Is.EqualTo(0.216f));

                var interaction = new SerializedObject(interactions[0]);
                Assert.That(interaction.FindProperty("sceneCamera").objectReferenceValue, Is.SameAs(sceneCamera));
                Assert.That(interaction.FindProperty("cityCamera").objectReferenceValue, Is.SameAs(controller));
                Assert.That(interaction.FindProperty("debugHome").boolValue, Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
