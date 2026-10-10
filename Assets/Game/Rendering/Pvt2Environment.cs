using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Deterministic 1 km landscape. Vegetation cells are created/destroyed around the camera;
    // the terrain, road and river are persistent. Buildings retain their own logical world.
    public sealed class Pvt2Environment : IDisposable
    {
        public const int CellsPerSide = 16;
        public const float CellSize = 62.5f;
        private readonly Transform parent;
        private readonly int seed;
        private readonly Dictionary<Vector2Int, GameObject> loaded = new Dictionary<Vector2Int, GameObject>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Material bark, foliage, grass, road, water;
        private readonly Mesh treeMesh;
        private GameObject root;
        private int density;
        private int lastRadius = -1;
        private Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
        private bool disposed;

        public int LoadedRegions => loaded.Count;
        public int VegetationRenderers { get; private set; }
        public int TotalRegionTransitions { get; private set; }
        public float LastTransitionMs { get; private set; }
        public bool IsLoaded(Vector2Int cell) => loaded.ContainsKey(cell);

        public Pvt2Environment(Transform parent, int seed, Material terrainMaterial,
            Material barkMaterial, Material foliageMaterial, Material grassMaterial,
            Material roadMaterial, Material waterMaterial)
        {
            this.parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            this.seed = seed;
            ValidateMaterials(terrainMaterial, barkMaterial, foliageMaterial, grassMaterial,
                roadMaterial, waterMaterial);
            bark = barkMaterial;
            foliage = foliageMaterial;
            grass = grassMaterial;
            road = roadMaterial;
            water = waterMaterial;
            treeMesh = TreeMesh();
            owned.Add(treeMesh);
            root = new GameObject("PVT-2 landscape");
            root.transform.SetParent(parent, false);
            CreateTerrain(terrainMaterial);
            CreateRibbon("River", true, water, 16f);
            CreateRibbon("North-south road", true, road, 5f, true);
            CreateRibbon("East-west road", false, road, 5f, true);
        }

        public static void ValidateMaterials(Material terrain, Material bark, Material foliage,
            Material grass, Material road, Material water)
        {
            ValidateMaterial(terrain, "terrain", "Universal Render Pipeline/Terrain/Lit");
            ValidateMaterial(bark, "bark", "Universal Render Pipeline/Lit");
            ValidateMaterial(foliage, "foliage", "Universal Render Pipeline/Lit");
            ValidateMaterial(grass, "grass", "Universal Render Pipeline/Lit");
            ValidateMaterial(road, "road", "Universal Render Pipeline/Lit");
            ValidateMaterial(water, "water", "Universal Render Pipeline/Lit");
        }

        private static void ValidateMaterial(Material material, string role, string shaderName)
        {
            if (material == null || material.shader == null || !material.shader.isSupported ||
                material.shader.name != shaderName)
                throw new InvalidOperationException($"PVT-2 {role} requires a supported {shaderName} material asset.");
        }

        private void CreateTerrain(Material terrainMaterial)
        {
            const int resolution = 257;
            var data = new TerrainData { heightmapResolution = resolution,
                size = new Vector3(1000f, 40f, 1000f) };
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                heights[z, x] = Pvt2Scenario.GroundHeight(-500f + x * 1000f / (resolution - 1),
                    -500f + z * 1000f / (resolution - 1)) / 40f;
            data.SetHeights(0, 0, heights);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.SetPixels(new[] { new Color(0.33f,0.38f,0.25f), new Color(0.35f,0.4f,0.27f),
                new Color(0.34f,0.39f,0.26f), new Color(0.36f,0.4f,0.28f) });
            texture.Apply();
            var layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(16f,16f) };
            data.terrainLayers = new[] { layer };
            data.alphamapResolution = 16;
            var alpha = new float[16,16,1];
            for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++) alpha[z,x,0] = 1f;
            data.SetAlphamaps(0,0,alpha);
            owned.Add(data); owned.Add(layer); owned.Add(texture);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "PVT-2 terrain 1 km";
            terrainObject.transform.SetParent(root.transform, false);
            terrainObject.transform.position = new Vector3(-500f,0f,-500f);
            terrainObject.GetComponent<Terrain>().materialTemplate = terrainMaterial;
        }

        private void CreateRibbon(string name, bool alongZ, Material material, float width, bool isRoad = false)
        {
            const int segments = 128;
            var vertices = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = -500f + 1000f * i / segments;
                float center = isRoad ? 0f : Pvt2Scenario.RiverX(t);
                for (int side = 0; side < 2; side++)
                {
                    float offset = (side == 0 ? -0.5f : 0.5f) * width;
                    float x = alongZ ? center + offset : t;
                    float z = alongZ ? t : center + offset;
                    vertices[i*2+side] = new Vector3(x, Pvt2Scenario.GroundHeight(x,z) + (isRoad ? 0.12f : 0.9f), z);
                    uv[i*2+side] = new Vector2(side, i * 0.25f);
                }
                if (i == segments) continue;
                int j = i * 6, v = i * 2;
                if (alongZ)
                {
                    triangles[j]=v; triangles[j+1]=v+2; triangles[j+2]=v+1;
                    triangles[j+3]=v+1; triangles[j+4]=v+2; triangles[j+5]=v+3;
                }
                else
                {
                    triangles[j]=v; triangles[j+1]=v+1; triangles[j+2]=v+2;
                    triangles[j+3]=v+1; triangles[j+4]=v+3; triangles[j+5]=v+2;
                }
            }
            var mesh = new Mesh { name = name };
            mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        public bool UpdateStreaming(Vector3 cameraPosition, int scenarioDensity, int radiusCells = 2)
        {
            if (disposed) throw new ObjectDisposedException(nameof(Pvt2Environment));
            if (scenarioDensity < 1 || scenarioDensity > 3) throw new ArgumentOutOfRangeException(nameof(scenarioDensity));
            if (radiusCells < 0 || radiusCells > CellsPerSide) throw new ArgumentOutOfRangeException(nameof(radiusCells));
            Vector2Int cell = Cell(cameraPosition);
            if (cell == lastCell && density == scenarioDensity && lastRadius == radiusCells) return false;
            Stopwatch timer = Stopwatch.StartNew();
            bool rebuildDensity = density != scenarioDensity;
            density = scenarioDensity;
            lastCell = cell;
            lastRadius = radiusCells;
            var wanted = new HashSet<Vector2Int>();
            for (int z = Math.Max(0, cell.y-radiusCells); z <= Math.Min(CellsPerSide-1, cell.y+radiusCells); z++)
            for (int x = Math.Max(0, cell.x-radiusCells); x <= Math.Min(CellsPerSide-1, cell.x+radiusCells); x++)
                wanted.Add(new Vector2Int(x,z));
            var remove = new List<Vector2Int>();
            foreach (Vector2Int key in loaded.Keys) if (!wanted.Contains(key) || rebuildDensity) remove.Add(key);
            foreach (Vector2Int key in remove)
            {
                VegetationRenderers -= loaded[key].transform.childCount;
                UnityEngine.Object.Destroy(loaded[key]);
                loaded.Remove(key);
            }
            foreach (Vector2Int key in wanted)
                if (!loaded.ContainsKey(key)) loaded.Add(key, CreateCell(key, scenarioDensity));
            timer.Stop();
            LastTransitionMs = (float)timer.Elapsed.TotalMilliseconds;
            TotalRegionTransitions++;
            return true;
        }

        public static Vector2Int Cell(Vector3 position) => new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((position.x + 500f) / CellSize), 0, CellsPerSide-1),
            Mathf.Clamp(Mathf.FloorToInt((position.z + 500f) / CellSize), 0, CellsPerSide-1));

        private GameObject CreateCell(Vector2Int cell, int level)
        {
            var go = new GameObject($"PVT-2 foliage {cell.x},{cell.y}");
            go.transform.SetParent(root.transform, false);
            uint state = unchecked((uint)(seed * 16777619) ^ (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663));
            int count = level * 24; // 24 / 48 / 72 candidates per loaded 62.5 m cell
            for (int i = 0; i < count; i++)
            {
                float x = -500f + (cell.x + Next(ref state)) * CellSize;
                float z = -500f + (cell.y + Next(ref state)) * CellSize;
                if (Mathf.Abs(x-Pvt2Scenario.RiverX(z)) < 15f || Mathf.Abs(x) < 4f || Mathf.Abs(z) < 4f) continue;
                float scale = i % 3 == 0 ? 2.5f + Next(ref state) * 2f
                    : i % 3 == 1 ? 0.8f + Next(ref state) : 0.25f + Next(ref state) * 0.35f;
                var plant = new GameObject(i % 3 == 0 ? "Tree" : i % 3 == 1 ? "Shrub" : "Ground cover");
                plant.transform.SetParent(go.transform, false);
                plant.transform.position = new Vector3(x,Pvt2Scenario.GroundHeight(x,z),z);
                plant.transform.localScale = new Vector3(scale * 0.5f,scale,scale * 0.5f);
                plant.AddComponent<MeshFilter>().sharedMesh = treeMesh;
                plant.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, i % 3 == 2 ? grass : foliage };
                VegetationRenderers++;
            }
            return go;
        }

        private static float Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state >> 8) * (1f / 16777216f);
        }

        private static Mesh TreeMesh()
        {
            const int sides = 8;
            var vertices = new List<Vector3>();
            var trunk = new List<int>();
            var crown = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides;
                float b = (i+1) * 2f * Mathf.PI / sides;
                int start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*0.12f,0f,Mathf.Sin(a)*0.12f));
                vertices.Add(new Vector3(Mathf.Cos(b)*0.12f,0f,Mathf.Sin(b)*0.12f));
                vertices.Add(new Vector3(Mathf.Cos(a)*0.12f,0.75f,Mathf.Sin(a)*0.12f));
                vertices.Add(new Vector3(Mathf.Cos(b)*0.12f,0.75f,Mathf.Sin(b)*0.12f));
                trunk.AddRange(new[] { start,start+2,start+1,start+1,start+2,start+3 });
                start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*0.55f,0.55f,Mathf.Sin(a)*0.55f));
                vertices.Add(new Vector3(Mathf.Cos(b)*0.55f,0.55f,Mathf.Sin(b)*0.55f));
                vertices.Add(new Vector3(0f,2f,0f));
                crown.AddRange(new[] { start,start+2,start+1 });
            }
            var mesh = new Mesh { name = "PVT-2 shared low-poly tree" };
            mesh.SetVertices(vertices); mesh.subMeshCount=2;
            mesh.SetTriangles(trunk,0); mesh.SetTriangles(crown,1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed=true;
            loaded.Clear();
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (UnityEngine.Object asset in owned) if (asset != null) UnityEngine.Object.Destroy(asset);
        }
    }

}
