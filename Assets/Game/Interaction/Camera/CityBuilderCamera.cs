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
        [SerializeField] private bool debugHome;

        private CameraMotion motion;
        private bool panSuppressed;
        private bool panActive;
        private bool wasOrbiting;
        private bool homeFirstStepPending;
        private bool homeFinalStepPending;
        private float homeFinalStepTime;
        private bool homeOverwriteReported;

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
            if (motion == null || panActive || motion.IsOrbiting || rightPressed)
            {
                if (debugHome) Debug.LogWarning($"Home camera rejected: frame={Time.frameCount}, motionReady={motion != null}, panActive={panActive}, orbitActive={motion != null && motion.IsOrbiting}, rightPressed={rightPressed}, cursorLock={Cursor.lockState}", this);
                return false;
            }
            UnityEngine.Camera sceneCamera = GetComponent<UnityEngine.Camera>();
            if (debugHome) Debug.Log($"Home camera before: frame={Time.frameCount}, camera={sceneCamera.name}#{sceneCamera.GetInstanceID()}, cameraEnabled={sceneCamera.enabled}, mainCamera={(UnityEngine.Camera.main != null ? UnityEngine.Camera.main.GetInstanceID().ToString() : "none")}, focus={motion.Focus.ToString("F4")}, distance={motion.Distance:R}, targetFocus={motion.TargetFocus.ToString("F4")}, targetDistance={motion.TargetDistance:R}, position={transform.position.ToString("F4")}, boundsCenter={bounds.center.ToString("F4")}, boundsSize={bounds.size.ToString("F4")}", this);
            motion.FrameBounds(bounds, sceneCamera.fieldOfView, sceneCamera.aspect, sceneCamera.nearClipPlane, settings);
            if (debugHome)
            {
                Debug.Log($"Home camera target: frame={Time.frameCount}, focus={motion.Focus.ToString("F4")}, distance={motion.Distance:R}, targetFocus={motion.TargetFocus.ToString("F4")}, targetDistance={motion.TargetDistance:R}, fov={sceneCamera.fieldOfView:R}, aspect={sceneCamera.aspect:R}, nearClip={sceneCamera.nearClipPlane:R}", this);
                homeFirstStepPending = true;
                homeFinalStepPending = true;
                homeFinalStepTime = Time.unscaledTime + 0.5f;
                homeOverwriteReported = false;
            }
            return true;
        }

        private void OnValidate() => settings?.Normalize();

        private void Awake()
        {
            settings ??= new CameraSettings();
            motion = new CameraMotion(initialFocus, initialYaw, initialPitch, initialDistance, settings);
            ApplyTransform();
        }

        private void OnDisable()
        {
            EndPan();
            if (motion != null) EndOrbitWithDiagnostics("OnDisable");
            wasOrbiting = false;
            CameraInputReader.ReleaseRotationCapture();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                EndPan();
                if (motion != null) EndOrbitWithDiagnostics("FocusLost");
                wasOrbiting = false;
                CameraInputReader.ReleaseRotationCapture();
            }
        }

        private void Update()
        {
            if (debugHome && homeFinalStepPending && !homeOverwriteReported &&
                Vector3.Distance(transform.position, motion.Position) > 0.01f)
            {
                Debug.LogWarning($"Home transform overwritten before camera Update: frame={Time.frameCount}, transformPosition={transform.position.ToString("F4")}, motionPosition={motion.Position.ToString("F4")}", this);
                homeOverwriteReported = true;
            }
            CameraInput input = CameraInputReader.Read(out float rawScroll, out CameraScrollRegime scrollRegime);
            bool orbiting = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (orbiting && !wasOrbiting)
            {
                EndPan();
                float distanceBefore = motion.Distance;
                Vector3 focusBefore = motion.Focus;
                motion.BeginOrbit();
                if (debugOrbit)
                    Debug.Log($"Orbit begin transition: frame={Time.frameCount}, distanceBefore={distanceBefore:R}, distanceAfter={motion.Distance:R}, focusBefore={focusBefore.ToString("F6")}, focusAfter={motion.Focus.ToString("F6")}, cameraPosition={motion.Position.ToString("F6")}", this);
            }
            if (!orbiting && wasOrbiting) EndOrbitWithDiagnostics("RmbReleased");
            wasOrbiting = orbiting;
            if (!panActive || panSuppressed || orbiting || Cursor.lockState == CursorLockMode.Locked)
                input.PanPixels = Vector2.zero;
            if (orbiting)
            {
                input.Move = Vector2.zero;
                input.KeyboardYaw = 0f;
                input.Scroll = 0f;
            }
            Vector3 previousPosition = motion.Position;
            float distanceBeforeStep = motion.Distance;
            float consumedScroll = input.Scroll;
            motion.Step(input, Time.unscaledDeltaTime, settings);
            if (debugHome && consumedScroll != 0f)
                Debug.Log($"Home zoom: frame={Time.frameCount}, rawScroll={rawScroll:R}, processedScroll={consumedScroll:R}, distanceBefore={distanceBeforeStep:R}, distanceAfter={motion.Distance:R}, targetDistance={motion.TargetDistance:R}, focus={motion.Focus.ToString("F4")}, orbitActive={motion.IsOrbiting}", this);
            if (debugOrbit && motion.Distance != distanceBeforeStep)
            {
                string cause = consumedScroll != 0f && !motion.IsOrbiting ? "ZoomInput" :
                    motion.IsOrbiting ? "OrbitStep" : "ZoomSmoothing";
                Debug.Log($"Distance change: frame={Time.frameCount}, cause={cause}, rawScroll={rawScroll:R}, processedScroll={consumedScroll:R}, scrollRegime={scrollRegime}, distanceBefore={distanceBeforeStep:R}, distanceAfter={motion.Distance:R}, rmbPressed={orbiting}, orbitActive={motion.IsOrbiting}, cursorLock={Cursor.lockState}", this);
            }
            if (debugOrbit && orbiting && input.RotatePixels != Vector2.zero)
                Debug.Log($"Orbit drag: mouseDelta={input.RotatePixels}, yaw={motion.Yaw:F2}, pitch={motion.Pitch:F2}, distance={motion.Distance:F3}, cameraDisplacement={Vector3.Distance(previousPosition, motion.Position):F3}", this);
            ApplyTransform();
            if (debugHome && homeFirstStepPending)
            {
                Debug.Log($"Home first step: frame={Time.frameCount}, focus={motion.Focus.ToString("F4")}, distance={motion.Distance:R}, targetFocus={motion.TargetFocus.ToString("F4")}, targetDistance={motion.TargetDistance:R}, position={transform.position.ToString("F4")}", this);
                homeFirstStepPending = false;
            }
            if (debugHome && homeFinalStepPending && Time.unscaledTime >= homeFinalStepTime)
            {
                Debug.Log($"Home after 0.5s: frame={Time.frameCount}, focus={motion.Focus.ToString("F4")}, distance={motion.Distance:R}, targetFocus={motion.TargetFocus.ToString("F4")}, targetDistance={motion.TargetDistance:R}, position={transform.position.ToString("F4")}", this);
                homeFinalStepPending = false;
            }
        }

        private void EndOrbitWithDiagnostics(string reason)
        {
            if (debugOrbit && motion.IsOrbiting)
                Debug.Log($"Orbit end: frame={Time.frameCount}, reason={reason}, distance={motion.Distance:R}, focus={motion.Focus.ToString("F6")}, cameraPosition={motion.Position.ToString("F6")}, yaw={motion.Yaw:R}, pitch={motion.Pitch:R}", this);
            motion.EndOrbit();
        }

        private void ApplyTransform()
        {
            transform.SetPositionAndRotation(motion.Position, motion.Rotation);
        }
    }
}
