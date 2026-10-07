using System;

namespace Aedifica.Construction
{
    public readonly struct CirculationSection
    {
        public readonly float StartZ, EndZ, BottomStart, BottomEnd, TopStart, TopEnd;
        public CirculationSection(float startZ, float endZ, float bottomStart, float bottomEnd, float topStart, float topEnd)
        { StartZ = startZ; EndZ = endZ; BottomStart = bottomStart; BottomEnd = bottomEnd; TopStart = topStart; TopEnd = topEnd; }
    }

    // One solid profile shared by mesh and snap. Local -Z is low; +Z is high.
    public sealed class CirculationProfile
    {
        public CirculationSection[] Sections { get; }
        public bool IsStair { get; }
        public bool IsRamp => !IsStair;
        public float Width { get; }
        public float TotalHeight { get; }

        private CirculationProfile(CirculationSection[] sections, bool isStair, float width, float totalHeight)
        { Sections = sections; IsStair = isStair; Width = width; TotalHeight = totalHeight; }

        public static CirculationProfile Create(PieceDimensions dimensions)
        {
            if (!dimensions.IsValid || !dimensions.IsStair && !dimensions.IsRamp)
                throw new ArgumentException("A valid Stair or Ramp is required.", nameof(dimensions));
            if (dimensions.IsRamp)
                return new CirculationProfile(new[] { new CirculationSection(-dimensions.Z * 0.5f,
                    dimensions.Z * 0.5f, 0f, dimensions.RampHeight,
                    dimensions.RampThickness, dimensions.Y) }, false, dimensions.X, dimensions.Y);
            int count = dimensions.StepCount;
            var sections = new CirculationSection[count];
            float start = -dimensions.Z * 0.5f;
            float previousTop = 0f;
            for (int i = 0; i < count; i++)
            {
                float end = i == count - 1 ? dimensions.Z * 0.5f
                    : -dimensions.Z * 0.5f + dimensions.Z * (i + 1) / count;
                float top = i == count - 1 ? dimensions.Y : dimensions.Y * (i + 1) / count;
                sections[i] = new CirculationSection(start, end, 0f, 0f, top, top);
                start = end;
                previousTop = top;
            }
            return new CirculationProfile(sections, true, dimensions.X, previousTop);
        }
    }
}
