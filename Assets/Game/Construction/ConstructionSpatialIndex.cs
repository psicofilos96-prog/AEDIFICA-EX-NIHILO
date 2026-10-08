using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aedifica.Construction
{
    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>, IComparable<ChunkCoordinate>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public ChunkCoordinate(int x, int y, int z) { X = x; Y = y; Z = z; }
        public bool Equals(ChunkCoordinate other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is ChunkCoordinate other && Equals(other);
        public override int GetHashCode() { unchecked { return ((X * 397) ^ Y) * 397 ^ Z; } }
        public int CompareTo(ChunkCoordinate other)
        {
            int x = X.CompareTo(other.X);
            if (x != 0) return x;
            int y = Y.CompareTo(other.Y);
            return y != 0 ? y : Z.CompareTo(other.Z);
        }
        public override string ToString() => $"({X},{Y},{Z})";
    }

    public readonly struct ConstructionInvalidation
    {
        public ConstructionChangeSet Change { get; }
        public IReadOnlyList<ChunkCoordinate> Chunks { get; }

        internal ConstructionInvalidation(ConstructionChangeSet change,
            IEnumerable<ChunkCoordinate> before, IEnumerable<ChunkCoordinate> after)
        {
            Change = change;
            var affected = new HashSet<ChunkCoordinate>();
            if (before != null) affected.UnionWith(before);
            if (after != null) affected.UnionWith(after);
            var sorted = new List<ChunkCoordinate>(affected);
            sorted.Sort();
            Chunks = sorted.AsReadOnly();
        }
    }

    // Logical membership only: a piece owns one ID and can be referenced by several cells.
    public sealed class ConstructionSpatialIndex
    {
        private readonly Dictionary<ChunkCoordinate, HashSet<PieceId>> cells = new Dictionary<ChunkCoordinate, HashSet<PieceId>>();
        private readonly Dictionary<PieceId, HashSet<ChunkCoordinate>> memberships = new Dictionary<PieceId, HashSet<ChunkCoordinate>>();
        private readonly Dictionary<PieceId, Bounds> bounds = new Dictionary<PieceId, Bounds>();
        public float ChunkSize { get; }
        public int IndexedPieceCount => memberships.Count;
        public int OccupiedChunkCount => cells.Count;

        public ConstructionSpatialIndex(float chunkSize = 64f)
        {
            if (!(chunkSize > 0f) || float.IsNaN(chunkSize) || float.IsInfinity(chunkSize))
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            ChunkSize = chunkSize;
        }

        internal HashSet<ChunkCoordinate> Prepare(Bounds area)
        {
            Vector3 min = area.min, max = area.max;
            int x0 = Cell(min.x), y0 = Cell(min.y), z0 = Cell(min.z);
            int x1 = LastCell(max.x, x0), y1 = LastCell(max.y, y0), z1 = LastCell(max.z, z0);
            var result = new HashSet<ChunkCoordinate>();
            for (long x = x0; x <= x1; x++)
            for (long y = y0; y <= y1; y++)
            for (long z = z0; z <= z1; z++) result.Add(new ChunkCoordinate((int)x, (int)y, (int)z));
            return result;
        }

        private int Cell(double coordinate)
        {
            double value = Math.Floor(coordinate / ChunkSize);
            if (double.IsNaN(value) || value < int.MinValue || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(coordinate), "World coordinate exceeds the spatial index range.");
            return (int)value;
        }

        private int LastCell(double exclusiveMax, int first)
        {
            double value = Math.Ceiling(exclusiveMax / ChunkSize) - 1d;
            if (value < first) return first;
            if (double.IsNaN(value) || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "World coordinate exceeds the spatial index range.");
            return (int)value;
        }

        internal void Insert(PieceId id, Bounds area, HashSet<ChunkCoordinate> coverage)
        {
            memberships.Add(id, coverage);
            bounds.Add(id, area);
            foreach (ChunkCoordinate cell in coverage)
            {
                if (!cells.TryGetValue(cell, out HashSet<PieceId> ids)) cells.Add(cell, ids = new HashSet<PieceId>());
                ids.Add(id);
            }
        }

        internal void Replace(PieceId id, Bounds area, HashSet<ChunkCoordinate> coverage)
        {
            HashSet<ChunkCoordinate> old = memberships[id];
            foreach (ChunkCoordinate cell in old)
                if (!coverage.Contains(cell)) RemoveFromCell(cell, id);
            foreach (ChunkCoordinate cell in coverage)
                if (!old.Contains(cell))
                {
                    if (!cells.TryGetValue(cell, out HashSet<PieceId> ids)) cells.Add(cell, ids = new HashSet<PieceId>());
                    ids.Add(id);
                }
            memberships[id] = coverage;
            bounds[id] = area;
        }

        internal void Remove(PieceId id)
        {
            foreach (ChunkCoordinate cell in memberships[id]) RemoveFromCell(cell, id);
            memberships.Remove(id);
            bounds.Remove(id);
        }

        private void RemoveFromCell(ChunkCoordinate cell, PieceId id)
        {
            HashSet<PieceId> ids = cells[cell];
            ids.Remove(id);
            if (ids.Count == 0) cells.Remove(cell);
        }

        public IReadOnlyList<ChunkCoordinate> ChunksFor(PieceId id)
        {
            if (!memberships.TryGetValue(id, out HashSet<ChunkCoordinate> found)) return Array.Empty<ChunkCoordinate>();
            var result = new List<ChunkCoordinate>(found);
            result.Sort();
            return result.AsReadOnly();
        }

        public IReadOnlyList<PieceId> Query(Bounds area)
        {
            var found = new HashSet<PieceId>();
            foreach (ChunkCoordinate cell in Prepare(area))
                if (cells.TryGetValue(cell, out HashSet<PieceId> ids))
                    foreach (PieceId id in ids)
                        if (Overlaps(bounds[id], area)) found.Add(id);
            var result = new List<PieceId>(found);
            result.Sort();
            return result;
        }

        public IReadOnlyList<PieceId> QueryNearby(Vector3 center, float radius)
        {
            if (radius < 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            float span = Mathf.Max(radius, 0.0001f);
            var area = new Bounds(center, Vector3.one * (2f * span));
            IReadOnlyList<PieceId> candidates = Query(area);
            var result = new List<PieceId>();
            foreach (PieceId id in candidates)
            {
                Vector3 nearest = bounds[id].ClosestPoint(center);
                if ((nearest - center).sqrMagnitude <= radius * radius) result.Add(id);
            }
            return result;
        }

        // Positive-volume overlap; touching a half-open chunk boundary alone is not intersection.
        private static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x && b.min.x < a.max.x &&
            a.min.y < b.max.y && b.min.y < a.max.y && a.min.z < b.max.z && b.min.z < a.max.z;
    }
}
