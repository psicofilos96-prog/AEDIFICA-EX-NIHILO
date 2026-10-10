using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aedifica.Rendering
{
    // The inspection city uses real CC0 vegetation with LOD. This is deliberately
    // separate from the historical PVT-2 environment and its benchmark baseline.
    public sealed class PvtVisualEnvironment : IDisposable
    {
        private const int Cells = 16;
        private const float CellSize = 62.5f;
        private readonly Transform parent;
        private readonly GameObject tree, shrub;
        private readonly Dictionary<Vector2Int,GameObject> loaded = new Dictionary<Vector2Int,GameObject>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly GameObject root;
        private readonly int seed;
        private Vector2Int lastCell = new Vector2Int(int.MinValue,int.MinValue);
        private bool disposed;

        public int LoadedRegions => loaded.Count;
        public int VegetationInstances { get; private set; }

        public PvtVisualEnvironment(Transform parent,int seed,Material terrainMaterial,
            Material roadMaterial,Material waterMaterial,Texture2D groundAlbedo,
            Texture2D groundNormal,Texture2D groundMask,GameObject treePrefab,GameObject shrubPrefab)
        {
            this.parent=parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            tree=treePrefab != null ? treePrefab : throw new ArgumentNullException(nameof(treePrefab));
            shrub=shrubPrefab != null ? shrubPrefab : throw new ArgumentNullException(nameof(shrubPrefab));
            if (groundAlbedo==null || groundNormal==null || groundMask==null ||
                terrainMaterial==null || roadMaterial==null || waterMaterial==null)
                throw new ArgumentException("Visual terrain and river materials must be assigned.");
            this.seed=seed;
            root=new GameObject("PVT visual landscape");
            root.transform.SetParent(parent,false);
            CreateTerrain(terrainMaterial,groundAlbedo,groundNormal,groundMask);
            CreateRibbon("Historic river",true,waterMaterial,15f,false);
            CreateRibbon("Cobbled north street",true,roadMaterial,5f,true);
            CreateRibbon("Cobbled east street",false,roadMaterial,5f,true);
        }

        private void CreateTerrain(Material material,Texture2D albedo,Texture2D normal,Texture2D mask)
        {
            const int resolution=257;
            var data=new TerrainData {heightmapResolution=resolution,
                size=new Vector3(1000f,40f,1000f)};
            var heights=new float[resolution,resolution];
            for (int z=0;z<resolution;z++) for (int x=0;x<resolution;x++)
                heights[z,x]=Pvt2Scenario.GroundHeight(-500f+x*1000f/(resolution-1),
                    -500f+z*1000f/(resolution-1))/40f;
            data.SetHeights(0,0,heights);
            var layer=new TerrainLayer {diffuseTexture=albedo,normalMapTexture=normal,
                maskMapTexture=mask,tileSize=new Vector2(6f,6f)};
            data.terrainLayers=new[] {layer};
            owned.Add(data); owned.Add(layer);
            GameObject terrain=Terrain.CreateTerrainGameObject(data);
            terrain.name="Textured terrain 1 km";
            terrain.transform.SetParent(root.transform,false);
            terrain.transform.position=new Vector3(-500f,0f,-500f);
            terrain.GetComponent<Terrain>().materialTemplate=material;
        }

        private void CreateRibbon(string name,bool alongZ,Material material,float width,bool road)
        {
            const int segments=128;
            var vertices=new Vector3[(segments+1)*2];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*6];
            for (int i=0;i<=segments;i++)
            {
                float start=road ? alongZ ? -140f : -290f : -500f;
                float length=road ? alongZ ? 200f : 140f : 1000f;
                float t=start+i*length/segments;
                float center=road ? alongZ ? -220f : -20f : Pvt2Scenario.RiverX(t);
                for (int side=0;side<2;side++)
                {
                    float offset=(side==0 ? -0.5f : 0.5f)*width;
                    float x=alongZ ? center+offset : t;
                    float z=alongZ ? t : center+offset;
                    vertices[i*2+side]=new Vector3(x,
                        Pvt2Scenario.GroundHeight(x,z)+(road ? 0.13f : 0.9f),z);
                    uv[i*2+side]=new Vector2(side*width,i*length/(segments*4f));
                }
                if (i==segments) continue;
                int j=i*6,v=i*2;
                if (alongZ)
                { triangles[j]=v;triangles[j+1]=v+2;triangles[j+2]=v+1;
                  triangles[j+3]=v+1;triangles[j+4]=v+2;triangles[j+5]=v+3; }
                else
                { triangles[j]=v;triangles[j+1]=v+1;triangles[j+2]=v+2;
                  triangles[j+3]=v+1;triangles[j+4]=v+3;triangles[j+5]=v+2; }
            }
            var mesh=new Mesh {name=name};
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            owned.Add(mesh);
            var obj=new GameObject(name);
            obj.transform.SetParent(root.transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        public void UpdateStreaming(Vector3 cameraPosition,int radius=2)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PvtVisualEnvironment));
            if (radius<0 || radius>Cells) throw new ArgumentOutOfRangeException(nameof(radius));
            Vector2Int cell=Cell(cameraPosition);
            if (cell==lastCell) return;
            lastCell=cell;
            var wanted=new HashSet<Vector2Int>();
            for (int z=Mathf.Max(0,cell.y-radius);z<=Mathf.Min(Cells-1,cell.y+radius);z++)
            for (int x=Mathf.Max(0,cell.x-radius);x<=Mathf.Min(Cells-1,cell.x+radius);x++)
                wanted.Add(new Vector2Int(x,z));
            var remove=new List<Vector2Int>();
            foreach (Vector2Int key in loaded.Keys) if (!wanted.Contains(key)) remove.Add(key);
            foreach (Vector2Int key in remove)
            {
                VegetationInstances-=loaded[key].transform.childCount;
                UnityEngine.Object.Destroy(loaded[key]);loaded.Remove(key);
            }
            foreach (Vector2Int key in wanted)
                if (!loaded.ContainsKey(key)) loaded.Add(key,CreateCell(key));
        }

        private GameObject CreateCell(Vector2Int cell)
        {
            var group=new GameObject($"CC0 foliage {cell.x},{cell.y}");
            group.transform.SetParent(root.transform,false);
            uint state=unchecked((uint)seed ^ (uint)(cell.x*73856093) ^ (uint)(cell.y*19349663));
            for (int i=0;i<20;i++)
            {
                float x=-500f+(cell.x+Next(ref state))*CellSize;
                float z=-500f+(cell.y+Next(ref state))*CellSize;
                if (Mathf.Abs(x-Pvt2Scenario.RiverX(z))<20f ||
                    Mathf.Abs(x+220f)<8f || Mathf.Abs(z)<8f ||
                    (x>-310f && x<-130f && z>-110f && z<110f)) continue;
                bool isTree=i<5;
                GameObject plant=UnityEngine.Object.Instantiate(isTree ? tree : shrub,group.transform);
                plant.name=isTree ? "CC0 pine with LOD" : "CC0 shrub with LOD";
                plant.transform.position=new Vector3(x,Pvt2Scenario.GroundHeight(x,z),z);
                plant.transform.rotation=Quaternion.Euler(0f,Next(ref state)*360f,0f);
                plant.transform.localScale*=0.8f+Next(ref state)*0.5f;
                VegetationInstances++;
            }
            return group;
        }

        private static float Next(ref uint state)
        { state=unchecked(state*1664525u+1013904223u);return (state>>8)*(1f/16777216f); }

        private static Vector2Int Cell(Vector3 p) => new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((p.x+500f)/CellSize),0,Cells-1),
            Mathf.Clamp(Mathf.FloorToInt((p.z+500f)/CellSize),0,Cells-1));

        public void Dispose()
        {
            if (disposed) return;
            disposed=true;loaded.Clear();
            if (root!=null) UnityEngine.Object.Destroy(root);
            foreach (UnityEngine.Object item in owned) if (item!=null) UnityEngine.Object.Destroy(item);
        }
    }
}
