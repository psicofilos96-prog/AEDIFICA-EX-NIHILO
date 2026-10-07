using System;

namespace Aedifica.Construction
{
    public sealed class PieceData
    {
        public PieceId Id { get; }
        public PieceType Type { get; }
        public PieceTransform Transform { get; }
        public PieceDimensions Dimensions { get; }
        public MaterialId MaterialId { get; }
        public BlockDimensions BlockDimensions => Dimensions.AsBlock();
        public WallDimensions WallDimensions => Dimensions.AsWall();
        public SlabDimensions SlabDimensions => Dimensions.AsSlab();
        public ColumnDimensions ColumnDimensions => Dimensions.AsColumn();
        public BeamDimensions BeamDimensions => Dimensions.AsBeam();
        public ParapetDimensions ParapetDimensions => Dimensions.AsParapet();
        public FlatRoofDimensions FlatRoofDimensions => Dimensions.AsFlatRoof();
        public ShedRoofDimensions ShedRoofDimensions => Dimensions.AsShedRoof();
        public GableRoofDimensions GableRoofDimensions => Dimensions.AsGableRoof();
        public HipRoofDimensions HipRoofDimensions => Dimensions.AsHipRoof();

        public PieceData(PieceId id, PieceTransform transform, BlockDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, WallDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, SlabDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, ColumnDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, BeamDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, ParapetDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, FlatRoofDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, ShedRoofDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, GableRoofDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, HipRoofDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }

        // Keep the P0.2 constructor while rejecting a mismatched semantic type.
        public PieceData(PieceId id, PieceType type, PieceTransform transform, BlockDimensions dimensions)
            : this(id, transform, CheckBlockType(type, dimensions)) { }

        private static PieceDimensions CheckBlockType(PieceType type, BlockDimensions dimensions)
        {
            if (type != PieceType.Block) throw new ArgumentOutOfRangeException(nameof(type));
            return new PieceDimensions(dimensions);
        }

        public PieceData(PieceId id, PieceTransform transform, PieceDimensions dimensions)
            : this(id, transform, dimensions, LabMaterialIds.Neutral) { }

        public PieceData(PieceId id, PieceTransform transform, PieceDimensions dimensions, MaterialId materialId)
        {
            if (!id.IsValid) throw new ArgumentException("A piece must have a valid ID.", nameof(id));
            if (!transform.IsValid) throw new ArgumentException("A piece must have a valid transform.", nameof(transform));
            if (!dimensions.IsValid) throw new ArgumentException("A piece must have valid dimensions.", nameof(dimensions));
            Id = id;
            Type = dimensions.Type;
            Transform = transform;
            Dimensions = dimensions;
            MaterialId = materialId;
        }

        public PieceData WithTransform(PieceTransform transform) => new PieceData(Id, transform, Dimensions, MaterialId);
        public PieceData WithDimensions(PieceDimensions dimensions)
        {
            if (dimensions.Type != Type) throw new ArgumentException("Replacement parameters must preserve piece type.", nameof(dimensions));
            return new PieceData(Id, Transform, dimensions, MaterialId);
        }
        public PieceData WithMaterial(MaterialId materialId) => new PieceData(Id, Transform, Dimensions, materialId);
        public PieceData WithBlockDimensions(BlockDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithWallDimensions(WallDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithSlabDimensions(SlabDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithColumnDimensions(ColumnDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithBeamDimensions(BeamDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithParapetDimensions(ParapetDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithFlatRoofDimensions(FlatRoofDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithShedRoofDimensions(ShedRoofDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithGableRoofDimensions(GableRoofDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithHipRoofDimensions(HipRoofDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithRise(float rise) => WithDimensions(Dimensions.WithRise(rise));
    }
}
