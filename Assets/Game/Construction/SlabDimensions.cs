using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct SlabDimensions : IEquatable<SlabDimensions>
    {
        public float Width { get; }
        public float Thickness { get; }
        public float Depth { get; }

        public SlabDimensions(float width, float thickness, float depth)
        {
            Validate(width, nameof(width));
            Validate(thickness, nameof(thickness));
            Validate(depth, nameof(depth));
            Width = width;
            Thickness = thickness;
            Depth = depth;
        }

        public bool IsValid => Positive(Width) && Positive(Thickness) && Positive(Depth);
        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        {
            if (!Positive(value)) throw new ArgumentOutOfRangeException(name, "A dimension must be positive and finite.");
        }

        public bool Equals(SlabDimensions other) => Width == other.Width && Thickness == other.Thickness && Depth == other.Depth;
        public override bool Equals(object obj) => obj is SlabDimensions other && Equals(other);
        public override int GetHashCode() => ((Width.GetHashCode() * 397) ^ Thickness.GetHashCode()) * 397 ^ Depth.GetHashCode();
    }
}
