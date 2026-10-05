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
            if (mouse != null)
            {
                bool rightPressed = mouse.rightButton.isPressed;
                SetRotationCapture(rightPressed);
                input.Scroll = mouse.scroll.ReadValue().y / 120f;
                AssignMouseDrag(ref input, rightPressed, mouse.middleButton.isPressed, mouse.delta.ReadValue());
            }
            else ReleaseRotationCapture();
            return input;
        }

        public static void AssignMouseDrag(ref CameraInput input, bool rightPressed, bool middlePressed, Vector2 delta)
        {
            if (rightPressed) input.RotatePixels = delta;
            else if (middlePressed) input.PanPixels = delta;
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
