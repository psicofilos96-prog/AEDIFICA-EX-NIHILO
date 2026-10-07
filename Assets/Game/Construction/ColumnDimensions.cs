using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct ColumnDimensions : IEquatable<ColumnDimensions>
    {
        public float Width { get; }
        public float Height { get; }
        public float Depth { get; }

        public ColumnDimensions(float width, float height, float depth)
        {
            Validate(width, nameof(width));
            Validate(height, nameof(height));
            Validate(depth, nameof(depth));
            Width = width;
            Height = height;
            Depth = depth;
        }

        public bool IsValid => Positive(Width) && Positive(Height) && Positive(Depth);
        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        {
            if (!Positive(value)) throw new ArgumentOutOfRangeException(name, "A dimension must be positive and finite.");
        }

        public bool Equals(ColumnDimensions other) => Width == other.Width && Height == other.Height && Depth == other.Depth;
        public override bool Equals(object obj) => obj is ColumnDimensions other && Equals(other);
        public override int GetHashCode() => ((Width.GetHashCode() * 397) ^ Height.GetHashCode()) * 397 ^ Depth.GetHashCode();
    }
}
