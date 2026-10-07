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

        public PieceDimensions(BlockDimensions dimensions) : this(PieceType.Block, dimensions.Width, dimensions.Height, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(WallDimensions dimensions) : this(PieceType.Wall, dimensions.Length, dimensions.Height, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(SlabDimensions dimensions) : this(PieceType.Slab, dimensions.Width, dimensions.Thickness, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(ColumnDimensions dimensions) : this(PieceType.Column, dimensions.Width, dimensions.Height, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(BeamDimensions dimensions) : this(PieceType.Beam, dimensions.Length, dimensions.Height, dimensions.Width, dimensions.IsValid) { }
        public PieceDimensions(ParapetDimensions dimensions) : this(PieceType.Parapet, dimensions.Length, dimensions.Height, dimensions.Thickness, dimensions.IsValid) { }

        private PieceDimensions(PieceType type, float x, float y, float z, bool valid)
        {
            if (!valid) throw new ArgumentException("Piece dimensions must be valid.");
            Type = type;
            X = x;
            Y = y;
            Z = z;
        }

        public bool IsValid => (Type == PieceType.Block || Type == PieceType.Wall || Type == PieceType.Slab ||
            Type == PieceType.Column || Type == PieceType.Beam || Type == PieceType.Parapet) &&
            Positive(X) && Positive(Y) && Positive(Z);
        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        public BlockDimensions AsBlock() => Type == PieceType.Block ? new BlockDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Block.");
        public WallDimensions AsWall() => Type == PieceType.Wall ? new WallDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Wall.");
        public SlabDimensions AsSlab() => Type == PieceType.Slab ? new SlabDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Slab.");
        public ColumnDimensions AsColumn() => Type == PieceType.Column ? new ColumnDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Column.");
        public BeamDimensions AsBeam() => Type == PieceType.Beam ? new BeamDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Beam.");
        public ParapetDimensions AsParapet() => Type == PieceType.Parapet ? new ParapetDimensions(X, Y, Z) : throw new InvalidOperationException("Piece is not a Parapet.");

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
                default: throw new InvalidOperationException("Unsupported piece type.");
            }
        }

        public bool Equals(PieceDimensions other) => Type == other.Type && X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is PieceDimensions other && Equals(other);
        public override int GetHashCode() => (((int)Type * 397 ^ X.GetHashCode()) * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
    }
}
