using System;
using UnityEngine;

namespace Aedifica.Interaction.Camera
{
    [Serializable]
    public sealed class CameraSettings
    {
        public float moveSpeedMin = 3f;
        public float moveSpeedMax = 150f;
        public float zoomSpeed = 0.30f;
        public float zoomMinDistance = 0.5f;
        public float zoomMaxDistance = 400f;
        public float yawSpeed = 0.2f;
        public float pitchSpeed = 0.2f;
        public float orbitYawSensitivity = 0.18f;
        public float orbitPitchSensitivity = 0.18f;
        public bool invertHorizontal;
        public bool invertVertical;
        public float keyboardYawSpeed = 90f;
        public float pitchMin = 15f;
        public float pitchMax = 80f;
        public float panSpeed = 0.002f;
        public float smoothing = 14f;

        public void Normalize()
        {
            moveSpeedMin = Positive(moveSpeedMin, 3f);
            moveSpeedMax = Mathf.Max(moveSpeedMin, Positive(moveSpeedMax, 150f));
            zoomSpeed = Positive(zoomSpeed, 0.30f);
            zoomMinDistance = Mathf.Max(0.5f, Positive(zoomMinDistance, 0.5f));
            zoomMaxDistance = Mathf.Max(zoomMinDistance, Positive(zoomMaxDistance, 400f));
            yawSpeed = Positive(yawSpeed, 0.2f);
            pitchSpeed = Positive(pitchSpeed, 0.2f);
            orbitYawSensitivity = orbitYawSensitivity <= 0f ? 0.18f : Positive(orbitYawSensitivity, 0.18f);
            orbitPitchSensitivity = orbitPitchSensitivity <= 0f ? 0.18f : Positive(orbitPitchSensitivity, 0.18f);
            keyboardYawSpeed = Positive(keyboardYawSpeed, 90f);
            pitchMin = Mathf.Clamp(Finite(pitchMin, 15f), 1f, 88f);
            pitchMax = Mathf.Clamp(Finite(pitchMax, 80f), pitchMin, 89f);
            panSpeed = Positive(panSpeed, 0.002f);
            smoothing = Mathf.Clamp(Finite(smoothing, 14f), 0f, 100f);
        }

        private static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        private static float Positive(float value, float fallback) => Mathf.Clamp(Finite(value, fallback), 0f, 10000f);
    }
}
