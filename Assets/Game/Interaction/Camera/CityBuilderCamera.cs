using UnityEngine;

namespace Aedifica.Interaction.Camera
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CityBuilderCamera : MonoBehaviour
    {
        [SerializeField] private CameraSettings settings = new CameraSettings();
        [SerializeField] private Vector3 initialFocus = Vector3.zero;
        [SerializeField] private float initialYaw = 0f;
        [SerializeField] private float initialPitch = 45f;
        [SerializeField] private float initialDistance = 25f;

        private CameraMotion motion;

        public CameraMotion DiagnosticMotion => motion;
        public float DiagnosticZoomSpeed => settings.zoomSpeed;

        private void OnValidate() => settings?.Normalize();

        private void Awake()
        {
            settings ??= new CameraSettings();
            motion = new CameraMotion(initialFocus, initialYaw, initialPitch, initialDistance, settings);
            ApplyTransform();
        }

        private void OnDisable() => CameraInputReader.ReleaseRotationCapture();

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CameraInputReader.ReleaseRotationCapture();
        }

        private void Update()
        {
            motion.Step(CameraInputReader.Read(), Time.unscaledDeltaTime, settings);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(motion.Position, motion.Rotation);
        }
    }
}
