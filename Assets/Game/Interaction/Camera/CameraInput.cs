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

    public struct CameraInputDiagnostics
    {
        public bool RightPressed;
        public bool LeftPressed;
        public bool MiddlePressed;
        public Vector2 MouseDelta;
        public float RawScrollY;
        public float LastNonzeroRawScrollY;
        public float LastNonzeroNormalizedScroll;
        public CameraInput Input;
    }

    public static class CameraInputReader
    {
        public static CameraInputDiagnostics LastDiagnostics { get; private set; }

        private static float lastNonzeroRawScrollY;
        private static float lastNonzeroNormalizedScroll;
        private static bool ownsCursorLock;
        private static CursorLockMode previousLockMode;
        private static bool previousVisibility;

        public static CameraInput Read()
        {
            var input = new CameraInput();
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null)
            {
                input.Move = new Vector2((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                input.KeyboardYaw = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            }
            var diagnostics = new CameraInputDiagnostics();
            if (mouse != null)
            {
                diagnostics.RightPressed = mouse.rightButton.isPressed;
                diagnostics.LeftPressed = mouse.leftButton.isPressed;
                diagnostics.MiddlePressed = mouse.middleButton.isPressed;
                SetRotationCapture(diagnostics.RightPressed);
                diagnostics.RawScrollY = mouse.scroll.ReadValue().y;
                diagnostics.MouseDelta = mouse.delta.ReadValue();
                input.Scroll = diagnostics.RawScrollY / 120f;
                if (diagnostics.RawScrollY != 0f)
                {
                    lastNonzeroRawScrollY = diagnostics.RawScrollY;
                    lastNonzeroNormalizedScroll = input.Scroll;
                }
                AssignMouseDrag(ref input, diagnostics.RightPressed, diagnostics.LeftPressed, diagnostics.MouseDelta);
            }
            else ReleaseRotationCapture();
            diagnostics.LastNonzeroRawScrollY = lastNonzeroRawScrollY;
            diagnostics.LastNonzeroNormalizedScroll = lastNonzeroNormalizedScroll;
            diagnostics.Input = input;
            LastDiagnostics = diagnostics;
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
        }

        public static void ReleaseRotationCapture()
        {
            if (!ownsCursorLock) return;
            Cursor.lockState = previousLockMode;
            Cursor.visible = previousVisibility;
            ownsCursorLock = false;
        }
    }
}
