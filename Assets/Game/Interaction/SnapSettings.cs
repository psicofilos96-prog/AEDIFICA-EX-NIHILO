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
