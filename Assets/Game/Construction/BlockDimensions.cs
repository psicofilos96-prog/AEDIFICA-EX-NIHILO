using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct BlockDimensions : IEquatable<BlockDimensions>
    {
        public float Width { get; }
        public float Height { get; }
        public float Depth { get; }

        public BlockDimensions(float width, float height, float depth)
        {
            Validate(width, nameof(width));
            Validate(height, nameof(height));
            Validate(depth, nameof(depth));
            Width = width;
            Height = height;
            Depth = depth;
        }

        public bool IsValid => IsPositiveFinite(Width) && IsPositiveFinite(Height) && IsPositiveFinite(Depth);
        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        {
            if (!IsPositiveFinite(value)) throw new ArgumentOutOfRangeException(name, "A dimension must be positive and finite.");
        }

        public bool Equals(BlockDimensions other) => Width == other.Width && Height == other.Height && Depth == other.Depth;
        public override bool Equals(object obj) => obj is BlockDimensions other && Equals(other);
        public override int GetHashCode() => ((Width.GetHashCode() * 397) ^ Height.GetHashCode()) * 397 ^ Depth.GetHashCode();
    }
}
