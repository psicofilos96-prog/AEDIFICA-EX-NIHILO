using System;

namespace Aedifica.Construction
{
    // A compact tagged value: exactly one semantic parameter family is valid at a time.
    [Serializable]
    public readonly struct PieceDimensions : IEquatable<PieceDimensions>
    {
        public PieceType Type { get; }
        public float X { get; }
        private readonly float height;
        public float Y => IsSlopedRoof ? profileThickness + Rise : IsRamp ? height + profileThickness : height;
        public float Z { get; }
        // Rise stores roof rise or arch opening rise; Y is derived for sloped roofs.
        public float Rise { get; }
        private readonly float profileThickness;
        private readonly float archPierWidth;
        private readonly int stepCount;
        public bool IsRoof => Type == PieceType.FlatRoof || IsSlopedRoof;
        public bool IsSlopedRoof => Type == PieceType.ShedRoof || Type == PieceType.GableRoof || Type == PieceType.HipRoof;
        public bool IsStair => Type == PieceType.Stair;
        public bool IsRamp => Type == PieceType.Ramp;
        public bool IsArch => Type == PieceType.Arch;
        public bool IsVault => Type == PieceType.Vault;
        public bool IsDome => Type == PieceType.Dome;
        public bool IsCurved => IsArch || IsVault || IsDome;
        public float ArchRise => IsArch ? Rise : throw new InvalidOperationException("Piece is not an Arch.");
        public float RoofThickness => IsRoof ? profileThickness : throw new InvalidOperationException("Piece is not a roof.");
        public float RampThickness => IsRamp ? profileThickness : throw new InvalidOperationException("Piece is not a ramp.");
        public float RampHeight => IsRamp ? height : throw new InvalidOperationException("Piece is not a ramp.");
        public int StepCount => IsStair ? stepCount : throw new InvalidOperationException("Piece is not a stair.");

        public PieceDimensions(BlockDimensions dimensions) : this(PieceType.Block, dimensions.Width, dimensions.Height, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(WallDimensions dimensions) : this(PieceType.Wall, dimensions.Length, dimensions.Height, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(SlabDimensions dimensions) : this(PieceType.Slab, dimensions.Width, dimensions.Thickness, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(ColumnDimensions dimensions) : this(PieceType.Column, dimensions.Width, dimensions.Height, dimensions.Depth, dimensions.IsValid) { }
        public PieceDimensions(BeamDimensions dimensions) : this(PieceType.Beam, dimensions.Length, dimensions.Height, dimensions.Width, dimensions.IsValid) { }
        public PieceDimensions(ParapetDimensions dimensions) : this(PieceType.Parapet, dimensions.Length, dimensions.Height, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(FlatRoofDimensions dimensions) : this(PieceType.FlatRoof, dimensions.Width, dimensions.Thickness, dimensions.Depth, 0f, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(ShedRoofDimensions dimensions) : this(PieceType.ShedRoof, dimensions.Width, 0f, dimensions.Depth, dimensions.Rise, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(GableRoofDimensions dimensions) : this(PieceType.GableRoof, dimensions.Width, 0f, dimensions.Depth, dimensions.Rise, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(HipRoofDimensions dimensions) : this(PieceType.HipRoof, dimensions.Width, 0f, dimensions.Depth, dimensions.Rise, dimensions.Thickness, dimensions.IsValid) { }
        public PieceDimensions(StairDimensions dimensions) : this(PieceType.Stair, dimensions.Width, dimensions.Height, dimensions.Run, 0f, 0f, dimensions.StepCount, dimensions.IsValid) { }
        public PieceDimensions(RampDimensions dimensions) : this(PieceType.Ramp, dimensions.Width, dimensions.Height, dimensions.Run, 0f, dimensions.Thickness, 0, dimensions.IsValid) { }
        public PieceDimensions(ArchDimensions dimensions) : this(PieceType.Arch, dimensions.Width, dimensions.Height, dimensions.Depth,
            dimensions.ArchRise, dimensions.CrownThickness, 0, dimensions.PierWidth, dimensions.IsValid) { }
        public PieceDimensions(VaultDimensions dimensions) : this(PieceType.Vault, dimensions.Width, dimensions.Height, dimensions.Length,
            0f, dimensions.Thickness, 0, 0f, dimensions.IsValid) { }
        public PieceDimensions(DomeDimensions dimensions) : this(PieceType.Dome, dimensions.Diameter, dimensions.Rise, dimensions.Diameter,
            0f, dimensions.Thickness, 0, 0f, dimensions.IsValid) { }

        private PieceDimensions(PieceType type, float x, float y, float z, bool valid)
            : this(type, x, y, z, 0f, 0f, valid) { }

        private PieceDimensions(PieceType type, float x, float y, float z, float rise, float thickness, bool valid)
            : this(type, x, y, z, rise, thickness, 0, valid) { }

        private PieceDimensions(PieceType type, float x, float y, float z, float rise, float thickness, int steps, bool valid)
            : this(type, x, y, z, rise, thickness, steps, 0f, valid) { }

        private PieceDimensions(PieceType type, float x, float y, float z, float rise, float thickness, int steps, float pierWidth, bool valid)
        {
            if (!valid) throw new ArgumentException("Piece dimensions must be valid.");
            Type = type;
            X = x;
            height = y;
            Z = z;
            Rise = rise;
            profileThickness = thickness;
            archPierWidth = pierWidth;
            stepCount = steps;
        }

        public bool IsValid => (Type == PieceType.Block || Type == PieceType.Wall || Type == PieceType.Slab ||
            Type == PieceType.Column || Type == PieceType.Beam || Type == PieceType.Parapet ||
            IsRoof || IsStair || IsRamp || IsCurved) &&
            Positive(X) && Positive(Y) && Positive(Z) && (IsSlopedRoof
                ? Positive(Rise) && Positive(profileThickness)
                : IsRoof ? Rise == 0f && profileThickness == Y
                : IsStair ? Rise == 0f && profileThickness == 0f &&
                    stepCount >= 1 && stepCount <= StairDimensions.MaximumStepCount
                : IsRamp ? Rise == 0f && stepCount == 0 && Positive(height) && Positive(profileThickness)
                : IsArch ? stepCount == 0 && archPierWidth >= 0.1f &&
                    X >= 2f * archPierWidth + 0.1f && profileThickness >= 0.1f &&
                    Rise >= 0.1f && Y >= Rise + profileThickness + 0.1f
                : IsVault ? Rise == 0f && stepCount == 0 && profileThickness >= 0.1f &&
                    X >= 2f * profileThickness + 0.1f && Y >= profileThickness + 0.1f
                : IsDome ? Rise == 0f && stepCount == 0 && X == Z && profileThickness >= 0.1f &&
                    X >= 2f * profileThickness + 0.1f && Y >= profileThickness + 0.1f
                : Rise == 0f && profileThickness == 0f && stepCount == 0 && archPierWidth == 0f);
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
        public StairDimensions AsStair() => IsStair ? new StairDimensions(X, Y, Z, StepCount) : throw new InvalidOperationException("Piece is not a Stair.");
        public RampDimensions AsRamp() => IsRamp ? new RampDimensions(X, RampHeight, Z, RampThickness) : throw new InvalidOperationException("Piece is not a Ramp.");
        public ArchDimensions AsArch() => IsArch ? new ArchDimensions(X, Y, Z, archPierWidth, Rise, profileThickness) : throw new InvalidOperationException("Piece is not an Arch.");
        public VaultDimensions AsVault() => IsVault ? new VaultDimensions(X, Y, Z, profileThickness) : throw new InvalidOperationException("Piece is not a Vault.");
        public DomeDimensions AsDome() => IsDome ? new DomeDimensions(X, Y, profileThickness) : throw new InvalidOperationException("Piece is not a Dome.");

        public PieceDimensions WithStepCount(int steps) => IsStair
            ? new PieceDimensions(new StairDimensions(X, Y, Z, steps))
            : throw new InvalidOperationException("Only stairs have StepCount.");

        public float MinimumForAxis(int axis)
        {
            if (axis < 0 || axis > 2) throw new ArgumentOutOfRangeException(nameof(axis));
            if (IsRamp && axis == 1) return RampThickness + 0.1f;
            if (IsArch) return axis == 0 ? 2f * archPierWidth + 0.1f : axis == 1 ? Rise + profileThickness + 0.1f : 0.1f;
            if (IsVault) return axis == 0 ? 2f * profileThickness + 0.1f : axis == 1 ? profileThickness + 0.1f : 0.1f;
            if (IsDome) return axis == 1 ? profileThickness + 0.1f : 2f * profileThickness + 0.1f;
            return 0.1f;
        }

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
                case PieceType.Stair: return new PieceDimensions(new StairDimensions(axis == 0 ? value : X, axis == 1 ? value : Y, axis == 2 ? value : Z, StepCount));
                case PieceType.Ramp: return new PieceDimensions(new RampDimensions(axis == 0 ? value : X,
                    axis == 1 ? Math.Max(0.1f, value - RampThickness) : RampHeight,
                    axis == 2 ? value : Z, RampThickness));
                case PieceType.Arch: return new PieceDimensions(new ArchDimensions(axis == 0 ? value : X,
                    axis == 1 ? value : Y, axis == 2 ? value : Z, archPierWidth, Rise, profileThickness));
                case PieceType.Vault: return new PieceDimensions(new VaultDimensions(axis == 0 ? value : X,
                    axis == 1 ? value : Y, axis == 2 ? value : Z, profileThickness));
                case PieceType.Dome: return new PieceDimensions(new DomeDimensions(axis == 1 ? X : value,
                    axis == 1 ? value : Y, profileThickness));
                default: throw new InvalidOperationException("Unsupported piece type.");
            }
        }

        public bool Equals(PieceDimensions other) => Type == other.Type && X == other.X && Y == other.Y && Z == other.Z && Rise == other.Rise && profileThickness == other.profileThickness && archPierWidth == other.archPierWidth && stepCount == other.stepCount;
        public override bool Equals(object obj) => obj is PieceDimensions other && Equals(other);
        public override int GetHashCode() => (((((((int)Type * 397 ^ X.GetHashCode()) * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode()) * 397 ^ Rise.GetHashCode()) * 397 ^ profileThickness.GetHashCode()) * 397 ^ archPierWidth.GetHashCode()) * 397 ^ stepCount;
    }
}
