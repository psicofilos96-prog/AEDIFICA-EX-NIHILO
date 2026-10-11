using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Visual environment only. Architectural pieces stay in ConstructionWorld.
    public sealed class PvtGrandVillageEnvironment : IDisposable
    {
        private const float Extent=90f;
        private static readonly float[] HouseXs={-72f,-57f,-42f,-27f,-12f,3f};
        private static readonly float[] HouseZs={-58f,-40f,-22f};
        private readonly GameObject root;
        private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();

        public static float RiverCenter(float z) => 48f+7f*Mathf.Sin(z*0.027f);
        public static float Height(float x,float z)
        {
            float baseHeight=2.25f+0.22f*Mathf.Sin(x*0.08f)+0.2f*Mathf.Cos(z*0.07f);
            float plateau=Mathf.Max(Mathf.Abs((x+22f)/45f),Mathf.Abs((z-38f)/40f));
            float hill=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.78f,1.3f,plateau));
            float height=Mathf.Lerp(baseHeight,12f,hill);
            height=Terrace(height,x,z,-60f,-6f,15f,9f,3.2f);
            height=Terrace(height,x,z,-5f,-7f,11f,8f,2.8f);
            float shore=Mathf.Abs(x-RiverCenter(z));
            float river=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(5.2f,9f,shore));
            return Mathf.Lerp(height,0.15f,river);
        }

        private static float Terrace(float height,float x,float z,float centerX,float centerZ,
            float halfX,float halfZ,float level)
        {
            float outside=Mathf.Max(Mathf.Abs(x-centerX)-halfX,Mathf.Abs(z-centerZ)-halfZ);
            float weight=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(outside/6f));
            return Mathf.Lerp(height,level,weight);
        }

        public PvtGrandVillageEnvironment(Transform parent,Material terrain,Material road,
            Material water,Texture2D dryAlbedo,Texture2D dryNormal,Texture2D dryMask,
            Texture2D groundAlbedo,Texture2D groundNormal,Texture2D groundMask,
            GameObject coastalTree,GameObject pine,GameObject shrub,GameObject rock)
        {
            if (parent==null || terrain==null || road==null || water==null ||
                dryAlbedo==null || dryNormal==null || dryMask==null || groundAlbedo==null ||
                groundNormal==null || groundMask==null || coastalTree==null || pine==null ||
                shrub==null || rock==null)
                throw new ArgumentException("Grand village scene has a missing serialized environment asset.");
            root=new GameObject("Grand village environment - terrain river trees");
            root.transform.SetParent(parent,false);
            BuildTerrain(terrain,dryAlbedo,dryNormal,dryMask,groundAlbedo,groundNormal,groundMask);
            River(water);
            RoadAlongZ("Stone ascent to castle",-22f,-77f,14f,5f,road);
            RoadAlongZ("Village avenue",-33f,-77f,-12f,5.3f,road);
            RoadAlongX("Riverside route",-18f,-77f,36f,4.5f,road);
            Plant(coastalTree,pine,shrub,rock);
        }

        private void BuildTerrain(Material material,Texture2D dryAlbedo,Texture2D dryNormal,
            Texture2D dryMask,Texture2D groundAlbedo,Texture2D groundNormal,Texture2D groundMask)
        {
            const int resolution=257;
            var data=new TerrainData {heightmapResolution=resolution,
                size=new Vector3(180f,24f,180f)};
            var heights=new float[resolution,resolution];
            for (int z=0;z<resolution;z++) for (int x=0;x<resolution;x++)
                heights[z,x]=Height(-Extent+x*180f/(resolution-1),
                    -Extent+z*180f/(resolution-1))/24f;
            data.SetHeights(0,0,heights);
            var dry=new TerrainLayer {diffuseTexture=dryAlbedo,normalMapTexture=dryNormal,
                maskMapTexture=dryMask,tileSize=new Vector2(6f,6f)};
            var ground=new TerrainLayer {diffuseTexture=groundAlbedo,normalMapTexture=groundNormal,
                maskMapTexture=groundMask,tileSize=new Vector2(6f,6f)};
            data.terrainLayers=new[] {dry,ground};
            data.alphamapResolution=256;
            var mix=new float[256,256,2];
            for (int z=0;z<256;z++) for (int x=0;x<256;x++)
            {
                float wx=-Extent+(x+0.5f)*180f/256f;
                float wz=-Extent+(z+0.5f)*180f/256f;
                float soil=Mathf.Clamp01(0.54f+0.18f*Mathf.Sin(wx*0.15f+wz*0.13f)+
                    0.17f*Mathf.Cos(wz*0.22f));
                if (Mathf.Abs(wx-RiverCenter(wz))<10f) soil*=0.6f;
                if (Mathf.Abs(wx+22f)<38f && wz>0f) soil=0.78f;
                mix[z,x,0]=soil;mix[z,x,1]=1f-soil;
            }
            data.SetAlphamaps(0,0,mix);
            owned.Add(data);owned.Add(dry);owned.Add(ground);
            GameObject terrainObject=Terrain.CreateTerrainGameObject(data);
            terrainObject.name="Castle hill and riverbanks - CC0 ground";
            terrainObject.transform.SetParent(root.transform,false);
            terrainObject.transform.position=new Vector3(-Extent,0f,-Extent);
            terrainObject.GetComponent<Terrain>().materialTemplate=material;
        }

        private void River(Material material)
        {
            const int segments=120;
            var vertices=new Vector3[(segments+1)*2];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*6];
            for (int i=0;i<=segments;i++)
            {
                float z=-Extent+i*180f/segments;
                for (int side=0;side<2;side++)
                {
                    int index=i*2+side;
                    vertices[index]=new Vector3(RiverCenter(z)+(side==0 ? -4.8f : 4.8f),0.75f,z);
                    uv[index]=new Vector2(side,z/8f);
                }
                if (i==segments) continue;
                int a=i*2,t=i*6;
                triangles[t]=a;triangles[t+1]=a+2;triangles[t+2]=a+1;
                triangles[t+3]=a+1;triangles[t+4]=a+2;triangles[t+5]=a+3;
            }
            MeshObject("River - shared URP water",vertices,uv,triangles,material);
        }

        private void RoadAlongZ(string name,float x,float z0,float z1,float width,Material mat)
        { Ribbon(name,40,i=>new Vector2(x,Mathf.Lerp(z0,z1,i/40f)),width,mat,true); }
        private void RoadAlongX(string name,float z,float x0,float x1,float width,Material mat)
        { Ribbon(name,40,i=>new Vector2(Mathf.Lerp(x0,x1,i/40f),z),width,mat,false); }
        private void Ribbon(string name,int segments,Func<int,Vector2> point,float width,
            Material mat,bool alongZ)
        {
            var vertices=new Vector3[(segments+1)*2];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*6];
            for (int i=0;i<=segments;i++)
            {
                Vector2 p=point(i);
                for (int side=0;side<2;side++)
                {
                    int v=i*2+side;
                    float offset=(side==0 ? -0.5f : 0.5f)*width;
                    float x=p.x+(alongZ ? offset : 0f),z=p.y+(alongZ ? 0f : offset);
                    vertices[v]=new Vector3(x,Height(x,z)+0.16f,z);
                    uv[v]=new Vector2(x/4f,z/4f);
                }
                if (i==segments) continue;
                int a=i*2,t=i*6;
                triangles[t]=a;triangles[t+1]=a+2;triangles[t+2]=a+1;
                triangles[t+3]=a+1;triangles[t+4]=a+2;triangles[t+5]=a+3;
            }
            MeshObject(name,vertices,uv,triangles,mat);
        }

        private void MeshObject(string name,Vector3[] vertices,Vector2[] uv,int[] triangles,Material mat)
        {
            var mesh=new Mesh {name=name};
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            owned.Add(mesh);
            var item=new GameObject(name);
            item.transform.SetParent(root.transform,false);
            item.AddComponent<MeshFilter>().sharedMesh=mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }

        public static bool IsReserved(float x,float z)
        {
            if (Mathf.Abs(x-RiverCenter(z))<11f) return true;
            if (x>-54f && x<10f && z>7f && z<69f) return true;
            if (x>-75f && x<-45f && z>-16f && z<4f) return true;
            if (x>-18f && x<8f && z>-20f && z<4f) return true;
            if (Mathf.Abs(x+22f)<5f && z>-78f && z<15f) return true;
            if (Mathf.Abs(x+33f)<5f && z>-78f && z<-12f) return true;
            foreach (float houseX in HouseXs)
                foreach (float houseZ in HouseZs)
                    if (Mathf.Abs(x-houseX)<6f && Mathf.Abs(z-houseZ)<6f) return true;
            return false;
        }

        private void Plant(GameObject coastalTree,GameObject pine,GameObject shrub,GameObject rock)
        {
            uint state=53467;
            int planted=0;
            for (int attempt=0;attempt<2400 && planted<230;attempt++)
            {
                float x=-83f+Next(ref state)*166f,z=-83f+Next(ref state)*166f;
                if (IsReserved(x,z)) continue;
                GameObject prefab=planted%13==0 ? rock : planted%5==0 ? shrub :
                    planted%7==0 ? pine : coastalTree;
                GameObject item=UnityEngine.Object.Instantiate(prefab,root.transform);
                item.name=prefab.name+" LOD environment";
                item.transform.position=new Vector3(x,Height(x,z),z);
                item.transform.rotation=Quaternion.Euler(0f,Next(ref state)*360f,0f);
                item.transform.localScale*=0.7f+Next(ref state)*0.55f;
                planted++;
            }
            if (planted<230) throw new InvalidOperationException("Grand village vegetation scatter exhausted.");
        }

        private static float Next(ref uint state)
        { state=unchecked(state*1664525u+1013904223u);return (state>>8)*(1f/16777216f); }
        public void Dispose()
        {
            if (root!=null) UnityEngine.Object.Destroy(root);
            foreach (UnityEngine.Object item in owned)
                if (item!=null) UnityEngine.Object.Destroy(item);
            owned.Clear();
        }
    }
}
