using UnityEngine;

namespace Aedifica.Interaction.Camera
{
    // Temporary P0.1 instrumentation. Remove this component and its diagnostic accessors after local diagnosis.
    [RequireComponent(typeof(CityBuilderCamera))]
    public sealed class ConstructionLabCameraDiagnostics : MonoBehaviour
    {
        private CityBuilderCamera controller;
        private UnityEngine.Camera sceneCamera;

        private void Awake()
        {
            controller = GetComponent<CityBuilderCamera>();
            sceneCamera = GetComponent<UnityEngine.Camera>();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || controller == null || controller.DiagnosticMotion == null) return;

            var read = CameraInputReader.LastDiagnostics;
            var motion = controller.DiagnosticMotion;
            GUILayout.BeginArea(new Rect(12f, 12f, 410f, 470f), GUI.skin.box);
            GUILayout.Label("CAMERA DEBUG — TEMPORARY P0.1");
            GUILayout.Label($"RMB: {(read.RightPressed ? "DOWN" : "UP")}   MMB: {(read.MiddlePressed ? "DOWN" : "UP")}");
            GUILayout.Label($"Mouse Delta: X {read.MouseDelta.x:F3}   Y {read.MouseDelta.y:F3}");
            GUILayout.Label($"RotatePixels: X {read.Input.RotatePixels.x:F3}   Y {read.Input.RotatePixels.y:F3}");
            GUILayout.Label($"PanPixels: X {read.Input.PanPixels.x:F3}   Y {read.Input.PanPixels.y:F3}");
            GUILayout.Label($"Raw Scroll Y: {read.RawScrollY:F4}   Normalized: {read.Input.Scroll:F4}");
            GUILayout.Label($"Zoom Speed (runtime): {controller.DiagnosticZoomSpeed:F3}");
            GUILayout.Label($"Distance: {motion.Distance:F3}   Target: {motion.TargetDistance:F3}");
            GUILayout.Label($"Yaw: {motion.Yaw:F3}   Target: {motion.TargetYaw:F3}");
            GUILayout.Label($"Pitch: {motion.Pitch:F3}   Target: {motion.TargetPitch:F3}");
            GUILayout.Label($"Cursor Lock: {Cursor.lockState}   Visible: {Cursor.visible}");
            GUILayout.Label($"Camera GO: {gameObject.name}   Enabled: {sceneCamera.enabled}");
            GUILayout.Label($"Camera.main: {(UnityEngine.Camera.main == sceneCamera ? "THIS CAMERA" : "OTHER/NONE")}   Active cameras: {UnityEngine.Camera.allCamerasCount}");
            GUILayout.Label($"Transform Position: {transform.position.ToString("F3")}");
            GUILayout.Label($"Transform Rotation: {transform.eulerAngles.ToString("F3")}");
            GUILayout.EndArea();
        }
    }
}
