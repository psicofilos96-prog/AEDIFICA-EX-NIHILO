#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Deterministic procedural PBR stand-ins. Visual adequacy must be judged from Windows captures.
public static class PvtFinalMaterialBuilder
{
    public const string Folder = "Assets/Game/Materials/PvtFinalGenerated";
    public static readonly string[] Names = { "Stone", "Brick", "Wood", "Plaster", "Ceramic" };
    private static readonly Color[] Bases = {
        new Color(0.52f,0.50f,0.45f), new Color(0.48f,0.23f,0.17f),
        new Color(0.36f,0.24f,0.13f), new Color(0.69f,0.64f,0.52f),
        new Color(0.41f,0.22f,0.16f) };
    private static readonly float[] Smoothness = { 0.24f,0.17f,0.28f,0.12f,0.38f };

    [MenuItem("Tools/Aedifica/PVT-FINAL/Generate Procedural PBR Materials")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if (shader==null) throw new InvalidOperationException("URP/Lit is unavailable.");
        for (int kind=0;kind<Names.Length;kind++)
        {
            string prefix=Folder+"/"+Names[kind];
            bool newNormal=!File.Exists(prefix+"_Normal.png");
            WriteTexture(prefix+"_Albedo.png",kind,false);
            WriteTexture(prefix+"_Normal.png",kind,true);
            AssetDatabase.ImportAsset(prefix+"_Albedo.png",ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(prefix+"_Normal.png",ImportAssetOptions.ForceUpdate);
            var importer=AssetImporter.GetAtPath(prefix+"_Normal.png") as TextureImporter;
            if (importer==null) throw new InvalidOperationException("Normal texture importer unavailable: "+prefix);
            if (newNormal)
            {
                importer.textureType=TextureImporterType.NormalMap;
                importer.sRGBTexture=false;
                importer.SaveAndReimport();
            }
            else if (importer.textureType!=TextureImporterType.NormalMap || importer.sRGBTexture)
                throw new InvalidOperationException("Existing normal texture import settings need review: "+prefix);
            string path=prefix+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material!=null) { Load(Names[kind]); continue; }
            material=new Material(shader) { name="PVT-Final "+Names[kind],enableInstancing=true };
            AssetDatabase.CreateAsset(material,path);
            if (material.shader!=shader) throw new InvalidOperationException("Unexpected shader: "+path);
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Albedo.png"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Normal.png"));
            material.SetFloat("_BumpScale",0.65f);
            material.SetFloat("_Smoothness",Smoothness[kind]);
            material.EnableKeyword("_NORMALMAP");
            material.enableInstancing=true;
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("PVT-Final PBR textures and materials generated at "+Folder);
    }

    public static Material Load(string name)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
        if (material==null || material.shader==null || material.shader.name!="Universal Render Pipeline/Lit" ||
            material.GetTexture("_BaseMap")==null || material.GetTexture("_BumpMap")==null)
            throw new InvalidOperationException("Generate the required PVT-Final PBR material: "+name);
        return material;
    }

    private static void WriteTexture(string path,int kind,bool normal)
    {
        const int size=256;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,normal);
        var colors=new Color[size*size];
        var heights=new float[size*size];
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            heights[y*size+x]=Height(kind,x,y);
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
        {
            int i=y*size+x;
            float h=heights[i];
            if (normal)
            {
                float dx=heights[y*size+(x+1)%size]-heights[y*size+(x+size-1)%size];
                float dy=heights[((y+1)%size)*size+x]-heights[((y+size-1)%size)*size+x];
                Vector3 n=new Vector3(-dx*2f,-dy*2f,1f).normalized;
                colors[i]=new Color(n.x*0.5f+0.5f,n.y*0.5f+0.5f,n.z*0.5f+0.5f,1f);
            }
            else
            {
                float shade=Mathf.Clamp(0.72f+h*0.48f,0.22f,1.12f);
                colors[i]=new Color(Bases[kind].r*shade,Bases[kind].g*shade,
                    Bases[kind].b*shade,1f);
            }
        }
        texture.SetPixels(colors); texture.Apply();
        byte[] png=texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        // Regeneration is deterministic and does not overwrite a locally edited texture.
        if (!File.Exists(path)) File.WriteAllBytes(path,png);
    }

    private static float Height(int kind,int x,int y)
    {
        uint hash=unchecked((uint)(x*73856093)^(uint)(y*19349663)^(uint)(kind*83492791));
        hash^=hash>>13; hash*=1274126177u; hash^=hash>>16;
        float grain=(hash&1023u)/1023f-0.5f;
        if (kind==1 || kind==4)
        {
            int row=y/32, horizontal=(x+((row&1)*32))%64;
            bool mortar=x%64<2 || y%32<2;
            if (kind==4) mortar=x%32<2 || y%32<2;
            return mortar ? -0.72f : 0.12f+grain*0.26f+horizontal/64f*0.08f;
        }
        if (kind==2) return 0.18f*Mathf.Sin(y*0.27f+x*0.02f)+grain*0.18f;
        if (kind==0) return 0.22f*Mathf.Sin(x*0.14f)*Mathf.Cos(y*0.17f)+grain*0.22f;
        return grain*0.13f+0.08f*Mathf.Sin(x*0.025f+y*0.018f);
    }
}
#endif
