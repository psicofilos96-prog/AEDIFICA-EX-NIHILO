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
            Material roadMaterial,Material stoneMaterial,Material waterMaterial,Texture2D groundAlbedo,
            Texture2D groundNormal,Texture2D groundMask,GameObject treePrefab,GameObject shrubPrefab)
        {
            this.parent=parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            tree=treePrefab != null ? treePrefab : throw new ArgumentNullException(nameof(treePrefab));
            shrub=shrubPrefab != null ? shrubPrefab : throw new ArgumentNullException(nameof(shrubPrefab));
            if (groundAlbedo==null || groundNormal==null || groundMask==null ||
                terrainMaterial==null || roadMaterial==null || stoneMaterial==null || waterMaterial==null)
                throw new ArgumentException("Visual terrain and river materials must be assigned.");
            this.seed=seed;
            root=new GameObject("PVT visual landscape");
            root.transform.SetParent(parent,false);
            CreateTerrain(terrainMaterial,roadMaterial,groundAlbedo,groundNormal,groundMask);
            CreateRibbon("Historic river",true,waterMaterial,15f,false);
            CreateRibbon("Cobbled north street",true,roadMaterial,5f,true);
            CreateRibbon("Cobbled east street",false,roadMaterial,5f,true);
            CreateRibbon("West stone curb",true,stoneMaterial,0.28f,true,-222.75f);
            CreateRibbon("East stone curb",true,stoneMaterial,0.28f,true,-217.25f);
            CreatePlaza(roadMaterial);
            CreateCityPlantings();
        }

        private void CreateTerrain(Material material,Material roadMaterial,
            Texture2D albedo,Texture2D normal,Texture2D mask)
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
            var cobble=new TerrainLayer {
                diffuseTexture=roadMaterial.GetTexture("_BaseMap") as Texture2D,
                normalMapTexture=roadMaterial.GetTexture("_BumpMap") as Texture2D,
                maskMapTexture=roadMaterial.GetTexture("_MetallicGlossMap") as Texture2D,
                tileSize=new Vector2(5f,5f)};
            if (cobble.diffuseTexture==null || cobble.normalMapTexture==null)
                throw new InvalidOperationException("CC0 road material lacks terrain transition maps.");
            data.terrainLayers=new[] {layer,cobble};
            data.alphamapResolution=256;
            var blend=new float[256,256,2];
            for (int z=0;z<256;z++) for (int x=0;x<256;x++)
            {
                float wx=-500f+(x+0.5f)*1000f/256f;
                float wz=-500f+(z+0.5f)*1000f/256f;
                float irregular=0.7f*Mathf.Sin(wx*0.32f+wz*0.17f)+
                    0.45f*Mathf.Sin(wx*0.73f-wz*0.28f);
                float street=Mathf.Abs(wx+220f);
                float streetRange=wz>=-146f && wz<=70f ?
                    Mathf.Clamp01((9f+irregular-street)/5f) : 0f;
                float plazaDistance=Mathf.Max(Mathf.Abs(wx+220f)-19f,
                    Mathf.Abs(wz-59f)-11f);
                float square=Mathf.Clamp01((3f+irregular-plazaDistance)/5f);
                float mix=Mathf.Max(streetRange,square);
                blend[z,x,0]=1f-mix;
                blend[z,x,1]=mix;
            }
            data.SetAlphamaps(0,0,blend);
            owned.Add(data); owned.Add(layer);owned.Add(cobble);
            GameObject terrain=Terrain.CreateTerrainGameObject(data);
            terrain.name="Textured terrain 1 km";
            terrain.transform.SetParent(root.transform,false);
            terrain.transform.position=new Vector3(-500f,0f,-500f);
            terrain.GetComponent<Terrain>().materialTemplate=material;
        }

        private void CreateRibbon(string name,bool alongZ,Material material,float width,bool road,
            float centerOverride=float.NaN)
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
                float center=!float.IsNaN(centerOverride) ? centerOverride :
                    road ? alongZ ? -220f : -20f : Pvt2Scenario.RiverX(t);
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

        private void CreatePlaza(Material material)
        {
            const int cells=20;
            var vertices=new Vector3[(cells+1)*(cells+1)];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[cells*cells*6];
            for (int z=0;z<=cells;z++) for (int x=0;x<=cells;x++)
            {
                float wx=-238f+x*36f/cells,wz=48f+z*20f/cells;
                int index=z*(cells+1)+x;
                vertices[index]=new Vector3(wx,Pvt2Scenario.GroundHeight(wx,wz)+0.16f,wz);
                uv[index]=new Vector2(wx/5f,wz/5f);
            }
            for (int z=0;z<cells;z++) for (int x=0;x<cells;x++)
            {
                int index=(z*cells+x)*6,v=z*(cells+1)+x;
                triangles[index]=v;triangles[index+1]=v+cells+1;triangles[index+2]=v+1;
                triangles[index+3]=v+1;triangles[index+4]=v+cells+1;
                triangles[index+5]=v+cells+2;
            }
            AddMesh("Stone forecourt",vertices,uv,triangles,material);
        }

        private void AddMesh(string name,Vector3[] vertices,Vector2[] uv,int[] triangles,Material material)
        {
            var mesh=new Mesh {name=name};
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            owned.Add(mesh);
            var obj=new GameObject(name);
            obj.transform.SetParent(root.transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        private void CreateCityPlantings()
        {
            var group=new GameObject("Courtyard plantings - CC0 LOD");
            group.transform.SetParent(root.transform,false);
            for (int i=0;i<16;i++)
            {
                float z=-117f+(i/4)*63f+(i%4)*3.4f;
                float x=i%2==0 ? -252f-(i%3)*1.4f : -188f+(i%3)*1.4f;
                bool pine=i%4==0;
                GameObject plant=UnityEngine.Object.Instantiate(pine ? tree : shrub,group.transform);
                plant.name=pine ? "CC0 courtyard pine" : "CC0 courtyard shrub";
                plant.transform.position=new Vector3(x,Pvt2Scenario.GroundHeight(x,z),z);
                plant.transform.rotation=Quaternion.Euler(0f,(i*137f)%360f,0f);
                plant.transform.localScale*=pine ? 0.82f : 0.38f+(i%3)*0.12f;
                VegetationInstances++;
            }
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
