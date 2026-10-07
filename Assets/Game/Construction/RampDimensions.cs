using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct RampDimensions : IEquatable<RampDimensions>
    {
        public float Width { get; }
        public float Height { get; }
        public float Run { get; }
        public float Thickness { get; }
        public float PitchDegrees => (float)(Math.Atan2(Height, Run) * 180.0 / Math.PI);

        public RampDimensions(float width, float height, float run, float thickness)
        {
            Validate(width, nameof(width)); Validate(height, nameof(height));
            Validate(run, nameof(run)); Validate(thickness, nameof(thickness));
            if (float.IsInfinity(height + thickness))
                throw new ArgumentOutOfRangeException(nameof(height), "Height plus thickness must be finite.");
            Width = width; Height = height; Run = run; Thickness = thickness;
        }

        public bool IsValid => Valid(Width) && Valid(Height) && Valid(Run) && Valid(Thickness) &&
            !float.IsInfinity(Height + Thickness);
        private static bool Valid(float value) => value >= 0.1f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        { if (!Valid(value)) throw new ArgumentOutOfRangeException(name); }
        public bool Equals(RampDimensions other) => Width == other.Width && Height == other.Height && Run == other.Run && Thickness == other.Thickness;
        public override bool Equals(object obj) => obj is RampDimensions other && Equals(other);
        public override int GetHashCode() => (((Width.GetHashCode() * 397) ^ Height.GetHashCode()) * 397 ^ Run.GetHashCode()) * 397 ^ Thickness.GetHashCode();
    }
}
