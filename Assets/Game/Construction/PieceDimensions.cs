using System;

namespace Aedifica.Construction
{
    // A compact tagged value: exactly one semantic parameter family is valid at a time.
    [Serializable]
    public readonly struct PieceDimensions : IEquatable<PieceDimensions>
    {
        public PieceType Type { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        // Rise is authoritative for sloped roofs; Y is the derived total height.
        public float Rise { get; }
        private readonly float roofThickness;
        public bool IsRoof => Type == PieceType.FlatRoof || IsSlopedRoof;
        public bool IsSlopedRoof => Type == PieceType.ShedRoof || Type == PieceType.GableRoof || Type == PieceType.HipRoof;
        public float RoofThickness => IsRoof ? roofThickness : throw new InvalidOperationException("Piece is not a roof.");

        public PieceDimensions(BlockDimensions dimensions) : this(PieceType.Block, dimensions.Width, dimensions.Height, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(WallDimensions dimensions) : this(PieceType.Wall, dimensions.Length, dimensions.Height, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(SlabDimensions dimensions) : this(PieceType.Slab, dimensions.Width, dimensions.Thickness, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(ColumnDimensions dimensions) : this(PieceType.Column, dimensions.Width, dimensions.Height, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(BeamDimensions dimensions) : this(PieceType.Beam, dimensions.Length, dimensions.Height, dimensions.Width, dimensions.IsValid) { }
        public PieceDimensions(ParapetDimensions dimensions) : this(PieceType.Parapet, dimensions.Length, dimensions.Height, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(FlatRoofDimensions dimensions) : this(PieceType.FlatRoof, dimensions.Width, dimensions.Thickness, dimensions.Depth, 0f, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(ShedRoofDimensions dimensions) : this(PieceType.ShedRoof, dimensions.Width, dimensions.Thickness + dimensions.Rise, dimensions.Depth, dimensions.Rise, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(GableRoofDimensions dimensions) : this(PieceType.GableRoof, dimensions.Width, dimensions.Thickness + dimensions.Rise, dimensions.Depth, dimensions.Rise, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(HipRoofDimensions dimensions) : this(PieceType.HipRoof, dimensions.Width, dimensions.Thickness + dimensions.Rise, dimensions.Depth, dimensions.Rise, dimensions.Thickness, dimensions.IsValid) { }

        private PieceDimensions(PieceType type, float x, float y, float z, bool valid)
            : this(type, x, y, z, 0f, 0f, valid) { }

        private PieceDimensions(PieceType type, float x, float y, float z, float rise, float thickness, bool valid)
        {
            if (!valid) throw new ArgumentException("Piece dimensions must be valid.");
            Type = type;
            X = x;
            Y = y;
            Z = z;
            Rise = rise;
            roofThickness = thickness;
        }

        public bool IsValid => (Type == PieceType.Block || Type == PieceType.Wall || Type == PieceType.Slab ||
            Type == PieceType.Column || Type == PieceType.Beam || Type == PieceType.Parapet || IsRoof) &&
            Positive(X) && Positive(Y) && Positive(Z) && (IsSlopedRoof
                ? Positive(Rise) && Positive(roofThickness) && Y == Rise + roofThickness
                : IsRoof ? Rise == 0f && roofThickness == Y : Rise == 0f && roofThickness == 0f);
        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        public BlockDimensions AsBlock() => Type == PieceType.Block ? new BlockDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Block.");
        public WallDimensions AsWall() => Type == PieceType.Wall ? new WallDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Wall.");
        public SlabDimensions AsSlab() => Type == PieceType.Slab ? new SlabDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Slab.");
        public ColumnDimensions AsColumn() => Type == PieceType.Column ? new ColumnDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Column.");
        public BeamDimensions AsBeam() => Type == PieceType.Beam ? new BeamDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Beam.");
        public ParapetDimensions AsParapet() => Type == PieceType.Parapet ? new ParapetDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Parapet.");
        public FlatRoofDimensions AsFlatRoof() => Type == PieceType.FlatRoof ? new FlatRoofDimensions(X, Z, Y) : throw new InvalidOperationException("Piece is not a FlatRoof.");
        public ShedRoofDimensions AsShedRoof() => Type == PieceType.ShedRoof ? new ShedRoofDimensions(X, Z, RoofThickness, Rise) : throw new InvalidOperationException("Piece is not a ShedRoof.");
        public GableRoofDimensions AsGableRoof() => Type == PieceType.GableRoof ? new GableRoofDimensions(X, Z, RoofThickness, Rise) : throw new InvalidOperationException("Piece is not a GableRoof.");
        public HipRoofDimensions AsHipRoof() => Type == PieceType.HipRoof ? new HipRoofDimensions(X, Z, RoofThickness, Rise) : throw new InvalidOperationException("Piece is not a HipRoof.");

        public PieceDimensions WithRise(float rise)
        {
            switch (Type)
            {
                case PieceType.ShedRoof: return new PieceDimensions(new ShedRoofDimensions(X, Z, RoofThickness, rise));
                case PieceType.GableRoof: return new PieceDimensions(new GableRoofDimensions(X, Z, RoofThickness, rise));
                case PieceType.HipRoof: return new PieceDimensions(new HipRoofDimensions(X, Z, RoofThickness, rise));
                default: throw new InvalidOperationException("Only sloped roofs have Rise.");
            }
        }

        public PieceDimensions Resize(int axis, float value)
        {
            if (!IsValid) throw new InvalidOperationException("Invalid piece dimensions.");
            if (axis < 0 || axis > 2) throw new ArgumentOutOfRangeException(nameof(axis));
            switch (Type)
            {
                case PieceType.Block: return new PieceDimensions(new BlockDimensions(axis == 0 ? value : X, axis == 1 ? value : Y, axis == 2 ? value : Z));
                case PieceType.Wall: return new PieceDimensions(new WallDimensions(axis == 0 ? value : X, axis == 1 ? value : Y, axis == 2 ? value : Z));
                case PieceType.Slab: return new PieceDimensions(new SlabDimensions(axis == 0 ? value : X, axis == 1 ? value : Y, axis == 2 ? value : Z));
                case PieceType.Column: return new PieceDimensions(new ColumnDimensions(axis == 0 ? value : X, axis == 1 ? value : Y, axis == 2 ? value : Z));
                case PieceType.Beam: return new PieceDimensions(new BeamDimensions(axis == 0 ? value : X, axis == 1 ? value : Y, axis == 2 ? value : Z));
                case PieceType.Parapet: return new PieceDimensions(new ParapetDimensions(axis == 0 ? value : X, axis == 1 ? value : Y, axis == 2 ? value : Z));
                case PieceType.FlatRoof: return new PieceDimensions(new FlatRoofDimensions(axis == 0 ? value : X, axis == 2 ? value : Z, axis == 1 ? value : Y));
                case PieceType.ShedRoof:
                    if (axis == 1) throw new InvalidOperationException("Sloped roof height is derived from Thickness and Rise.");
                    return new PieceDimensions(new ShedRoofDimensions(axis == 0 ? value : X, axis == 2 ? value : Z, RoofThickness, Rise));
                case PieceType.GableRoof:
                    if (axis == 1) throw new InvalidOperationException("Sloped roof height is derived from Thickness and Rise.");
                    return new PieceDimensions(new GableRoofDimensions(axis == 0 ? value : X, axis == 2 ? value : Z, RoofThickness, Rise));
                case PieceType.HipRoof:
                    if (axis == 1) throw new InvalidOperationException("Sloped roof height is derived from Thickness and Rise.");
                    return new PieceDimensions(new HipRoofDimensions(axis == 0 ? value : X, axis == 2 ? value : Z, RoofThickness, Rise));
                default: throw new InvalidOperationException("Unsupported piece type.");
            }
        }

        public bool Equals(PieceDimensions other) => Type == other.Type && X == other.X && Y == other.Y && Z == other.Z && Rise == other.Rise && roofThickness == other.roofThickness;
        public override bool Equals(object obj) => obj is PieceDimensions other && Equals(other);
        public override int GetHashCode() => (((((int)Type * 397 ^ X.GetHashCode()) * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode()) * 397 ^ Rise.GetHashCode()) * 397 ^ roofThickness.GetHashCode();
    }
}
