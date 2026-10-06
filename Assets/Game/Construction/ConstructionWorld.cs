using System;
using System.Collections.Generic;

namespace Aedifica.Construction
{
    public sealed class ConstructionWorld
    {
        private readonly Dictionary<PieceId, PieceData> pieces = new Dictionary<PieceId, PieceData>();

        public int Count => pieces.Count;
        public IEnumerable<PieceData> Pieces => pieces.Values;

        public bool Add(PieceData piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            return pieces.TryAdd(piece.Id, piece);
        }

        public bool Replace(PieceId id, PieceData replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (!id.IsValid || replacement.Id != id || !pieces.TryGetValue(id, out PieceData current) ||
                current.Type != replacement.Type) return false;
            pieces[id] = replacement;
            return true;
        }

        public bool TryGet(PieceId id, out PieceData piece)
        {
            piece = null;
            return id.IsValid && pieces.TryGetValue(id, out piece);
        }

        public bool Remove(PieceId id) => id.IsValid && pieces.Remove(id);
    }
}
