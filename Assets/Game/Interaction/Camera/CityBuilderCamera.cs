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
            if (motion.IsOrbiting) return false;
            grabPlane = new Plane(Vector3.up, new Vector3(0f, motion.Focus.y, 0f));
            worldGrabActive = TryGroundPoint(sceneCamera.ScreenPointToRay(pointer), grabPlane, out grabbedPoint);
            return worldGrabActive;
        }

        public void DragWorld(Vector2 pointer)
        {
            if (!worldGrabActive || panSuppressed || motion.IsOrbiting) return;
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

        public static Ray ViewportCenterRay(UnityEngine.Camera camera) => camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        private Vector3 ChooseOrbitPivot()
        {
            Ray centerRay = ViewportCenterRay(sceneCamera);
            float nearest = float.MaxValue;
            Vector3 surfacePoint = default;
            foreach (RaycastHit hit in Physics.RaycastAll(centerRay, sceneCamera.farClipPlane))
                if (hit.distance < nearest && (hit.collider.GetComponent<TerrainCollider>() != null || hit.collider.name == "Lab Ground"))
                {
                    nearest = hit.distance;
                    surfacePoint = hit.point;
                }
            if (nearest < float.MaxValue) return surfacePoint;
            var plane = new Plane(Vector3.up, new Vector3(0f, motion.Focus.y, 0f));
            return TryGroundPoint(centerRay, plane, out Vector3 point) ? point : motion.Focus;
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
            if (motion != null) motion.EndOrbit();
            wasOrbiting = false;
            CameraInputReader.ReleaseRotationCapture();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                if (motion != null) motion.EndOrbit();
                wasOrbiting = false;
                CameraInputReader.ReleaseRotationCapture();
            }
        }

        private void Update()
        {
            CameraInput input = CameraInputReader.Read();
            input.PanPixels = Vector2.zero; // LMB pan uses the grabbed world point instead of mouse delta.
            bool orbiting = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (orbiting && !wasOrbiting)
            {
                EndWorldGrab();
                motion.BeginOrbit(ChooseOrbitPivot(), settings);
            }
            if (!orbiting && wasOrbiting) motion.EndOrbit();
            wasOrbiting = orbiting;
            if (orbiting)
            {
                input.Move = Vector2.zero;
                input.KeyboardYaw = 0f;
                input.Scroll = 0f;
            }
            motion.Step(input, Time.unscaledDeltaTime, settings);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(motion.Position, motion.Rotation);
        }
    }
}
