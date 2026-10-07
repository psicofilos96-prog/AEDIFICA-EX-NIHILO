using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct ParapetDimensions : IEquatable<ParapetDimensions>
    {
        public float Length { get; }
        public float Height { get; }
        public float Thickness { get; }

        public ParapetDimensions(float length, float height, float thickness)
        {
            Validate(length, nameof(length));
            Validate(height, nameof(height));
            Validate(thickness, nameof(thickness));
            Length = length;
            Height = height;
            Thickness = thickness;
        }

        public bool IsValid => Positive(Length) && Positive(Height) && Positive(Thickness);
        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        {
            if (!Positive(value)) throw new ArgumentOutOfRangeException(name, "A dimension must be positive and finite.");
        }

        public bool Equals(ParapetDimensions other) => Length == other.Length && Height == other.Height && Thickness == other.Thickness;
        public override bool Equals(object obj) => obj is ParapetDimensions other && Equals(other);
        public override int GetHashCode() => ((Length.GetHashCode() * 397) ^ Height.GetHashCode()) * 397 ^ Thickness.GetHashCode();
    }
}
