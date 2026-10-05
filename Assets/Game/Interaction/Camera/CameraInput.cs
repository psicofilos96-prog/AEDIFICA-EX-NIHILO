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
                input.Scroll = mouse.scroll.ReadValue().y / 120f;
                var delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed) input.RotatePixels = delta;
                else if (mouse.middleButton.isPressed) input.PanPixels = delta;
            }
            return input;
        }
    }
}
