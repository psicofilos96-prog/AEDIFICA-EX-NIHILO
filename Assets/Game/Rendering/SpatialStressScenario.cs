using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Logical-only E2a workload. No views, renderers, or colliders are created.
    public static class SpatialStressScenario
    {
        public const int Seed = 2202;
        public const int ChunkMeters = 64;
        public static readonly int[] Sizes = { 1000, 10000, 50000 };

        public static IEnumerable<PieceData> Generate(int count, int seed)
        {
            if (count < 1 || count > 50000) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++)
            {
                // A dense center and a broad signed grid exercise different query densities.
                bool dense = i % 5 == 0;
                int column = dense ? (i / 5) % 20 : i % 301;
                int row = dense ? (i / 100) % 20 : (i / 301) % 301;
                float x = dense ? (column - 10) * 2.4f : (column - 150) * 17f;
                float z = dense ? (row - 10) * 2.4f : (row - 150) * 17f;
                if (i % 19 == 0) x = (i % 2 == 0 ? -1f : 1f) * ChunkMeters * (1 + i % 9);
                float yaw = (i % 4) * 15f;
                // Seed changes IDs and a bounded vertical distribution, without relying on Random state.
                float y = ((i + (seed & 7)) % 3) * 3f;
                var transform = new PieceTransform(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
                PieceId id = PieceId.Parse(new Guid(seed, (short)(i >> 16), (short)i,
                    0xe2, 0xa0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01).ToString("N"));
                switch (i % 4)
                {
                    case 0: yield return new PieceData(id, transform, new BlockDimensions(i % 23 == 0 ? 90f : 3f, 2f, 4f)); break;
                    case 1: yield return new PieceData(id, transform, new WallDimensions(8f, 3f, 0.3f)); break;
                    case 2: yield return new PieceData(id, transform, new SlabDimensions(6f, 0.3f, 5f)); break;
                    default: yield return new PieceData(id, transform, new ColumnDimensions(0.7f, 4f, 0.7f)); break;
                }
            }
        }

        public static Bounds Region(int probe)
        {
            switch (probe % 3)
            {
                case 0: return new Bounds(Vector3.zero, new Vector3(40f, 20f, 40f)); // dense
                case 1: return new Bounds(new Vector3(6000f, 1f, 6000f), new Vector3(30f, 10f, 30f)); // empty
                default: return new Bounds(new Vector3(-64f, 2f, 64f), new Vector3(12f, 10f, 12f)); // boundary
            }
        }

        public static Vector3 NearbyCenter(int probe) => Region(probe).center;

        public static bool Overlaps(Bounds a, Bounds b) =>
            a.min.x < b.max.x && b.min.x < a.max.x &&
            a.min.y < b.max.y && b.min.y < a.max.y &&
            a.min.z < b.max.z && b.min.z < a.max.z;

        // Validation oracle only; never used by ConstructionWorld or its index.
        public static List<PieceId> LinearRegion(IEnumerable<PieceData> pieces, Bounds area)
        {
            var result = new List<PieceId>();
            foreach (PieceData piece in pieces)
                if (Overlaps(ConstructionChangeSet.WorldBounds(piece), area)) result.Add(piece.Id);
            result.Sort();
            return result;
        }

        public static List<PieceId> LinearNearby(IEnumerable<PieceData> pieces, Vector3 center, float radius)
        {
            var result = new List<PieceId>();
            var broadPhase = new Bounds(center, Vector3.one * (2f * Mathf.Max(radius, 0.0001f)));
            foreach (PieceData piece in pieces)
            {
                Bounds bounds = ConstructionChangeSet.WorldBounds(piece);
                if (Overlaps(bounds, broadPhase) && (bounds.ClosestPoint(center) - center).sqrMagnitude <= radius * radius)
                    result.Add(piece.Id);
            }
            result.Sort();
            return result;
        }

        public static void Validate(ConstructionWorld world, IReadOnlyDictionary<PieceId, PieceData> reference)
        {
            if (world.Count != reference.Count || world.SpatialIndex.IndexedPieceCount != reference.Count)
                throw new InvalidOperationException("World, reference and index piece counts differ.");
            foreach (KeyValuePair<PieceId, PieceData> entry in reference)
            {
                if (!world.TryGet(entry.Key, out PieceData current) ||
                    !current.Transform.Equals(entry.Value.Transform) || !current.Dimensions.Equals(entry.Value.Dimensions) ||
                    current.MaterialId != entry.Value.MaterialId || current.Openings.Count != entry.Value.Openings.Count ||
                    world.SpatialIndex.ChunksFor(entry.Key).Count == 0)
                    throw new InvalidOperationException($"Stale or mismatched piece: {entry.Key}");
            }
            for (int probe = 0; probe < 3; probe++)
            {
                Bounds area = Region(probe);
                EqualIds(LinearRegion(reference.Values, area), world.SpatialIndex.Query(area));
                Vector3 center = NearbyCenter(probe);
                EqualIds(LinearNearby(reference.Values, center, 12f), world.SpatialIndex.QueryNearby(center, 12f));
            }
        }

        private static void EqualIds(IReadOnlyList<PieceId> expected, IReadOnlyList<PieceId> actual)
        {
            if (expected.Count != actual.Count) throw new InvalidOperationException("Spatial query count differs from linear reference.");
            var unique = new HashSet<PieceId>();
            for (int i = 0; i < actual.Count; i++)
                if (expected[i] != actual[i] || !unique.Add(actual[i]))
                    throw new InvalidOperationException("Spatial query differs from reference or returned duplicate IDs.");
        }
    }
}
