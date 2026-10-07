using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct BeamDimensions : IEquatable<BeamDimensions>
    {
        public float Length { get; }
        public float Height { get; }
        public float Width { get; }

        public BeamDimensions(float length, float height, float width)
        {
            Validate(length, nameof(length));
            Validate(height, nameof(height));
            Validate(width, nameof(width));
            Length = length;
            Height = height;
            Width = width;
        }

        public bool IsValid => Positive(Length) && Positive(Height) && Positive(Width);
        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Validate(float value, string name)
        {
            if (!Positive(value)) throw new ArgumentOutOfRangeException(name, "A dimension must be positive and finite.");
        }

        public bool Equals(BeamDimensions other) => Length == other.Length && Height == other.Height && Width == other.Width;
        public override bool Equals(object obj) => obj is BeamDimensions other && Equals(other);
        public override int GetHashCode() => ((Length.GetHashCode() * 397) ^ Height.GetHashCode()) * 397 ^ Width.GetHashCode();
    }
}
