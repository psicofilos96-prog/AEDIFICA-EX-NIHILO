using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct VaultDimensions : IEquatable<VaultDimensions>
    {
        public const float MaximumExtent = 10000f;
        public float Width { get; }
        public float Height { get; }
        public float Length { get; }
        public float Thickness { get; }
        public float InnerWidth => Width - 2f * Thickness;
        public float InnerRise => Height - Thickness;
        public VaultDimensions(float width, float height, float length, float thickness)
        {
            Check(width, nameof(width)); Check(height, nameof(height)); Check(length, nameof(length)); Check(thickness, nameof(thickness));
            if (!Fits(width, height, thickness)) throw new ArgumentException("Vault inner radii must remain positive.");
            Width = width; Height = height; Length = length; Thickness = thickness;
        }
        public bool IsValid => Valid(Width) && Valid(Height) && Valid(Length) && Valid(Thickness) && Fits(Width, Height, Thickness);
        private static bool Fits(float w, float h, float t) => w >= 2f * t + 0.1f && h >= t + 0.1f;
        private static bool Valid(float value) => value >= 0.1f && value <= MaximumExtent &&
            !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Check(float value, string name) { if (!Valid(value)) throw new ArgumentOutOfRangeException(name); }
        public bool Equals(VaultDimensions other) => Width == other.Width && Height == other.Height && Length == other.Length && Thickness == other.Thickness;
        public override bool Equals(object obj) => obj is VaultDimensions other && Equals(other);
        public override int GetHashCode() => (((Width.GetHashCode() * 397 ^ Height.GetHashCode()) * 397 ^ Length.GetHashCode()) * 397 ^ Thickness.GetHashCode());
    }
}
