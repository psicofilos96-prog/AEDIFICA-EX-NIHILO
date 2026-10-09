using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    // Broad phase for Move only. Face Resize retains the original full-scan path.
    public static class SpatialSnapCandidates
    {
        private const float BoundaryMargin = 0.001f;

        public static IReadOnlyList<PieceData> ForMove(ConstructionWorld world, PieceData moving, SnapSettings settings)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (moving == null) throw new ArgumentNullException(nameof(moving));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ValidateGeometric();
            Bounds search = ConstructionChangeSet.WorldBounds(moving);
            // Every Move correction accepted by SnapResolver is <= ReleaseDistance.
            // The margin includes half-open boundaries and grows with float ULP at large coordinates.
            Vector3 center = search.center, extent = search.extents;
            float magnitude = Mathf.Max(Mathf.Max(Mathf.Abs(center.x) + extent.x,
                Mathf.Abs(center.y) + extent.y), Mathf.Abs(center.z) + extent.z);
            float margin = BoundaryMargin + magnitude * 0.000001f;
            search.Expand(2f * (settings.ReleaseDistance + margin));
            IReadOnlyList<PieceId> ids = world.SpatialIndex.Query(search);
            var candidates = new List<PieceData>(ids.Count);
            foreach (PieceId id in ids)
                if (id != moving.Id && world.TryGet(id, out PieceData piece)) candidates.Add(piece);
            return candidates;
        }
    }
}
