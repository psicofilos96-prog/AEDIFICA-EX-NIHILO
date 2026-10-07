using System;

namespace Aedifica.Interaction
{
    [Serializable]
    public sealed class SnapSettings
    {
        public bool PositionSnapEnabled;
        public bool RotationSnapEnabled;
        public float PositionIncrement = 0.5f;
        public float RotationIncrementDegrees = 15f;
        public bool SurfaceSnapEnabled;
        public bool EdgeSnapEnabled;
        public bool EndpointSnapEnabled;
        public float CaptureDistance = 0.25f;
        public float ReleaseDistance = 0.4f;

        public bool HasGeometricSnap => SurfaceSnapEnabled || EdgeSnapEnabled || EndpointSnapEnabled;

        public void ValidateGeometric()
        {
            if (!(CaptureDistance > 0f) || float.IsNaN(CaptureDistance) || float.IsInfinity(CaptureDistance) ||
                !(ReleaseDistance > CaptureDistance) || float.IsNaN(ReleaseDistance) || float.IsInfinity(ReleaseDistance))
                throw new ArgumentOutOfRangeException(nameof(CaptureDistance), "Snap distances must be finite and release must exceed capture.");
        }

        public bool TogglePosition()
        {
            if (!PositionSnapEnabled) SnapPolicy.ValidateIncrement(PositionIncrement);
            PositionSnapEnabled = !PositionSnapEnabled;
            return PositionSnapEnabled;
        }

        public bool ToggleRotation()
        {
            if (!RotationSnapEnabled) SnapPolicy.ValidateIncrement(RotationIncrementDegrees);
            RotationSnapEnabled = !RotationSnapEnabled;
            return RotationSnapEnabled;
        }

        public void ValidateEnabled()
        {
            if (PositionSnapEnabled) SnapPolicy.ValidateIncrement(PositionIncrement);
            if (RotationSnapEnabled) SnapPolicy.ValidateIncrement(RotationIncrementDegrees);
        }
    }
}
