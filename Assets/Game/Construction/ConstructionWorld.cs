using System;
using System.Collections.Generic;

namespace Aedifica.Construction
{
    public sealed class ConstructionWorld
    {
        private readonly Dictionary<PieceId, PieceData> pieces = new Dictionary<PieceId, PieceData>();
        public ConstructionSpatialIndex SpatialIndex { get; }

        public ConstructionWorld(float chunkSize = 64f) => SpatialIndex = new ConstructionSpatialIndex(chunkSize);

        public int Count => pieces.Count;
        public IEnumerable<PieceData> Pieces => pieces.Values;
        public event Action<ConstructionChangeSet> Changed;
        public event Action<ConstructionInvalidation> ChunksInvalidated;

        public ConstructionChangeSet Create(PieceData piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (pieces.TryGetValue(piece.Id, out PieceData existing))
                return new ConstructionChangeSet(ConstructionOperation.Create, ConstructionChangeStatus.Rejected,
                    piece.Id, existing, null);
            var change = new ConstructionChangeSet(ConstructionOperation.Create, ConstructionChangeStatus.Changed,
                piece.Id, null, piece);
            HashSet<ChunkCoordinate> coverage = SpatialIndex.Prepare(change.BoundsAfter.Value);
            pieces.Add(piece.Id, piece);
            SpatialIndex.Insert(piece.Id, change.BoundsAfter.Value, coverage);
            Changed?.Invoke(change);
            ChunksInvalidated?.Invoke(new ConstructionInvalidation(change, null, coverage));
            return change;
        }

        public ConstructionChangeSet Update(PieceId id, PieceData replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            PieceData current = null;
            if (!id.IsValid || replacement.Id != id || !pieces.TryGetValue(id, out current) ||
                current.Type != replacement.Type)
                return new ConstructionChangeSet(ConstructionOperation.Update, ConstructionChangeStatus.Rejected,
                    id, current, null);
            if (SameState(current, replacement))
                return new ConstructionChangeSet(ConstructionOperation.Update, ConstructionChangeStatus.NoChange,
                    id, current, current);
            var change = new ConstructionChangeSet(ConstructionOperation.Update, ConstructionChangeStatus.Changed,
                id, current, replacement);
            HashSet<ChunkCoordinate> coverage = SpatialIndex.Prepare(change.BoundsAfter.Value);
            IReadOnlyList<ChunkCoordinate> before = SpatialIndex.ChunksFor(id);
            pieces[id] = replacement;
            SpatialIndex.Replace(id, change.BoundsAfter.Value, coverage);
            Changed?.Invoke(change);
            ChunksInvalidated?.Invoke(new ConstructionInvalidation(change, before, coverage));
            return change;
        }

        public ConstructionChangeSet Delete(PieceId id)
        {
            if (!id.IsValid || !pieces.TryGetValue(id, out PieceData current))
                return new ConstructionChangeSet(ConstructionOperation.Delete, ConstructionChangeStatus.Rejected,
                    id, null, null);
            var change = new ConstructionChangeSet(ConstructionOperation.Delete, ConstructionChangeStatus.Changed,
                id, current, null);
            IReadOnlyList<ChunkCoordinate> before = SpatialIndex.ChunksFor(id);
            pieces.Remove(id);
            SpatialIndex.Remove(id);
            Changed?.Invoke(change);
            ChunksInvalidated?.Invoke(new ConstructionInvalidation(change, before, null));
            return change;
        }

        internal static bool SameState(PieceData a, PieceData b)
        {
            if (a == null || b == null || a.Id != b.Id || a.Type != b.Type ||
                !a.Transform.Equals(b.Transform) || !a.Dimensions.Equals(b.Dimensions) ||
                a.MaterialId != b.MaterialId || a.Openings.Count != b.Openings.Count) return false;
            for (int i = 0; i < a.Openings.Count; i++)
                if (!a.Openings[i].Equals(b.Openings[i])) return false;
            return true;
        }

        public bool Add(PieceData piece)
        {
            return Create(piece).Changed;
        }

        public bool Replace(PieceId id, PieceData replacement)
        {
            return Update(id, replacement).Status != ConstructionChangeStatus.Rejected;
        }

        public bool TryGet(PieceId id, out PieceData piece)
        {
            piece = null;
            return id.IsValid && pieces.TryGetValue(id, out piece);
        }

        public bool Remove(PieceId id) => Delete(id).Changed;
    }
}
