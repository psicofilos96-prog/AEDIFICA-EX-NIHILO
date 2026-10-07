using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct GableRoofDimensions : IEquatable<GableRoofDimensions>
    {
        public float Width { get; }
        public float Depth { get; }
        public float Thickness { get; }
        public float Rise { get; }

        public GableRoofDimensions(float width, float depth, float thickness, float rise)
        {
            Validate(width, nameof(width));
            Validate(depth, nameof(depth));
            Validate(thickness, nameof(thickness));
            Validate(rise, nameof(rise));
            Width = width;
            Depth = depth;
            Thickness = thickness;
            Rise = rise;
        }

        public bool IsValid => Positive(Width) && Positive(Depth) && Positive(Thickness) && Positive(Rise);
        private static bool Positive(float value) => value >= 0.1f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        {
            if (!Positive(value)) throw new ArgumentOutOfRangeException(name, "Roof dimensions must be positive and finite.");
        }

        public bool Equals(GableRoofDimensions other) => Width == other.Width && Depth == other.Depth && Thickness == other.Thickness && Rise == other.Rise;
        public override bool Equals(object obj) => obj is GableRoofDimensions other && Equals(other);
        public override int GetHashCode() => Width.GetHashCode() ^ Depth.GetHashCode() ^ Thickness.GetHashCode() ^ Rise.GetHashCode();
    }
}
