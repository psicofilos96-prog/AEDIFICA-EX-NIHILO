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
        private UnityEngine.Camera sceneCamera;
        private bool panSuppressed;

        public void SetPanSuppressed(bool suppressed) => panSuppressed = suppressed;

        private void OnValidate() => settings?.Normalize();

        private void Awake()
        {
            sceneCamera = GetComponent<UnityEngine.Camera>();
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
            CameraInput input = CameraInputReader.Read();
            if (panSuppressed) input.PanPixels = Vector2.zero;
            input.PanFieldOfView = sceneCamera.fieldOfView;
            input.PanPixelHeight = sceneCamera.pixelHeight;
            motion.Step(input, Time.unscaledDeltaTime, settings);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(motion.Position, motion.Rotation);
        }
    }
}
