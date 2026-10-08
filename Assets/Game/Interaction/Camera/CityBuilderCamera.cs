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
        private bool panSuppressed;
        private bool panActive;
        private bool wasOrbiting;
        private UnityEngine.Camera sceneCamera;
        private readonly RaycastHit[] zoomHits = new RaycastHit[128];
        private Collider previousZoomCollider;
        private Vector3 previousZoomPoint;
        private bool previousZoomWasPlane;
        private bool hasPreviousZoomTarget;

        public void SetPanSuppressed(bool suppressed) => panSuppressed = suppressed;

        public bool BeginPan()
        {
            if (motion.IsOrbiting || Cursor.lockState == CursorLockMode.Locked ||
                Mouse.current != null && Mouse.current.rightButton.isPressed) return false;
            panActive = true;
            return true;
        }

        public void EndPan() => panActive = false;

        public bool FrameBounds(Bounds bounds)
        {
            bool rightPressed = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (motion == null || panActive || motion.IsOrbiting || rightPressed) return false;
            hasPreviousZoomTarget = false;
            motion.FrameBounds(bounds, sceneCamera.fieldOfView, sceneCamera.aspect, sceneCamera.nearClipPlane, settings);
            return true;
        }

        private void OnValidate() => settings?.Normalize();

        private void Awake()
        {
            settings ??= new CameraSettings();
            sceneCamera = GetComponent<UnityEngine.Camera>();
            motion = new CameraMotion(initialFocus, initialYaw, initialPitch, initialDistance, settings);
            ApplyTransform();
        }

        private void OnDisable()
        {
            EndPan();
            if (motion != null) motion.EndOrbit();
            wasOrbiting = false;
            CameraInputReader.ReleaseRotationCapture();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                EndPan();
                if (motion != null) motion.EndOrbit();
                wasOrbiting = false;
                CameraInputReader.ReleaseRotationCapture();
            }
        }

        private void Update()
        {
            CameraInput input = CameraInputReader.Read();
            bool orbiting = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (orbiting && !wasOrbiting)
            {
                hasPreviousZoomTarget = false;
                EndPan();
                motion.BeginOrbit();
            }
            if (!orbiting && wasOrbiting) motion.EndOrbit();
            wasOrbiting = orbiting;
            if (!panActive || panSuppressed || orbiting || Cursor.lockState == CursorLockMode.Locked)
                input.PanPixels = Vector2.zero;
            if (orbiting)
            {
                input.Move = Vector2.zero;
                input.KeyboardYaw = 0f;
                input.Scroll = 0f;
            }
            if (input.Move != Vector2.zero || input.PanPixels != Vector2.zero ||
                input.RotatePixels != Vector2.zero || input.KeyboardYaw != 0f || input.Scroll < 0f)
                hasPreviousZoomTarget = false;
            if (!orbiting && input.Scroll > 0f && TryZoomToCursor(input.Scroll))
                input.Scroll = 0f;
            motion.Step(input, Time.unscaledDeltaTime, settings);
            ApplyTransform();
        }

        private bool TryZoomToCursor(float scroll)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Cursor.lockState == CursorLockMode.Locked) return false;
            Vector2 pointer = mouse.position.ReadValue();
            if (!sceneCamera.pixelRect.Contains(pointer)) return false;
            Physics.SyncTransforms();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            float maxRayDistance = Mathf.Min(sceneCamera.farClipPlane, 1000f);
            int count = Physics.RaycastNonAlloc(ray, zoomHits, maxRayDistance, ~0, QueryTriggerInteraction.Ignore);
            // NonAlloc may return a full, unordered buffer. Resolve overflow
            // exceptionally with the complete hit list so the nearest surface wins.
            RaycastHit[] hits = count == zoomHits.Length
                ? Physics.RaycastAll(ray, maxRayDistance, ~0, QueryTriggerInteraction.Ignore)
                : zoomHits;
            if (count == zoomHits.Length) count = hits.Length;
            Collider nearest = null;
            Vector3 point = default;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || collider.GetComponent<Aedifica.Interaction.GizmoHandle>() != null) continue;
                if (hits[i].distance >= nearestDistance) continue;
                nearest = collider;
                nearestDistance = hits[i].distance;
                point = hits[i].point;
            }
            // The empty construction plane is useful only when its intersection
            // is nearby; a nearly horizontal ray must never launch Focus away.
            bool plane = nearest == null;
            if (plane)
            {
                var ground = new Plane(Vector3.up, Vector3.zero);
                float limit = Mathf.Min(maxRayDistance, Mathf.Max(10f, motion.Distance * 4f));
                if (!ground.Raycast(ray, out float planeDistance) || planeDistance <= 0f || planeDistance > limit)
                    return false; // Keep the existing orbital zoom for sky.
                point = ray.GetPoint(planeDistance);
            }
            bool sameTarget = hasPreviousZoomTarget && previousZoomWasPlane == plane &&
                previousZoomCollider == nearest &&
                Vector3.Distance(previousZoomPoint, point) <= Mathf.Max(0.25f, motion.Distance * 0.02f);
            if (!motion.ZoomToward(point, ray.direction, scroll, sceneCamera.nearClipPlane, sameTarget, settings))
                return false;
            hasPreviousZoomTarget = true;
            previousZoomWasPlane = plane;
            previousZoomCollider = nearest;
            previousZoomPoint = point;
            return true;
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(motion.Position, motion.Rotation);
        }
    }
}
