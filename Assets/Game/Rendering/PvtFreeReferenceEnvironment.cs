using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Small art reference only: 80 m of terrain, one lane, and CC0 LOD foliage.
    // No GameObject in this class stands in for an editable construction piece.
    public sealed class PvtFreeReferenceEnvironment : IDisposable
    {
        private readonly GameObject root;
        private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();

        public PvtFreeReferenceEnvironment(Transform parent,Material terrainMaterial,
            Material roadMaterial,Material stoneMaterial,
            Texture2D dryAlbedo,Texture2D dryNormal,Texture2D dryMask,
            Texture2D groundAlbedo,Texture2D groundNormal,Texture2D groundMask,
            GameObject grass,GameObject rock,GameObject pot,GameObject shrub)
        {
            if (parent==null || terrainMaterial==null || roadMaterial==null || stoneMaterial==null ||
                dryAlbedo==null || dryNormal==null || dryMask==null ||
                groundAlbedo==null || groundNormal==null || groundMask==null ||
                grass==null || rock==null || pot==null || shrub==null)
                throw new ArgumentException("Free reference scene has a missing CC0 asset.");
            root=new GameObject("CC0 reference landscape");
            root.transform.SetParent(parent,false);
            BuildTerrain(terrainMaterial,dryAlbedo,dryNormal,dryMask,
                groundAlbedo,groundNormal,groundMask);
            Ribbon("Weathered cobble lane",-10f,5.8f,roadMaterial);
            Ribbon("Near stone verge",-13.1f,0.28f,stoneMaterial);
            Ribbon("Far stone verge",-6.9f,0.28f,stoneMaterial);
            Plant(grass,rock,pot,shrub);
        }

        private void BuildTerrain(Material material,Texture2D dryAlbedo,Texture2D dryNormal,
            Texture2D dryMask,Texture2D forestAlbedo,Texture2D forestNormal,Texture2D forestMask)
        {
            const int resolution=129;
            var data=new TerrainData {heightmapResolution=resolution,
                size=new Vector3(80f,40f,80f)};
            var heights=new float[resolution,resolution];
            for (int z=0;z<resolution;z++) for (int x=0;x<resolution;x++)
                heights[z,x]=Pvt2Scenario.GroundHeight(-40f+x*80f/(resolution-1),
                    -40f+z*80f/(resolution-1))/40f;
            data.SetHeights(0,0,heights);
            var dry=new TerrainLayer {diffuseTexture=dryAlbedo,normalMapTexture=dryNormal,
                maskMapTexture=dryMask,tileSize=new Vector2(4.5f,4.5f)};
            var ground=new TerrainLayer {diffuseTexture=forestAlbedo,normalMapTexture=forestNormal,
                maskMapTexture=forestMask,tileSize=new Vector2(5.5f,5.5f)};
            data.terrainLayers=new[] {dry,ground};
            data.alphamapResolution=128;
            var blend=new float[128,128,2];
            for (int z=0;z<128;z++) for (int x=0;x<128;x++)
            {
                float wx=-40f+(x+0.5f)*80f/128f;
                float wz=-40f+(z+0.5f)*80f/128f;
                float variation=0.14f*Mathf.Sin(wx*0.37f+wz*0.48f)+
                    0.1f*Mathf.Sin(wx*0.81f-wz*0.22f);
                float nearPath=Mathf.Clamp01((13f-Mathf.Abs(wz+10f))/13f);
                float nearHouse=Mathf.Clamp01((17f-Mathf.Max(Mathf.Abs(wx),Mathf.Abs(wz)))/15f);
                float dryWeight=Mathf.Clamp(0.55f+variation+
                    Mathf.Max(nearPath,nearHouse)*0.36f,0.15f,0.96f);
                blend[z,x,0]=dryWeight;
                blend[z,x,1]=1f-dryWeight;
            }
            data.SetAlphamaps(0,0,blend);
            owned.Add(data);owned.Add(dry);owned.Add(ground);
            GameObject terrain=Terrain.CreateTerrainGameObject(data);
            terrain.name="Natural ground 80 m - CC0 PBR";
            terrain.transform.SetParent(root.transform,false);
            terrain.transform.position=new Vector3(-40f,0f,-40f);
            terrain.GetComponent<Terrain>().materialTemplate=material;
        }

        private void Ribbon(string name,float centerZ,float width,Material material)
        {
            const int sections=32;
            var vertices=new Vector3[(sections+1)*2];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[sections*6];
            for (int i=0;i<=sections;i++)
            {
                float x=-32f+i*64f/sections;
                for (int side=0;side<2;side++)
                {
                    float z=centerZ+(side==0 ? -0.5f : 0.5f)*width;
                    int v=i*2+side;
                    vertices[v]=new Vector3(x,Pvt2Scenario.GroundHeight(x,z)+0.13f,z);
                    uv[v]=new Vector2(x/4f,z/4f);
                }
                if (i==sections) continue;
                int a=i*2,t=i*6;
                triangles[t]=a;triangles[t+1]=a+1;triangles[t+2]=a+2;
                triangles[t+3]=a+1;triangles[t+4]=a+3;triangles[t+5]=a+2;
            }
            var mesh=new Mesh {name=name};
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            owned.Add(mesh);
            var item=new GameObject(name);
            item.transform.SetParent(root.transform,false);
            item.AddComponent<MeshFilter>().sharedMesh=mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        private void Plant(GameObject grass,GameObject rock,GameObject pot,GameObject shrub)
        {
            uint state=19371u;
            int planted=0;
            for (int i=0;i<150 && planted<55;i++)
            {
                float x=-31f+Next(ref state)*62f,z=-31f+Next(ref state)*62f;
                if ((Mathf.Abs(x)<8.5f && Mathf.Abs(z)<8f) ||
                    Mathf.Abs(z+10f)<4.6f) continue;
                GameObject model=planted%10==0 ? rock : planted%7==0 ? shrub : grass;
                GameObject item=UnityEngine.Object.Instantiate(model,root.transform);
                item.name=model==rock ? "CC0 weathered rock LOD" :
                    model==shrub ? "CC0 shrub LOD" : "CC0 Bermuda grass LOD";
                item.transform.position=new Vector3(x,Pvt2Scenario.GroundHeight(x,z),z);
                item.transform.rotation=Quaternion.Euler(0f,Next(ref state)*360f,0f);
                item.transform.localScale*=0.8f+Next(ref state)*0.5f;
                planted++;
            }
            for (int side=-1;side<=1;side+=2)
            {
                float x=side*2.5f,z=-4.5f;
                GameObject item=UnityEngine.Object.Instantiate(pot,root.transform);
                item.name="CC0 ceramic door pot";
                item.transform.position=new Vector3(x,Pvt2Scenario.GroundHeight(x,z),z);
                item.transform.rotation=Quaternion.Euler(0f,side*20f,0f);
            }
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
