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
        [SerializeField] private bool debugOrbit;

        public enum OrbitPivotSource { Surface, NavigationPlane, BoundedRay }

        private CameraMotion motion;
        private UnityEngine.Camera sceneCamera;
        private bool panSuppressed;
        private bool worldGrabActive;
        private Plane grabPlane;
        private Vector3 grabbedPoint;
        private bool wasOrbiting;
        private OrbitPivotSource orbitPivotSource;

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

        public static Vector3 SelectOrbitPivot(Ray ray, Vector3 cameraPosition, float navigationDistance,
            Vector3? surfacePoint, Plane plane, out OrbitPivotSource source)
        {
            float minimum = navigationDistance * 0.5f;
            float maximum = navigationDistance * 1.5f;
            if (surfacePoint.HasValue && IsUseful(surfacePoint.Value))
            {
                source = OrbitPivotSource.Surface;
                return surfacePoint.Value;
            }
            // A nearly parallel ray can place the plane intersection arbitrarily far away.
            if (Mathf.Abs(Vector3.Dot(ray.direction, plane.normal)) >= 0.1f &&
                TryGroundPoint(ray, plane, out Vector3 planePoint) && IsUseful(planePoint))
            {
                source = OrbitPivotSource.NavigationPlane;
                return planePoint;
            }
            source = OrbitPivotSource.BoundedRay;
            return cameraPosition + ray.direction * navigationDistance;

            bool IsUseful(Vector3 candidate)
            {
                float distance = Vector3.Distance(cameraPosition, candidate);
                return !float.IsNaN(distance) && !float.IsInfinity(distance) &&
                    distance >= minimum && distance <= maximum;
            }
        }

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
            var plane = new Plane(Vector3.up, new Vector3(0f, motion.Focus.y, 0f));
            Vector3 pivot = SelectOrbitPivot(centerRay, sceneCamera.transform.position, motion.Distance,
                nearest < float.MaxValue ? surfacePoint : (Vector3?)null, plane, out orbitPivotSource);
            if (debugOrbit)
            {
                float surfaceDistance = nearest < float.MaxValue ? Vector3.Distance(sceneCamera.transform.position, surfacePoint) : float.NaN;
                float planeDistance = TryGroundPoint(centerRay, plane, out Vector3 point) ? Vector3.Distance(sceneCamera.transform.position, point) : float.NaN;
                Debug.Log($"Orbit begin: source={orbitPivotSource}, navigationDistance={motion.Distance:F3}, surfaceDistance={surfaceDistance:F3}, planeDistance={planeDistance:F3}, pivotDistance={Vector3.Distance(sceneCamera.transform.position, pivot):F3}, yaw={motion.Yaw:F2}, pitch={motion.Pitch:F2}", this);
            }
            return pivot;
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
            Vector3 previousPosition = motion.Position;
            motion.Step(input, Time.unscaledDeltaTime, settings);
            if (debugOrbit && orbiting && input.RotatePixels != Vector2.zero)
                Debug.Log($"Orbit drag: source={orbitPivotSource}, mouseDelta={input.RotatePixels}, yaw={motion.Yaw:F2}, pitch={motion.Pitch:F2}, pivotDistance={Vector3.Distance(motion.Position, motion.OrbitPivot):F3}, cameraDisplacement={Vector3.Distance(previousPosition, motion.Position):F3}", this);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(motion.Position, motion.Rotation);
        }
    }
}
