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
        public Vector3 OrbitPivot { get; private set; }

        private Vector3 targetFocus;
        private float targetYaw;
        private float targetPitch;
        private float targetDistance;

        public void ShiftFocus(Vector3 worldDelta)
        {
            if (float.IsNaN(worldDelta.x) || float.IsNaN(worldDelta.y) || float.IsNaN(worldDelta.z) ||
                float.IsInfinity(worldDelta.x) || float.IsInfinity(worldDelta.y) || float.IsInfinity(worldDelta.z)) return;
            targetFocus = Focus + worldDelta;
        }

        public void BeginOrbit(Vector3 pivot, CameraSettings settings)
        {
            Vector3 offset = pivot - Position;
            float distance = offset.magnitude;
            if (distance < 0.001f) return;
            Vector3 forward = offset / distance;
            OrbitPivot = Focus = targetFocus = pivot;
            Distance = targetDistance = distance;
            Yaw = targetYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            Pitch = targetPitch = Mathf.Clamp(-Mathf.Asin(forward.y) * Mathf.Rad2Deg,
                settings.pitchMin, settings.pitchMax);
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

        public static Vector3 GrabCorrection(Vector3 grabbedPoint, Vector3 currentPoint) => grabbedPoint - currentPoint;

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
            float yawSensitivity = IsOrbiting ? settings.orbitYawSensitivity * (settings.invertHorizontal ? -1f : 1f) : settings.yawSpeed;
            float pitchSensitivity = IsOrbiting ? settings.orbitPitchSensitivity * (settings.invertVertical ? -1f : 1f) : settings.pitchSpeed;
            targetYaw += input.KeyboardYaw * settings.keyboardYawSpeed * deltaTime + input.RotatePixels.x * yawSensitivity;
            targetPitch = Mathf.Clamp(targetPitch - input.RotatePixels.y * pitchSensitivity, settings.pitchMin, settings.pitchMax);
            if (!IsOrbiting)
            {
                targetDistance = Mathf.Clamp(targetDistance * Mathf.Exp(-input.Scroll * settings.zoomSpeed), settings.zoomMinDistance, settings.zoomMaxDistance);
                float speed = MoveSpeed(targetDistance, settings);
                targetFocus += HorizontalMove(input.Move, targetYaw) * speed * deltaTime;
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
