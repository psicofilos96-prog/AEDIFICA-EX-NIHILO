using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct ArchDimensions : IEquatable<ArchDimensions>
    {
        public const float MaximumExtent = 10000f;
        public float Width { get; }
        public float Height { get; }
        public float Depth { get; }
        public float PierWidth { get; }
        public float ArchRise { get; }
        public float CrownThickness { get; }
        public float OpeningWidth => Width - 2f * PierWidth;
        public float SpringHeight => Height - CrownThickness - ArchRise;
        public static float MinimumWidth(float pierWidth) =>
            CurvedDimensionPrecision.ResizableMinimum(2f * pierWidth, MaximumExtent);
        public static float MinimumHeight(float archRise, float crownThickness) =>
            CurvedDimensionPrecision.ResizableMinimum(archRise + crownThickness, MaximumExtent);

        public ArchDimensions(float width, float height, float depth, float pierWidth, float archRise, float crownThickness)
        {
            Check(width, nameof(width)); Check(height, nameof(height)); Check(depth, nameof(depth));
            Check(pierWidth, nameof(pierWidth)); Check(archRise, nameof(archRise)); Check(crownThickness, nameof(crownThickness));
            if (!Fits(width, height, pierWidth, archRise, crownThickness))
                throw new ArgumentException("Arch needs an opening, positive spring height and positive crown thickness.");
            Width = width; Height = height; Depth = depth; PierWidth = pierWidth;
            ArchRise = archRise; CrownThickness = crownThickness;
        }
        public bool IsValid => Valid(Width) && Valid(Height) && Valid(Depth) && Valid(PierWidth) &&
            Valid(ArchRise) && Valid(CrownThickness) && Fits(Width, Height, PierWidth, ArchRise, CrownThickness);
        private static bool Fits(float w, float h, float p, float r, float c) =>
            w - 2f * p >= CurvedDimensionPrecision.MinimumClearance &&
            h - c - r >= CurvedDimensionPrecision.MinimumClearance;
        private static bool Valid(float value) => value >= 0.1f && value <= MaximumExtent &&
            !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Check(float value, string name) { if (!Valid(value)) throw new ArgumentOutOfRangeException(name); }
        public bool Equals(ArchDimensions other) => Width == other.Width && Height == other.Height && Depth == other.Depth &&
            PierWidth == other.PierWidth && ArchRise == other.ArchRise && CrownThickness == other.CrownThickness;
        public override bool Equals(object obj) => obj is ArchDimensions other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Width.GetHashCode();
                hash = hash * 397 ^ Height.GetHashCode();
                hash = hash * 397 ^ Depth.GetHashCode();
                hash = hash * 397 ^ PierWidth.GetHashCode();
                hash = hash * 397 ^ ArchRise.GetHashCode();
                return hash * 397 ^ CrownThickness.GetHashCode();
            }
        }
    }
}
