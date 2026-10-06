using UnityEngine;

namespace Aedifica.Interaction.Camera
{
    public enum CameraScrollRegime
    {
        Precision,
        Transition,
        Wheel
    }

    public static class CameraScrollProcessor
    {
        private const float PrecisionLimit = 0.1f;
        private const float WheelLimit = 1f;
        private const float PrecisionScale = 1f / 120f;
        private const float WheelScale = 0.75f;
        private const float MaxRawMagnitude = 4f;

        public static float Process(float raw, out CameraScrollRegime regime)
        {
            if (float.IsNaN(raw) || float.IsInfinity(raw))
            {
                regime = CameraScrollRegime.Precision;
                return 0f;
            }

            float magnitude = Mathf.Min(Mathf.Abs(raw), MaxRawMagnitude);
            if (magnitude <= PrecisionLimit)
            {
                regime = CameraScrollRegime.Precision;
                return raw * PrecisionScale;
            }
            if (magnitude >= WheelLimit)
            {
                regime = CameraScrollRegime.Wheel;
                return Mathf.Sign(raw) * magnitude * WheelScale;
            }

            regime = CameraScrollRegime.Transition;
            float t = (magnitude - PrecisionLimit) / (WheelLimit - PrecisionLimit);
            t = t * t * (3f - 2f * t);
            float scale = Mathf.Lerp(PrecisionScale, WheelScale, t);
            return raw * scale;
        }
    }
}
