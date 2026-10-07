using System;

namespace Aedifica.Construction
{
    // The profile is evaluated by subtracting fixed dimensions from the resized extent.
    // Leave several float ULPs beyond the 0.1 m clearance before that subtraction.
    internal static class CurvedDimensionPrecision
    {
        public const float MinimumClearance = 0.1f;
        private const float FloatRelativePrecision = 1.1920929e-7f;

        public static float ResizableMinimum(float fixedExtent, float maximumExtent)
        {
            float margin = Math.Max(0.000001f,
                8f * FloatRelativePrecision * Math.Max(1f, fixedExtent));
            return Math.Min(maximumExtent, fixedExtent + MinimumClearance + margin);
        }
    }
}
