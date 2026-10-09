using System.Collections.Generic;
using Aedifica.Construction;

namespace Aedifica.Rendering
{
    public static class PvtIntegrity
    {
        // PieceData is immutable; references are safe snapshots of the logical state.
        public static Dictionary<PieceId, PieceData> Capture(ConstructionWorld world)
        {
            var snapshot = new Dictionary<PieceId, PieceData>(world.Count);
            foreach (PieceData piece in world.Pieces) snapshot.Add(piece.Id, piece);
            return snapshot;
        }

        public static bool Matches(ConstructionWorld world, IReadOnlyDictionary<PieceId, PieceData> snapshot)
        {
            if (world.Count != snapshot.Count || world.SpatialIndex.IndexedPieceCount != snapshot.Count) return false;
            foreach (KeyValuePair<PieceId, PieceData> item in snapshot)
            {
                if (!world.TryGet(item.Key, out PieceData actual)) return false;
                PieceData prior = item.Value;
                if (prior.Type != actual.Type || !prior.Transform.Equals(actual.Transform) ||
                    !prior.Dimensions.Equals(actual.Dimensions) || prior.MaterialId != actual.MaterialId ||
                    prior.Openings.Count != actual.Openings.Count) return false;
                for (int i = 0; i < prior.Openings.Count; i++)
                    if (!prior.Openings[i].Equals(actual.Openings[i])) return false;
                if (world.SpatialIndex.ChunksFor(item.Key).Count == 0) return false;
            }
            return true;
        }
    }
}
