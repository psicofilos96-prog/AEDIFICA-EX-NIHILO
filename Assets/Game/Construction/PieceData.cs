using System;

namespace Aedifica.Construction
{
    public sealed class PieceData
    {
        public PieceId Id { get; }
        public PieceType Type { get; }
        public PieceTransform Transform { get; }
        public BlockDimensions BlockDimensions { get; }

        public PieceData(PieceId id, PieceTransform transform, BlockDimensions blockDimensions)
            : this(id, PieceType.Block, transform, blockDimensions)
        {
        }

        public PieceData(PieceId id, PieceType type, PieceTransform transform, BlockDimensions blockDimensions)
        {
            if (type != PieceType.Block) throw new ArgumentOutOfRangeException(nameof(type), "Only Block is supported in P0.2.");
            if (!id.IsValid) throw new ArgumentException("A piece must have a valid ID.", nameof(id));
            if (!transform.IsValid) throw new ArgumentException("A piece must have a valid transform.", nameof(transform));
            if (!blockDimensions.IsValid) throw new ArgumentException("A block must have valid dimensions.", nameof(blockDimensions));
            Id = id;
            Type = type;
            Transform = transform;
            BlockDimensions = blockDimensions;
        }
    }
}
