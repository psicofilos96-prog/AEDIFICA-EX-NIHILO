using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct FlatRoofDimensions : IEquatable<FlatRoofDimensions>
    {
        public float Width { get; }
        public float Depth { get; }
        public float Thickness { get; }

        public FlatRoofDimensions(float width, float depth, float thickness)
        {
            Validate(width, nameof(width));
            Validate(depth, nameof(depth));
            Validate(thickness, nameof(thickness));
            Width = width;
            Depth = depth;
            Thickness = thickness;
        }

        public bool IsValid => Positive(Width) && Positive(Depth) && Positive(Thickness);
        private static bool Positive(float value) => value >= 0.1f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        {
            if (!Positive(value)) throw new ArgumentOutOfRangeException(name, "Roof dimensions must be positive and finite.");
        }

        public bool Equals(FlatRoofDimensions other) => Width == other.Width && Depth == other.Depth && Thickness == other.Thickness;
        public override bool Equals(object obj) => obj is FlatRoofDimensions other && Equals(other);
        public override int GetHashCode() => Width.GetHashCode() ^ Depth.GetHashCode() ^ Thickness.GetHashCode();
    }
}
