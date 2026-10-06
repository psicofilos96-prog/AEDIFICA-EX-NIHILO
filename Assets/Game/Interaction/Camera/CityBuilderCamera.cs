using UnityEngine;
using UnityEngine.InputSystem;

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
        private bool worldGrabActive;
        private Plane grabPlane;
        private Vector3 grabbedPoint;
        private bool wasOrbiting;

        public void SetPanSuppressed(bool suppressed) => panSuppressed = suppressed;

        public bool BeginWorldGrab(Vector2 pointer)
        {
            grabPlane = new Plane(Vector3.up, new Vector3(0f, motion.Focus.y, 0f));
            worldGrabActive = TryGroundPoint(sceneCamera.ScreenPointToRay(pointer), grabPlane, out grabbedPoint);
            return worldGrabActive;
        }

        public void DragWorld(Vector2 pointer)
        {
            if (!worldGrabActive || panSuppressed) return;
            if (TryGroundPoint(sceneCamera.ScreenPointToRay(pointer), grabPlane, out Vector3 currentPoint))
                motion.ShiftFocus(CameraMotion.GrabCorrection(grabbedPoint, currentPoint));
        }

        public void EndWorldGrab() => worldGrabActive = false;

        public static bool TryGroundPoint(Ray ray, Plane plane, out Vector3 point)
        {
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }

        private void OnValidate() => settings?.Normalize();

        private void Awake()
        {
            sceneCamera = GetComponent<UnityEngine.Camera>();
            settings ??= new CameraSettings();
            motion = new CameraMotion(initialFocus, initialYaw, initialPitch, initialDistance, settings);
            ApplyTransform();
        }

        private void OnDisable()
        {
            EndWorldGrab();
            CameraInputReader.ReleaseRotationCapture();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CameraInputReader.ReleaseRotationCapture();
        }

        private void Update()
        {
            CameraInput input = CameraInputReader.Read();
            input.PanPixels = Vector2.zero; // LMB pan uses the grabbed world point instead of mouse delta.
            bool orbiting = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (orbiting && !wasOrbiting) motion.SettleFocus();
            wasOrbiting = orbiting;
            motion.Step(input, Time.unscaledDeltaTime, settings);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(motion.Position, motion.Rotation);
        }
    }
}
