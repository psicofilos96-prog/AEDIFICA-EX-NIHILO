using System;
using System.Collections.Generic;

namespace Aedifica.Construction
{
    public sealed class PieceData
    {
        private static readonly WallOpening[] EmptyOpenings = Array.Empty<WallOpening>();
        private static readonly IReadOnlyList<WallOpening> EmptyOpeningView = Array.AsReadOnly(EmptyOpenings);
        public PieceId Id { get; }
        public PieceType Type { get; }
        public PieceTransform Transform { get; }
        public PieceDimensions Dimensions { get; }
        public MaterialId MaterialId { get; }
        private readonly WallOpening[] openings;
        private readonly IReadOnlyList<WallOpening> readOnlyOpenings;
        public IReadOnlyList<WallOpening> Openings => readOnlyOpenings;
        private static readonly ArchitecturalFinish[] EmptyFinishes = Array.Empty<ArchitecturalFinish>();
        private static readonly IReadOnlyList<ArchitecturalFinish> EmptyFinishView = Array.AsReadOnly(EmptyFinishes);
        private readonly ArchitecturalFinish[] finishes;
        private readonly IReadOnlyList<ArchitecturalFinish> readOnlyFinishes;
        public IReadOnlyList<ArchitecturalFinish> Finishes => readOnlyFinishes;
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
        public StairDimensions StairDimensions => Dimensions.AsStair();
        public RampDimensions RampDimensions => Dimensions.AsRamp();
        public ArchDimensions ArchDimensions => Dimensions.AsArch();
        public VaultDimensions VaultDimensions => Dimensions.AsVault();
        public DomeDimensions DomeDimensions => Dimensions.AsDome();

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
        public PieceData(PieceId id, PieceTransform transform, StairDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, RampDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, ArchDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, VaultDimensions dimensions)
            : this(id, transform, new PieceDimensions(dimensions)) { }
        public PieceData(PieceId id, PieceTransform transform, DomeDimensions dimensions)
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
            : this(id, transform, dimensions, materialId, EmptyOpenings, EmptyFinishes) { }

        private PieceData(PieceId id, PieceTransform transform, PieceDimensions dimensions,
            MaterialId materialId, WallOpening[] wallOpenings, ArchitecturalFinish[] finishData,
            IReadOnlyList<WallOpening> existingView = null,
            IReadOnlyList<ArchitecturalFinish> existingFinishView = null)
        {
            if (!id.IsValid) throw new ArgumentException("A piece must have a valid ID.", nameof(id));
            if (!transform.IsValid) throw new ArgumentException("A piece must have a valid transform.", nameof(transform));
            if (!dimensions.IsValid) throw new ArgumentException("A piece must have valid dimensions.", nameof(dimensions));
            Id = id;
            Type = dimensions.Type;
            Transform = transform;
            Dimensions = dimensions;
            MaterialId = materialId;
            WallOpeningLayout.Validate(id, dimensions, wallOpenings);
            ValidateFinishes(id, dimensions, wallOpenings, finishData);
            openings = wallOpenings;
            finishes = finishData;
            readOnlyFinishes = existingFinishView ?? (finishes.Length == 0 ? EmptyFinishView : Array.AsReadOnly(finishes));
            readOnlyOpenings = existingView ?? (openings.Length == 0 ? EmptyOpeningView : Array.AsReadOnly(openings));
        }

        public PieceData WithTransform(PieceTransform transform) => new PieceData(Id, transform, Dimensions, MaterialId, openings, finishes, readOnlyOpenings, readOnlyFinishes);
        public PieceData WithDimensions(PieceDimensions dimensions)
        {
            if (dimensions.Type != Type) throw new ArgumentException("Replacement parameters must preserve piece type.", nameof(dimensions));
            return new PieceData(Id, Transform, dimensions, MaterialId, openings, finishes, readOnlyOpenings, readOnlyFinishes);
        }
        public PieceData WithMaterial(MaterialId materialId) => new PieceData(Id, Transform, Dimensions, materialId, openings, finishes, readOnlyOpenings, readOnlyFinishes);
        public float MinimumResizeDimension(int axis)
        {
            float minimum = Dimensions.MinimumForAxis(axis);
            if (Type != PieceType.Wall || openings.Length == 0) return minimum;
            if (axis == 0)
                foreach (WallOpening opening in openings)
                    minimum = Math.Max(minimum, opening.Right + WallOpening.MinimumSolid + 0.0001f);
            if (axis == 1)
                foreach (WallOpening opening in openings)
                    minimum = Math.Max(minimum, opening.Top + WallOpening.MinimumSolid + 0.0001f);
            return minimum;
        }

        public PieceData WithOpening(WallOpening opening)
        {
            if (Type != PieceType.Wall) throw new InvalidOperationException("Only walls can host openings.");
            var changed = new WallOpening[openings.Length + 1];
            Array.Copy(openings, changed, openings.Length);
            changed[openings.Length] = opening;
            return new PieceData(Id, Transform, Dimensions, MaterialId, changed, finishes);
        }

        public PieceData ReplaceOpening(WallOpening opening)
        {
            var changed = (WallOpening[])openings.Clone();
            for (int i = 0; i < changed.Length; i++)
                if (changed[i].Id == opening.Id)
                {
                    changed[i] = opening;
                    return new PieceData(Id, Transform, Dimensions, MaterialId, changed, finishes);
                }
            throw new ArgumentException("Opening ID is not in this wall.", nameof(opening));
        }

        public PieceData WithoutOpening(Guid openingId)
        {
            int index = Array.FindIndex(openings, opening => opening.Id == openingId);
            if (index < 0) throw new ArgumentException("Opening ID is not in this wall.", nameof(openingId));
            var changed = new WallOpening[openings.Length - 1];
            if (index > 0) Array.Copy(openings, 0, changed, 0, index);
            if (index < changed.Length) Array.Copy(openings, index + 1, changed, index, changed.Length - index);
            var retained = new List<ArchitecturalFinish>(finishes.Length);
            foreach (ArchitecturalFinish finish in finishes)
                if (finish.OpeningId != openingId) retained.Add(finish);
            return new PieceData(Id, Transform, Dimensions, MaterialId, changed, retained.ToArray());
        }

        public PieceData WithFinish(ArchitecturalFinish finish)
        {
            var changed = new ArchitecturalFinish[finishes.Length+1];
            Array.Copy(finishes,changed,finishes.Length);
            changed[finishes.Length]=finish;
            return new PieceData(Id,Transform,Dimensions,MaterialId,openings,changed,readOnlyOpenings);
        }

        public PieceData WithoutFinish(Guid finishId)
        {
            int index=Array.FindIndex(finishes,item=>item.Id==finishId);
            if (index<0) throw new ArgumentException("Finish ID is not in this piece.",nameof(finishId));
            var changed=new ArchitecturalFinish[finishes.Length-1];
            if (index>0) Array.Copy(finishes,0,changed,0,index);
            if (index<changed.Length) Array.Copy(finishes,index+1,changed,index,changed.Length-index);
            return new PieceData(Id,Transform,Dimensions,MaterialId,openings,changed,readOnlyOpenings);
        }

        private static void ValidateFinishes(PieceId id,PieceDimensions dimensions,
            WallOpening[] openings,ArchitecturalFinish[] finishData)
        {
            for (int i=0;i<finishData.Length;i++)
            {
                ArchitecturalFinish finish=finishData[i];
                if (finish.HostId!=id || finish.Id==Guid.Empty || !finish.MaterialId.IsValid)
                    throw new ArgumentException("Finish host or identity is invalid.",nameof(finishData));
                for (int j=0;j<i;j++)
                    if (finishData[j].Id==finish.Id ||
                        finishData[j].OpeningId==finish.OpeningId && finishData[j].Kind==finish.Kind)
                        throw new ArgumentException("Duplicate finish identity or host feature.",nameof(finishData));
                if (finish.Kind==ArchitecturalFinishKind.RoofEave)
                {
                    if (!dimensions.IsRoof || finish.OpeningId!=Guid.Empty)
                        throw new ArgumentException("Roof eave needs a roof host.",nameof(finishData));
                    continue;
                }
                if (dimensions.Type!=PieceType.Wall)
                    throw new ArgumentException("Opening finish needs a wall host.",nameof(finishData));
                bool found=false;
                foreach (WallOpening opening in openings)
                    if (opening.Id==finish.OpeningId)
                    {
                        found=true;
                        if (finish.Kind==ArchitecturalFinishKind.DoorFrame && opening.Kind!=WallOpeningKind.Passage ||
                            finish.Kind==ArchitecturalFinishKind.WindowFrameSill && opening.Kind!=WallOpeningKind.Window)
                            throw new ArgumentException("Finish kind does not match opening.",nameof(finishData));
                        if (finish.Kind!=ArchitecturalFinishKind.Lintel &&
                            (opening.Width-2f*finish.Profile<0.05f ||
                             opening.Height-2f*finish.Profile<0.05f))
                            throw new ArgumentException("Opening is too small for finish profile.",nameof(finishData));
                        break;
                    }
                if (!found) throw new ArgumentException("Finish opening is absent from host.",nameof(finishData));
            }
        }

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
        public PieceData WithStairDimensions(StairDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithRampDimensions(RampDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithArchDimensions(ArchDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithVaultDimensions(VaultDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithDomeDimensions(DomeDimensions dimensions) => WithDimensions(new PieceDimensions(dimensions));
        public PieceData WithStepCount(int steps) => WithDimensions(Dimensions.WithStepCount(steps));
        public PieceData WithRise(float rise) => WithDimensions(Dimensions.WithRise(rise));
    }
}
