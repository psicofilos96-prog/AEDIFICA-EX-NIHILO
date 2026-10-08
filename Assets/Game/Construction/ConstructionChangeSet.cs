using System;
using UnityEngine;

namespace Aedifica.Construction
{
    public enum ConstructionOperation { Create, Update, Delete }
    public enum ConstructionChangeStatus { Rejected, NoChange, Changed }

    // One affected piece for E1a. The before/after values are immutable PieceData snapshots;
    // a later batch command can group these records without copying the world.
    public readonly struct ConstructionChangeSet
    {
        public ConstructionOperation Operation { get; }
        public ConstructionChangeStatus Status { get; }
        public PieceId PieceId { get; }
        public PieceData Before { get; }
        public PieceData After { get; }
        public Bounds? BoundsBefore { get; }
        public Bounds? BoundsAfter { get; }
        public bool Changed => Status == ConstructionChangeStatus.Changed;

        internal ConstructionChangeSet(ConstructionOperation operation, ConstructionChangeStatus status,
            PieceId pieceId, PieceData before, PieceData after)
        {
            Operation = operation;
            Status = status;
            PieceId = pieceId;
            Before = before;
            After = after;
            BoundsBefore = before == null ? (Bounds?)null : WorldBounds(before);
            BoundsAfter = after == null ? (Bounds?)null : WorldBounds(after);
        }

        // AABB of the piece's local parameter envelope after rotation; base pivot is Y=0.
        public static Bounds WorldBounds(PieceData piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            Vector3 localExtents = new Vector3(piece.Dimensions.X, piece.Dimensions.Y, piece.Dimensions.Z) * 0.5f;
            Quaternion rotation = piece.Transform.Rotation;
            Vector3 x = rotation * Vector3.right * localExtents.x;
            Vector3 y = rotation * Vector3.up * localExtents.y;
            Vector3 z = rotation * Vector3.forward * localExtents.z;
            Vector3 extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            Vector3 center = piece.Transform.Position + rotation * new Vector3(0f, localExtents.y, 0f);
            return new Bounds(center, extents * 2f);
        }
    }
}
