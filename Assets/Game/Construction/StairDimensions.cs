using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct StairDimensions : IEquatable<StairDimensions>
    {
        // The cap keeps a single mesh and snap gesture bounded even for extreme input.
        public const int MaximumStepCount = 128;
        public float Width { get; }
        public float Height { get; }
        public float Run { get; }
        public int StepCount { get; }
        public float RiserHeight => Height / StepCount;
        public float TreadDepth => Run / StepCount;

        public StairDimensions(float width, float height, float run, int stepCount)
        {
            Validate(width, nameof(width)); Validate(height, nameof(height)); Validate(run, nameof(run));
            if (stepCount < 1 || stepCount > MaximumStepCount)
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            Width = width; Height = height; Run = run; StepCount = stepCount;
        }

        public bool IsValid => Valid(Width) && Valid(Height) && Valid(Run) && StepCount >= 1 && StepCount <= MaximumStepCount;
        private static bool Valid(float value) => value >= 0.1f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        { if (!Valid(value)) throw new ArgumentOutOfRangeException(name); }
        public bool Equals(StairDimensions other) => Width == other.Width && Height == other.Height && Run == other.Run && StepCount == other.StepCount;
        public override bool Equals(object obj) => obj is StairDimensions other && Equals(other);
        public override int GetHashCode() => (((Width.GetHashCode() * 397) ^ Height.GetHashCode()) * 397 ^ Run.GetHashCode()) * 397 ^ StepCount;
    }
}
