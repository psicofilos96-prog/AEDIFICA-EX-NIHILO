using UnityEngine;
using UnityEngine.InputSystem;

namespace Aedifica.Interaction.Camera
{
    public struct CameraInput
    {
        public Vector2 Move;
        public float KeyboardYaw;
        public float Scroll;
        public Vector2 RotatePixels;
        public Vector2 PanPixels;
    }

    public static class CameraInputReader
    {
        private static bool ownsCursorLock;
        private static CursorLockMode previousLockMode;
        private static bool previousVisibility;
        private static RotationCaptureFilter rotationCaptureFilter;

        public static CameraInput Read() => Read(out _, out _);

        public static CameraInput Read(out float rawScroll, out CameraScrollRegime scrollRegime)
        {
            var input = new CameraInput();
            rawScroll = 0f;
            scrollRegime = CameraScrollRegime.Precision;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null)
            {
                input.Move = new Vector2((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                input.KeyboardYaw = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            }
            if (mouse != null)
            {
                bool rightPressed = mouse.rightButton.isPressed;
                bool leftPressed = mouse.leftButton.isPressed;
                SetRotationCapture(rightPressed);
                rawScroll = mouse.scroll.ReadValue().y;
                input.Scroll = CameraScrollProcessor.Process(rawScroll, out scrollRegime);
                Vector2 delta = mouse.delta.ReadValue();
                if (rightPressed)
                    delta = rotationCaptureFilter.Filter(delta, mouse.position.ReadValue(),
                        new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                        Cursor.lockState == CursorLockMode.Locked);
                AssignMouseDrag(ref input, rightPressed, leftPressed, delta);
            }
            else ReleaseRotationCapture();
            return input;
        }

        public static void AssignMouseDrag(ref CameraInput input, bool rightPressed, bool leftPressed, Vector2 delta)
        {
            if (rightPressed) input.RotatePixels = delta;
            else if (leftPressed) input.PanPixels = delta;
        }

        private static void SetRotationCapture(bool rightPressed)
        {
            if (!rightPressed)
            {
                ReleaseRotationCapture();
                return;
            }
            if (ownsCursorLock) return;
            previousLockMode = Cursor.lockState;
            previousVisibility = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ownsCursorLock = true;
            rotationCaptureFilter.BeginCapture();
        }

        public static void ReleaseRotationCapture()
        {
            if (!ownsCursorLock) return;
            Cursor.lockState = previousLockMode;
            Cursor.visible = previousVisibility;
            ownsCursorLock = false;
            rotationCaptureFilter.Reset();
        }
    }

    public struct RotationCaptureFilter
    {
        private bool awaitingStableLock;

        // The lock/recenter transition has no fixed frame duration. Arm rotation only
        // after the locked cursor has settled at the viewport center with zero delta.
        public void BeginCapture() => awaitingStableLock = true;

        public Vector2 Filter(Vector2 delta, Vector2 position, Vector2 center, bool locked)
        {
            if (!locked)
            {
                awaitingStableLock = true;
                return Vector2.zero;
            }
            if (!awaitingStableLock) return delta;
            if ((position - center).sqrMagnitude <= 1f && delta.sqrMagnitude <= 0.0001f)
                awaitingStableLock = false;
            return Vector2.zero;
        }

        public void Reset() => awaitingStableLock = true;
    }
}
