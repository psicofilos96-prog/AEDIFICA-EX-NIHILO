#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Persistent URP materials and UV-preserving, capped CC0 tree LODs.
public static class PvtGrandVillageAssetBuilder
{
    public const string Folder="Assets/Game/Art/GrandVillage";
    public const string PrefabPath=Folder+"/CoastalTree.prefab";
    private const string Maps=Folder+"/CoastalTree/CoastalTree_";

    [MenuItem("Tools/Aedifica/Grand River Village/Prepare CC0 Assets")]
    public static void Prepare()
    {
        PvtFreeReferenceAssetBuilder.Prepare();
        CreateMaterial("CoastalTrunk","TrunkAlbedo_1k.jpg","TrunkNormal_1k.jpg",
            "TrunkMask_1k.png",false);
        CreateMaterial("CoastalBranches","BranchesAlbedo_1k.jpg","BranchesNormal_1k.png",
            "BranchesMask_1k.png",false);
        CreateMaterial("CoastalLeaves","LeavesBase_1k.png","LeavesNormal_1k.png",
            "LeavesMask_1k.png",true);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)
        { AssetDatabase.SaveAssets();return; }
        var root=new GameObject("CC0 coastal tree LOD");
        try
        {
            var lods=new LOD[3];
            for (int i=0;i<3;i++)
            {
                string path=Folder+"/Models/CoastalTree_LOD"+i+".fbx";
                GameObject source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source==null) throw new InvalidOperationException("CC0 coastal tree FBX missing: "+path);
                GameObject level=(GameObject)PrefabUtility.InstantiatePrefab(source);
                level.name="Coastal tree LOD"+i;
                level.transform.SetParent(root.transform,false);
                MeshRenderer[] renderers=level.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length==0) throw new InvalidOperationException("Coastal tree has no renderer: "+path);
                Bounds bounds=renderers[0].bounds;
                foreach (MeshRenderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    Material[] slots=renderer.sharedMaterials;
                    if (slots.Length==0) throw new InvalidOperationException("Coastal tree material slot missing.");
                    for (int j=0;j<slots.Length;j++)
                    {
                        string label=slots[j]!=null ? slots[j].name.ToLowerInvariant() : "";
                        bool leaf=label.Contains("leaf") || label.Contains("leav") ||
                            label.Contains("foliage");
                        bool branch=label.Contains("branch");
                        if (!leaf && !branch && slots.Length>1)
                        { leaf=j==1;branch=j>=2; }
                        slots[j]=Load(leaf ? "CoastalLeaves" :
                            branch ? "CoastalBranches" : "CoastalTrunk");
                    }
                    renderer.sharedMaterials=slots;
                    renderer.shadowCastingMode=i==2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                if (bounds.size.y<=0f) throw new InvalidOperationException("Coastal tree has zero height.");
                level.transform.localScale*=8f/bounds.size.y;
                bounds=renderers[0].bounds;
                foreach (MeshRenderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                level.transform.position+=Vector3.down*bounds.min.y;
                lods[i]=new LOD(i==0 ? 0.55f : i==1 ? 0.18f : 0.025f,renderers);
            }
            LODGroup group=root.AddComponent<LODGroup>();
            group.SetLODs(lods);group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }

    public static GameObject LoadPrefab()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab==null) throw new InvalidOperationException("Prepare CC0 coastal tree prefab first.");
        return prefab;
    }
    private static Material Load(string name)
    {
        Material mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
        if (mat==null || mat.shader==null || mat.shader.name!="Universal Render Pipeline/Lit")
            throw new InvalidOperationException("Coastal tree URP material missing: "+name);
        return mat;
    }
    private static void CreateMaterial(string name,string albedo,string normal,string mask,bool cutout)
    {
        Shader lit=Shader.Find("Universal Render Pipeline/Lit");
        if (lit==null || !lit.isSupported) throw new InvalidOperationException("URP/Lit required.");
        Texture2D baseMap=Import(Maps+albedo,false,true);
        Texture2D normalMap=Import(Maps+normal,true,false);
        Texture2D maskMap=Import(Maps+mask,false,false);
        string path=Folder+"/"+name+".mat";
        Material existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing!=null)
        {
            if (existing.shader!=lit || existing.GetTexture("_BaseMap")!=baseMap ||
                existing.GetTexture("_BumpMap")!=normalMap ||
                existing.GetTexture("_MetallicGlossMap")!=maskMap)
                throw new InvalidOperationException("Review changed coastal tree material: "+path);
            return;
        }
        var material=new Material(lit) {name=name,enableInstancing=true};
        AssetDatabase.CreateAsset(material,path);
        material.SetTexture("_BaseMap",baseMap);
        material.SetTexture("_BumpMap",normalMap);
        material.SetTexture("_MetallicGlossMap",maskMap);
        material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Smoothness",1f);
        material.SetFloat("_Metallic",1f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        if (cutout)
        {
            material.SetFloat("_AlphaClip",1f);material.SetFloat("_Cutoff",0.45f);
            material.SetFloat("_Cull",0f);material.EnableKeyword("_ALPHATEST_ON");
            material.doubleSidedGI=true;
        }
        EditorUtility.SetDirty(material);
    }
    private static Texture2D Import(string path,bool normal,bool srgb)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("CC0 1K map missing",path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer==null) throw new InvalidOperationException("TextureImporter missing: "+path);
        bool changed=importer.maxTextureSize!=1024 || !importer.mipmapEnabled ||
            importer.textureType!=(normal ? TextureImporterType.NormalMap : TextureImporterType.Default) ||
            importer.sRGBTexture!=srgb || importer.textureCompression!=TextureImporterCompression.CompressedHQ ||
            (path.Contains("LeavesBase") && !importer.alphaIsTransparency);
        if (changed)
        {
            importer.maxTextureSize=1024;importer.mipmapEnabled=true;
            importer.textureType=normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture=srgb;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            if (path.Contains("LeavesBase")) importer.alphaIsTransparency=true;
            importer.SaveAndReimport();
        }
        Texture2D map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (map==null) throw new InvalidOperationException("CC0 texture import failed: "+path);
        return map;
    }
}
#endif
