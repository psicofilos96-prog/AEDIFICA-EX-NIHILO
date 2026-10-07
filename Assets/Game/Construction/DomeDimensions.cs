using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct DomeDimensions : IEquatable<DomeDimensions>
    {
        public const float MaximumExtent = 10000f;
        public float Diameter { get; }
        public float Rise { get; }
        public float Thickness { get; }
        public float InnerDiameter => Diameter - 2f * Thickness;
        public float InnerRise => Rise - Thickness;
        public DomeDimensions(float diameter, float rise, float thickness)
        {
            Check(diameter, nameof(diameter)); Check(rise, nameof(rise)); Check(thickness, nameof(thickness));
            if (!Fits(diameter, rise, thickness)) throw new ArgumentException("Dome inner radii must remain positive.");
            Diameter = diameter; Rise = rise; Thickness = thickness;
        }
        public bool IsValid => Valid(Diameter) && Valid(Rise) && Valid(Thickness) && Fits(Diameter, Rise, Thickness);
        private static bool Fits(float d, float r, float t) => d >= 2f * t + 0.1f && r >= t + 0.1f;
        private static bool Valid(float value) => value >= 0.1f && value <= MaximumExtent &&
            !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Check(float value, string name) { if (!Valid(value)) throw new ArgumentOutOfRangeException(name); }
        public bool Equals(DomeDimensions other) => Diameter == other.Diameter && Rise == other.Rise && Thickness == other.Thickness;
        public override bool Equals(object obj) => obj is DomeDimensions other && Equals(other);
        public override int GetHashCode() => (Diameter.GetHashCode() * 397 ^ Rise.GetHashCode()) * 397 ^ Thickness.GetHashCode();
    }
}
