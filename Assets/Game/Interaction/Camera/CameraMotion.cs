using UnityEngine;

namespace Aedifica.Interaction.Camera
{
    public sealed class CameraMotion
    {
        public Vector3 Focus { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Distance { get; private set; }

        private Vector3 targetFocus;
        private float targetYaw;
        private float targetPitch;
        private float targetDistance;

        public CameraMotion(Vector3 focus, float yaw, float pitch, float distance, CameraSettings settings)
        {
            settings.Normalize();
            Focus = targetFocus = new Vector3(Safe(focus.x), Safe(focus.y), Safe(focus.z));
            Yaw = targetYaw = Safe(yaw);
            Pitch = targetPitch = Mathf.Clamp(Safe(pitch), settings.pitchMin, settings.pitchMax);
            Distance = targetDistance = Mathf.Clamp(Safe(distance), settings.zoomMinDistance, settings.zoomMaxDistance);
        }

        private static float Safe(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        public static float MoveSpeed(float distance, CameraSettings settings)
        {
            float range = Mathf.Max(0.0001f, settings.zoomMaxDistance - settings.zoomMinDistance);
            float t = Mathf.Clamp01((distance - settings.zoomMinDistance) / range);
            return Mathf.Lerp(settings.moveSpeedMin, settings.moveSpeedMax, t);
        }

        public static Vector3 HorizontalMove(Vector2 input, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            return rotation * new Vector3(input.x, 0f, input.y).normalized;
        }

        public void Step(CameraInput input, float deltaTime, CameraSettings settings)
        {
            settings.Normalize();
            input.Move = new Vector2(Safe(input.Move.x), Safe(input.Move.y));
            input.RotatePixels = new Vector2(Safe(input.RotatePixels.x), Safe(input.RotatePixels.y));
            input.PanPixels = new Vector2(Safe(input.PanPixels.x), Safe(input.PanPixels.y));
            input.KeyboardYaw = Safe(input.KeyboardYaw);
            input.Scroll = Safe(input.Scroll);
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) return;
            targetYaw += input.KeyboardYaw * settings.keyboardYawSpeed * deltaTime + input.RotatePixels.x * settings.yawSpeed;
            targetPitch = Mathf.Clamp(targetPitch - input.RotatePixels.y * settings.pitchSpeed, settings.pitchMin, settings.pitchMax);
            targetDistance = Mathf.Clamp(targetDistance * Mathf.Exp(-input.Scroll * settings.zoomSpeed), settings.zoomMinDistance, settings.zoomMaxDistance);
            float speed = MoveSpeed(targetDistance, settings);
            targetFocus += HorizontalMove(input.Move, targetYaw) * speed * deltaTime;
            float fieldOfView = input.PanFieldOfView > 0f ? input.PanFieldOfView : 60f;
            float pixelHeight = input.PanPixelHeight > 0f ? input.PanPixelHeight : 600f;
            targetFocus += GroundPan(input.PanPixels, targetYaw, targetPitch, targetDistance,
                fieldOfView, pixelHeight, settings.panSpeed);
            float blend = settings.smoothing == 0f ? 1f : 1f - Mathf.Exp(-settings.smoothing * deltaTime);
            Focus = Vector3.Lerp(Focus, targetFocus, blend);
            Yaw = Mathf.LerpAngle(Yaw, targetYaw, blend);
            Pitch = Mathf.Lerp(Pitch, targetPitch, blend);
            Distance = Mathf.Lerp(Distance, targetDistance, blend);
        }

        public static Vector3 GroundPan(Vector2 pixels, float yaw, float pitch, float distance,
            float fieldOfView, float pixelHeight, float panSpeed)
        {
            if (pixels == Vector2.zero || pixelHeight <= 0f) return Vector3.zero;
            // Keep the approved 600 px / 60 degree baseline while accounting for the actual viewport.
            float baseline = 2f * Mathf.Tan(30f * Mathf.Deg2Rad) / 600f;
            float metersPerPixel = 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) / pixelHeight;
            float scale = metersPerPixel * panSpeed / baseline;
            float groundVertical = -pixels.y / Mathf.Max(0.1f, Mathf.Sin(pitch * Mathf.Deg2Rad));
            return Quaternion.Euler(0f, yaw, 0f) * new Vector3(-pixels.x, 0f, groundVertical) * scale;
        }

        public Vector3 Position => Focus - Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward * Distance;
        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);
    }
}
