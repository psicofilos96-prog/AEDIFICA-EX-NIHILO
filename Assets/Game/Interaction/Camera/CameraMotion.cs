using UnityEngine;

namespace Aedifica.Interaction.Camera
{
    public sealed class CameraMotion
    {
        public Vector3 Focus { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Distance { get; private set; }
        public bool IsOrbiting { get; private set; }
        internal Vector3 TargetFocus => targetFocus;
        internal float TargetDistance => targetDistance;

        private Vector3 targetFocus;
        private float targetYaw;
        private float targetPitch;
        private float targetDistance;

        public void BeginOrbit()
        {
            targetFocus = Focus;
            targetDistance = Distance;
            targetYaw = Yaw;
            targetPitch = Pitch;
            IsOrbiting = true;
        }

        public void EndOrbit()
        {
            IsOrbiting = false;
            targetFocus = Focus;
            targetDistance = Distance;
            targetYaw = Yaw;
            targetPitch = Pitch;
        }

        // Frame the world-space renderer bounds within the current camera projection.
        // Only an explicit command changes the navigation center this way.
        public void FrameBounds(Bounds bounds, float verticalFieldOfView, float aspect, float nearClip, CameraSettings settings)
        {
            settings.Normalize();
            if (IsOrbiting) return;
            float halfVertical = Mathf.Tan(Mathf.Clamp(verticalFieldOfView, 1f, 179f) * Mathf.Deg2Rad * 0.5f) * 0.8f;
            float halfHorizontal = halfVertical * Mathf.Max(0.01f, aspect);
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(targetPitch, targetYaw, 0f));
            Vector3 extent = bounds.extents;
            float requiredDistance = settings.zoomMinDistance;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = new Vector3(x * extent.x, y * extent.y, z * extent.z);
                Vector3 local = inverse * corner;
                requiredDistance = Mathf.Max(requiredDistance,
                    Mathf.Abs(local.x) / halfHorizontal - local.z,
                    Mathf.Abs(local.y) / halfVertical - local.z,
                    Mathf.Max(0f, nearClip) + 0.1f - local.z);
            }
            targetFocus = bounds.center;
            targetDistance = Mathf.Clamp(requiredDistance, settings.zoomMinDistance, settings.zoomMaxDistance);
        }

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

        // Opposite camera translation makes the world follow the dragged cursor.
        // Camera right/up are orthonormal screen axes. Translating the camera
        // opposite to the drag makes stationary geometry follow the cursor.
        public static Vector3 ScreenPan(Vector2 pixels, float yaw, float pitch, float distance, CameraSettings settings)
        {
            float metersPerPixel = settings.panSpeed * Mathf.Clamp(distance, settings.zoomMinDistance, settings.zoomMaxDistance);
            return -(Quaternion.Euler(pitch, yaw, 0f) * new Vector3(pixels.x, pixels.y, 0f)) * metersPerPixel;
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
            float yawSensitivity = IsOrbiting ? settings.orbitYawSensitivity * (settings.invertHorizontal ? -1f : 1f) : settings.yawSpeed;
            float pitchSensitivity = IsOrbiting ? settings.orbitPitchSensitivity * (settings.invertVertical ? -1f : 1f) : settings.pitchSpeed;
            targetYaw += input.KeyboardYaw * settings.keyboardYawSpeed * deltaTime + input.RotatePixels.x * yawSensitivity;
            targetPitch = Mathf.Clamp(targetPitch - input.RotatePixels.y * pitchSensitivity, settings.pitchMin, settings.pitchMax);
            if (!IsOrbiting)
            {
                targetDistance = Mathf.Clamp(targetDistance * Mathf.Exp(-input.Scroll * settings.zoomSpeed), settings.zoomMinDistance, settings.zoomMaxDistance);
                float speed = MoveSpeed(targetDistance, settings);
                targetFocus += HorizontalMove(input.Move, targetYaw) * speed * deltaTime;
                targetFocus += ScreenPan(input.PanPixels, Yaw, Pitch, Distance, settings);
            }
            float blend = IsOrbiting || settings.smoothing == 0f ? 1f : 1f - Mathf.Exp(-settings.smoothing * deltaTime);
            Focus = Vector3.Lerp(Focus, targetFocus, blend);
            Yaw = Mathf.LerpAngle(Yaw, targetYaw, blend);
            Pitch = Mathf.Lerp(Pitch, targetPitch, blend);
            Distance = Mathf.Lerp(Distance, targetDistance, blend);
        }


        public Vector3 Position => Focus - Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward * Distance;
        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);
    }
}
