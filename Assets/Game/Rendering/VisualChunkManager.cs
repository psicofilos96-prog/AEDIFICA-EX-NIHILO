using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Experimental visual layer. A PieceId has one view, even when its bounds cover many cells.
    // Renderer culling never disables GameObjects or colliders used by editing and selection.
    public sealed class VisualChunkManager : IDisposable
    {
        private sealed class Entry
        {
            public PieceView View;
            public MeshRenderer Renderer;
            public Bounds Bounds;
            public readonly List<ChunkCoordinate> Cells = new List<ChunkCoordinate>();
            public bool Enabled = true;
        }

        private sealed class Cell
        {
            public readonly HashSet<PieceId> Ids = new HashSet<PieceId>();
            public Bounds Envelope;
            public bool Visible = true;
            public float LastVisibleTime;
        }

        private readonly Dictionary<PieceId, Entry> entries = new Dictionary<PieceId, Entry>();
        private readonly Dictionary<ChunkCoordinate, Cell> cells = new Dictionary<ChunkCoordinate, Cell>();
        private readonly Dictionary<ChunkCoordinate, Transform> parents = new Dictionary<ChunkCoordinate, Transform>();
        private readonly HashSet<PieceId> desired = new HashSet<PieceId>();
        private readonly List<ChunkCoordinate> affected = new List<ChunkCoordinate>();
        private readonly float chunkSize;
        private readonly float releaseSeconds;
        private readonly Transform root;
        private bool culling;

        public int PieceCount => entries.Count;
        public int ChunkCount => cells.Count;
        public int ActiveChunkCount { get; private set; }
        public int VisibleChunkCount { get; private set; }
        public int ActiveRendererCount { get; private set; }
        public long StateChanges { get; private set; }
        public long DiscardedChunks { get; private set; }
        public float ChunkSize => chunkSize;

        public VisualChunkManager(float chunkSize = 64f, float releaseSeconds = 0.2f, Transform root = null)
        {
            if (chunkSize <= 0f || float.IsNaN(chunkSize) || float.IsInfinity(chunkSize))
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            if (releaseSeconds < 0f || float.IsNaN(releaseSeconds) || float.IsInfinity(releaseSeconds))
                throw new ArgumentOutOfRangeException(nameof(releaseSeconds));
            this.chunkSize = chunkSize;
            this.releaseSeconds = releaseSeconds;
            this.root = root;
        }

        public bool Contains(PieceId id) => entries.ContainsKey(id);
        public bool IsRendererEnabled(PieceId id) => entries.TryGetValue(id, out Entry e) && e.Renderer.enabled;
        public IReadOnlyList<ChunkCoordinate> CellsFor(PieceId id) => entries.TryGetValue(id, out Entry e)
            ? e.Cells.AsReadOnly() : Array.Empty<ChunkCoordinate>();
        public IReadOnlyList<ChunkCoordinate> CoveredCells(Bounds bounds)
        {
            var result = new List<ChunkCoordinate>();
            Coverage(bounds, result);
            return result.AsReadOnly();
        }

        public void Register(PieceData piece, PieceView view)
        {
            if (piece == null || view == null || view.Id != piece.Id || entries.ContainsKey(piece.Id))
                throw new ArgumentException("A unique matching PieceData/PieceView is required.");
            var entry = new Entry { View = view, Renderer = view.GetComponent<MeshRenderer>(),
                Bounds = ConstructionChangeSet.WorldBounds(piece) };
            if (entry.Renderer == null) throw new ArgumentException("PieceView has no MeshRenderer.");
            Coverage(entry.Bounds, entry.Cells);
            entries.Add(piece.Id, entry);
            foreach (ChunkCoordinate coordinate in entry.Cells)
            {
                if (!cells.TryGetValue(coordinate, out Cell cell)) cells.Add(coordinate, cell = new Cell());
                cell.Ids.Add(piece.Id);
                cell.Envelope = cell.Ids.Count == 1 ? entry.Bounds : Union(cell.Envelope, entry.Bounds);
            }
            Parent(entry);
            entry.Renderer.enabled = true;
        }

        public void Update(PieceData piece)
        {
            if (piece == null || !entries.TryGetValue(piece.Id, out Entry entry))
                throw new ArgumentException("Piece is not registered.");
            Bounds updatedBounds = ConstructionChangeSet.WorldBounds(piece);
            var updatedCells = new List<ChunkCoordinate>();
            Coverage(updatedBounds, updatedCells);
            ChunkCoordinate previousAnchor = entry.Cells[0];
            foreach (ChunkCoordinate coordinate in entry.Cells)
                if (!updatedCells.Contains(coordinate)) RemoveFromCell(coordinate, piece.Id);
            entry.Bounds = updatedBounds;
            foreach (ChunkCoordinate coordinate in updatedCells)
            {
                if (!cells.TryGetValue(coordinate, out Cell cell)) cells.Add(coordinate, cell = new Cell());
                cell.Ids.Add(piece.Id); // existing membership is unchanged
                RebuildEnvelope(cell);
            }
            entry.Cells.Clear();
            entry.Cells.AddRange(updatedCells);
            entry.View.Refresh(piece);
            if (!previousAnchor.Equals(entry.Cells[0])) Parent(entry);
        }

        public bool Remove(PieceId id)
        {
            if (!entries.TryGetValue(id, out Entry entry)) return false;
            RemoveMembership(id, entry);
            entry.Renderer.enabled = true;
            if (root != null && entry.View != null) entry.View.transform.SetParent(root, true);
            entries.Remove(id);
            return true;
        }

        private void RemoveMembership(PieceId id, Entry entry)
        {
            affected.Clear();
            affected.AddRange(entry.Cells);
            foreach (ChunkCoordinate coordinate in affected)
                RemoveFromCell(coordinate, id);
            entry.Cells.Clear();
        }

        private void RemoveFromCell(ChunkCoordinate coordinate, PieceId id)
        {
            Cell cell = cells[coordinate];
            cell.Ids.Remove(id);
            if (cell.Ids.Count == 0)
            {
                cells.Remove(coordinate); DiscardedChunks++;
                if (parents.TryGetValue(coordinate, out Transform parent))
                {
                    parents.Remove(coordinate);
                    UnityEngine.Object.Destroy(parent.gameObject);
                }
            }
            else RebuildEnvelope(cell);
        }

        private void RebuildEnvelope(Cell cell)
        {
            bool first = true;
            foreach (PieceId member in cell.Ids)
            {
                Bounds bounds = entries[member].Bounds;
                cell.Envelope = first ? bounds : Union(cell.Envelope, bounds);
                first = false;
            }
        }

        public void SetCulling(bool enabled)
        {
            culling = enabled;
            if (enabled) return;
            foreach (Entry entry in entries.Values) SetRenderer(entry, true);
            ActiveRendererCount = entries.Count;
            ActiveChunkCount = cells.Count;
            VisibleChunkCount = 0; // no frustum evaluation in this mode
        }

        // Conservative: each cell envelope contains the FULL bounds of every member, including
        // pieces extending outside this cell. This can overdraw but cannot hide a visible piece.
        public void Evaluate(Camera camera, float now)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!culling) return;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            desired.Clear();
            ActiveChunkCount = 0;
            VisibleChunkCount = 0;
            foreach (Cell cell in cells.Values)
            {
                bool intersects = GeometryUtility.TestPlanesAABB(planes, cell.Envelope);
                if (intersects) { cell.LastVisibleTime = now; VisibleChunkCount++; }
                cell.Visible = intersects || now - cell.LastVisibleTime <= releaseSeconds;
                if (!cell.Visible) continue;
                ActiveChunkCount++;
                desired.UnionWith(cell.Ids);
            }
            ActiveRendererCount = 0;
            foreach (KeyValuePair<PieceId, Entry> pair in entries)
            {
                bool visible = desired.Contains(pair.Key);
                SetRenderer(pair.Value, visible);
                if (visible) ActiveRendererCount++;
            }
        }

        public void Dispose()
        {
            SetCulling(false);
            if (root != null)
                foreach (Entry entry in entries.Values)
                    if (entry.View != null) entry.View.transform.SetParent(root, true);
            foreach (Transform parent in parents.Values)
                if (parent != null) UnityEngine.Object.Destroy(parent.gameObject);
            parents.Clear();
            entries.Clear(); cells.Clear(); desired.Clear(); affected.Clear();
        }

        private void Parent(Entry entry)
        {
            if (root == null || entry.Cells.Count == 0) return;
            ChunkCoordinate anchor = entry.Cells[0];
            if (!parents.TryGetValue(anchor, out Transform parent))
            {
                var group = new GameObject($"Visual Chunk {anchor}");
                group.transform.SetParent(root, false);
                parents.Add(anchor, parent = group.transform);
            }
            entry.View.transform.SetParent(parent, true);
        }

        private void SetRenderer(Entry entry, bool enabled)
        {
            if (entry.Enabled == enabled) return;
            entry.Enabled = enabled;
            if (entry.Renderer != null) entry.Renderer.enabled = enabled;
            StateChanges++;
        }

        private void Coverage(Bounds bounds, List<ChunkCoordinate> output)
        {
            int x0 = CellAt(bounds.min.x), z0 = CellAt(bounds.min.z);
            int x1 = LastCell(bounds.max.x, x0), z1 = LastCell(bounds.max.z, z0);
            long count = ((long)x1 - x0 + 1) * ((long)z1 - z0 + 1);
            if (count > 100000) throw new ArgumentOutOfRangeException(nameof(bounds), "Piece covers too many visual chunks.");
            for (long x = x0; x <= x1; x++)
                for (long z = z0; z <= z1; z++) output.Add(new ChunkCoordinate((int)x, 0, (int)z));
        }

        private int CellAt(float position)
        {
            double value = Math.Floor((double)position / chunkSize);
            if (double.IsNaN(value) || value < int.MinValue || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(position));
            return (int)value;
        }

        private int LastCell(float exclusiveMax, int first)
        {
            double value = Math.Ceiling((double)exclusiveMax / chunkSize) - 1;
            if (value < first) return first;
            if (double.IsNaN(value) || value > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)value;
        }

        private static Bounds Union(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
    }
}
