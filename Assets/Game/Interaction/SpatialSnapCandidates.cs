using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    // Conservative broad phase for the raw piece's features in Move and Face Resize.
    public static class SpatialSnapCandidates
    {
        private const float BoundaryMargin = 0.001f;

        public static IReadOnlyList<PieceData> ForMove(ConstructionWorld world, PieceData moving, SnapSettings settings)
            => NearRawEnvelope(world, moving, settings);

        public static IReadOnlyList<PieceData> ForFaceResize(ConstructionWorld world, PieceData raw,
            ManipulationSession session, SnapSettings settings)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.Mode != ManipulationMode.Resize || session.ResizeBehavior != ResizeMode.Face)
                throw new ArgumentException("A Face Resize session is required.", nameof(session));
            return NearRawEnvelope(world, raw, settings);
        }

        private static IReadOnlyList<PieceData> NearRawEnvelope(ConstructionWorld world, PieceData moving,
            SnapSettings settings)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (moving == null) throw new ArgumentNullException(nameof(moving));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.ValidateGeometric();
            Bounds search = ConstructionChangeSet.WorldBounds(moving);
            // SnapResolver compares the ORIGINAL geometric correction with ReleaseDistance
            // for both Move and Face Resize, before projecting a Face correction onto its axis.
            // A valid source and target feature therefore each contain a point at most this
            // distance apart. Their indexed world envelopes must intersect this expanded AABB.
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
