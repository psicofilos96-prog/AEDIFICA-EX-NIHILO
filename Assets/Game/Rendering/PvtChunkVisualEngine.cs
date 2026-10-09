using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aedifica.Rendering
{
    // PVT-1 experiment: one logical PieceData per ID, one combined visual/collision page per
    // chunk/material/vertex budget. ConstructionWorld remains the sole mutable data authority.
    public sealed class PvtChunkVisualEngine : IDisposable
    {
        private sealed class Page
        {
            public GameObject Object;
            public Mesh Mesh;
            public MeshCollider Collider;
            public MeshRenderer Renderer;
            public readonly List<PieceId> TriangleOwners = new List<PieceId>();
        }

        private sealed class Region
        {
            public readonly HashSet<PieceId> Ids = new HashSet<PieceId>();
            public readonly List<Page> Pages = new List<Page>();
        }

        private readonly ConstructionWorld world;
        private readonly MaterialRegistry materials;
        private readonly Transform parent;
        private readonly Dictionary<ChunkCoordinate, Region> regions = new Dictionary<ChunkCoordinate, Region>();
        private readonly Dictionary<PieceId, ChunkCoordinate> owners = new Dictionary<PieceId, ChunkCoordinate>();
        private readonly Dictionary<MeshCollider, Page> pagesByCollider = new Dictionary<MeshCollider, Page>();
        private readonly Dictionary<PieceId, PieceView> editViews = new Dictionary<PieceId, PieceView>();
        private readonly HashSet<PieceId> extracted = new HashSet<PieceId>();
        private readonly SortedSet<ChunkCoordinate> dirty = new SortedSet<ChunkCoordinate>();
        private readonly float chunkSize;
        private readonly int maxVertices;
        private bool disposed;

        public int PieceCount => owners.Count;
        public int RegionCount => regions.Count;
        public int PageCount => pagesByCollider.Count;
        public int ExtractedCount => extracted.Count;
        public int DirtyRegionCount => dirty.Count;
        public int TriangleCount { get; private set; }
        public int VertexCount { get; private set; }
        public int RebuiltRegions { get; private set; }
        public float ChunkSize => chunkSize;

        public PvtChunkVisualEngine(ConstructionWorld world, MaterialRegistry materials, Transform parent,
            float chunkSize = 64f, int maxVertices = 12000)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.materials = materials ?? throw new ArgumentNullException(nameof(materials));
            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            if (chunkSize <= 0f || float.IsNaN(chunkSize) || float.IsInfinity(chunkSize))
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            if (maxVertices < 128 || maxVertices > 65000) throw new ArgumentOutOfRangeException(nameof(maxVertices));
            this.chunkSize = chunkSize;
            this.maxVertices = maxVertices;
            foreach (PieceData piece in world.Pieces) Add(piece);
            world.Changed += OnChanged;
        }

        public bool Contains(PieceId id) => owners.ContainsKey(id);
        public ChunkCoordinate OwnerOf(PieceId id) => owners[id];

        private ChunkCoordinate Coordinate(PieceData piece)
        {
            Vector3 center = ConstructionChangeSet.WorldBounds(piece).center;
            return new ChunkCoordinate(Cell(center.x), 0, Cell(center.z));
        }

        private int Cell(float value)
        {
            double coordinate = Math.Floor((double)value / chunkSize);
            if (double.IsNaN(coordinate) || coordinate < int.MinValue || coordinate > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));
            return (int)coordinate;
        }

        private void Add(PieceData piece)
        {
            ChunkCoordinate coordinate = Coordinate(piece);
            if (!regions.TryGetValue(coordinate, out Region region))
                regions.Add(coordinate, region = new Region());
            region.Ids.Add(piece.Id);
            owners.Add(piece.Id, coordinate);
            dirty.Add(coordinate);
        }

        private void Remove(PieceId id)
        {
            ChunkCoordinate coordinate = owners[id];
            regions[coordinate].Ids.Remove(id);
            owners.Remove(id);
            dirty.Add(coordinate);
        }

        private void OnChanged(ConstructionChangeSet change)
        {
            if (!change.Changed || disposed) return;
            if (change.Before != null) Remove(change.PieceId);
            if (change.After != null) Add(change.After);
            if (editViews.TryGetValue(change.PieceId, out PieceView view))
            {
                if (change.After != null) view.Refresh(change.After);
                else
                {
                    editViews.Remove(change.PieceId);
                    extracted.Remove(change.PieceId);
                    UnityEngine.Object.Destroy(view.gameObject);
                }
            }
        }

        // One selected piece can be edited/highlighted through its normal PieceView. It is
        // omitted from the combined page until EndEdit, without changing its logical ID.
        public PieceView BeginEdit(PieceId id)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PvtChunkVisualEngine));
            if (editViews.TryGetValue(id, out PieceView existing)) return existing;
            if (!world.TryGet(id, out PieceData piece)) throw new ArgumentException("Unknown PieceId.", nameof(id));
            var go = new GameObject("PVT editing " + id);
            go.transform.SetParent(parent, false);
            try
            {
                PieceView view = go.AddComponent<PieceView>();
                view.Initialize(piece, materials);
                view.SetSelected(true);
                extracted.Add(id);
                editViews.Add(id, view);
                dirty.Add(owners[id]);
                RebuildDirty();
                return view;
            }
            catch
            {
                extracted.Remove(id);
                editViews.Remove(id);
                UnityEngine.Object.Destroy(go);
                if (owners.TryGetValue(id, out ChunkCoordinate coordinate)) dirty.Add(coordinate);
                throw;
            }
        }

        public void EndEdit(PieceId id)
        {
            if (!editViews.TryGetValue(id, out PieceView view)) return;
            extracted.Remove(id);
            try
            {
                if (owners.TryGetValue(id, out ChunkCoordinate coordinate))
                {
                    dirty.Add(coordinate);
                    RebuildDirty();
                }
            }
            catch { extracted.Add(id); throw; }
            editViews.Remove(id);
            UnityEngine.Object.Destroy(view.gameObject);
        }

        // A bounded caller can rebuild a few regions each frame. No global mesh regeneration.
        public int RebuildDirty(int maxRegions = int.MaxValue)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PvtChunkVisualEngine));
            if (maxRegions < 1) throw new ArgumentOutOfRangeException(nameof(maxRegions));
            int completed = 0;
            while (dirty.Count > 0 && completed < maxRegions)
            {
                ChunkCoordinate coordinate = dirty.Min;
                Rebuild(coordinate);
                dirty.Remove(coordinate);
                completed++;
                RebuiltRegions++;
            }
            return completed;
        }

        private void Rebuild(ChunkCoordinate coordinate)
        {
            Region region = regions[coordinate];
            var replacement = new List<Page>();
            try
            {
                if (region.Ids.Count > 0)
                {
                    var ordered = new List<PieceId>(region.Ids);
                    ordered.Sort();
                    var groups = new SortedDictionary<string, List<PieceData>>(StringComparer.Ordinal);
                    var materialByKey = new Dictionary<string, Material>();
                    foreach (PieceId id in ordered)
                    {
                        if (extracted.Contains(id)) continue;
                        if (!world.TryGet(id, out PieceData piece)) throw new InvalidOperationException("Missing logical piece.");
                        string key = piece.MaterialId.ToString();
                        if (!groups.TryGetValue(key, out List<PieceData> group))
                        {
                            groups.Add(key, group = new List<PieceData>());
                            materialByKey.Add(key, materials.Resolve(piece.MaterialId));
                        }
                        group.Add(piece);
                    }
                    foreach (KeyValuePair<string, List<PieceData>> group in groups)
                        BuildGroup(coordinate, group.Value, materialByKey[group.Key], replacement);
                }
            }
            catch
            {
                foreach (Page page in replacement) DestroyPage(page);
                throw;
            }
            foreach (Page old in region.Pages)
            {
                pagesByCollider.Remove(old.Collider);
                TriangleCount -= old.TriangleOwners.Count;
                VertexCount -= old.Mesh.vertexCount;
                DestroyPage(old);
            }
            region.Pages.Clear();
            region.Pages.AddRange(replacement);
            foreach (Page page in replacement)
            {
                pagesByCollider.Add(page.Collider, page);
                TriangleCount += page.TriangleOwners.Count;
                VertexCount += page.Mesh.vertexCount;
            }
            if (region.Ids.Count == 0) regions.Remove(coordinate);
        }

        private void BuildGroup(ChunkCoordinate coordinate, List<PieceData> group, Material material, List<Page> output)
        {
            int initialCapacity = Math.Min(maxVertices, 1024);
            var vertices = new List<Vector3>(initialCapacity);
            var normals = new List<Vector3>(initialCapacity);
            var uvs = new List<Vector2>(initialCapacity);
            var triangles = new List<int>(initialCapacity * 2);
            var ownerIds = new List<PieceId>(initialCapacity);
            foreach (PieceData piece in group)
            {
                BlockGeometry geometry = Geometry(piece);
                if (vertices.Count > 0 && vertices.Count + geometry.Vertices.Length > maxVertices)
                {
                    output.Add(BuildPage(coordinate, material, vertices, normals, uvs, triangles, ownerIds));
                    vertices.Clear(); normals.Clear(); uvs.Clear(); triangles.Clear(); ownerIds.Clear();
                }
                if (geometry.Vertices.Length > maxVertices)
                    throw new InvalidOperationException($"Piece {piece.Id} exceeds a PVT mesh page vertex budget.");
                int offset = vertices.Count;
                Quaternion rotation = piece.Transform.Rotation;
                Vector3 position = piece.Transform.Position;
                for (int i = 0; i < geometry.Vertices.Length; i++)
                {
                    vertices.Add(position + rotation * geometry.Vertices[i]);
                    normals.Add(rotation * geometry.Normals[i]);
                    uvs.Add(geometry.UVs[i]);
                }
                foreach (int triangle in geometry.Triangles) triangles.Add(offset + triangle);
                for (int i = 0; i < geometry.Triangles.Length / 3; i++) ownerIds.Add(piece.Id);
            }
            if (vertices.Count > 0)
                output.Add(BuildPage(coordinate, material, vertices, normals, uvs, triangles, ownerIds));
        }

        public static BlockGeometry Geometry(PieceData piece) =>
            piece.Type == PieceType.Wall && piece.Openings.Count > 0
                ? WallOpeningGeometryGenerator.Generate(piece)
                : piece.Dimensions.IsSlopedRoof ? RoofGeometryGenerator.Generate(piece.Dimensions)
                : piece.Dimensions.IsCurved ? CurvedGeometryGenerator.Generate(piece.Dimensions)
                : piece.Dimensions.IsStair || piece.Dimensions.IsRamp
                    ? CirculationGeometryGenerator.Generate(piece.Dimensions)
                    : BlockGeometryGenerator.GeneratePiece(piece.Dimensions);

        private Page BuildPage(ChunkCoordinate coordinate, Material material, List<Vector3> vertices,
            List<Vector3> normals, List<Vector2> uvs, List<int> triangles, List<PieceId> ownersByTriangle)
        {
            var mesh = new Mesh { name = $"PVT region {coordinate}", indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var gameObject = new GameObject($"PVT {coordinate} {material.name}");
            gameObject.transform.SetParent(parent, false);
            try
            {
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                MeshCollider collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                var page = new Page { Object = gameObject, Mesh = mesh, Renderer = renderer, Collider = collider };
                page.TriangleOwners.AddRange(ownersByTriangle);
                return page;
            }
            catch { UnityEngine.Object.Destroy(gameObject); UnityEngine.Object.Destroy(mesh); throw; }
        }

        public bool TryResolve(RaycastHit hit, out PieceId id)
        {
            id = default;
            if (hit.collider != null)
            {
                PieceView edit = hit.collider.GetComponent<PieceView>();
                if (edit != null && editViews.TryGetValue(edit.Id, out PieceView current) && current == edit)
                {
                    id = edit.Id;
                    return world.TryGet(id, out _);
                }
            }
            if (hit.collider is MeshCollider collider && pagesByCollider.TryGetValue(collider, out Page page) &&
                hit.triangleIndex >= 0 && hit.triangleIndex < page.TriangleOwners.Count)
            {
                id = page.TriangleOwners[hit.triangleIndex];
                return world.TryGet(id, out _);
            }
            return false;
        }

        public bool TryPick(Ray ray, float distance, out PieceId id)
        {
            id = default;
            RaycastHit[] hits = Physics.RaycastAll(ray, distance);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
                if (TryResolve(hit, out id)) return true;
            return false;
        }

        public int VisibleRendererCount()
        {
            int count = 0;
            foreach (Region region in regions.Values)
                foreach (Page page in region.Pages)
                    if (page.Renderer != null && page.Renderer.isVisible) count++;
            return count;
        }

        private static void DestroyPage(Page page)
        {
            if (page.Collider != null) page.Collider.sharedMesh = null;
            if (page.Object != null) UnityEngine.Object.Destroy(page.Object);
            if (page.Mesh != null) UnityEngine.Object.Destroy(page.Mesh);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            world.Changed -= OnChanged;
            foreach (PieceView edit in editViews.Values)
                if (edit != null) UnityEngine.Object.Destroy(edit.gameObject);
            foreach (Region region in regions.Values)
                foreach (Page page in region.Pages) DestroyPage(page);
            regions.Clear(); owners.Clear(); pagesByCollider.Clear(); dirty.Clear();
            editViews.Clear(); extracted.Clear();
            TriangleCount = 0; VertexCount = 0;
        }
    }
}
