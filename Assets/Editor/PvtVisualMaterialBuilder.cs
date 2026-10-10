#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Produces persistent URP materials from the checked-in, licensed 1K source maps.
// Existing materials are validated and never silently overwritten.
public static class PvtVisualMaterialBuilder
{
    public const string Root = "Assets/Game/Art/PolyHaven";
    public const string Output = "Assets/Game/Art/VisualMaterials";

    private readonly struct Definition
    {
        public readonly string Name, Folder, Base, Normal, Mask;
        public readonly bool Cutout;
        public Definition(string name, string folder, string baseMap, string normal, string mask,
            bool cutout = false)
        { Name=name; Folder=folder; Base=baseMap; Normal=normal; Mask=mask; Cutout=cutout; }
    }

    private static readonly Definition[] Definitions = {
        Texture("Stone"), Texture("Brick"), Texture("Wood"), Texture("Plaster"),
        Texture("Roof"), Texture("Road"), Texture("Ground"),
        Model("PineBark","Pine","Pine_BarkAlbedo_1k.jpg","Pine_BarkNormal_1k.png",
            "Pine_BarkMetallicSmoothness_1k.png"),
        Model("PineTwig","Pine","Pine_TwigBase_1k.png","Pine_TwigNormal_1k.png",
            "Pine_TwigMetallicSmoothness_1k.png",true),
        Model("Shrub","Shrub","Shrub_Base_1k.png","Shrub_Normal_1k.png",
            "Shrub_MetallicSmoothness_1k.png",true),
        Model("Door","Door","Door_Albedo_1k.jpg","Door_Normal_1k.png",
            "Door_MetallicSmoothness_1k.png"),
        Model("Gate","Gate","Gate_Albedo_1k.jpg","Gate_Normal_1k.png",
            "Gate_MetallicSmoothness_1k.png"),
        Model("Statue","Statue","Statue_Albedo_1k.jpg","Statue_Normal_1k.png",
            "Statue_MetallicSmoothness_1k.png"),
        Free("DryGround"),Free("OldSandstone"),Free("BermudaGrass",true),
        Free("WeatheredRock"),Free("CeramicPot")
    };

    private static Definition Texture(string name) => new Definition(name,
        Root+"/Textures/"+name,name+"_Albedo_1k.jpg",name+"_Normal_1k.png",
        name+"_MetallicSmoothness_1k.png");
    private static Definition Model(string name,string folder,string baseMap,string normal,
        string mask,bool cutout=false) => new Definition(name,Root+"/Models/"+folder,
        baseMap,normal,mask,cutout);
    private static Definition Free(string name,bool cutout=false) => new Definition(name,
        "Assets/Game/Art/FreeReference/"+name,
        name+(cutout ? "_Base_1k.png" : "_Albedo_1k.jpg"),
        name+"_Normal_1k.png",name+"_MetallicSmoothness_1k.png",cutout);

    [MenuItem("Tools/Aedifica/Visual Milestone/Prepare CC0 URP Materials")]
    public static void Prepare()
    {
        Directory.CreateDirectory(Output);
        Shader lit=Shader.Find("Universal Render Pipeline/Lit");
        if (lit==null || !lit.isSupported)
            throw new InvalidOperationException("URP/Lit is required for the visual milestone.");
        foreach (Definition definition in Definitions)
        {
            Texture2D albedo=Import(definition.Folder+"/"+definition.Base,false,definition.Cutout);
            Texture2D normal=Import(definition.Folder+"/"+definition.Normal,true,false);
            Texture2D mask=Import(definition.Folder+"/"+definition.Mask,false,false,true);
            string path=Output+"/"+definition.Name+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material!=null)
            {
                if (material.shader!=lit || material.GetTexture("_BaseMap")!=albedo ||
                    material.GetTexture("_BumpMap")!=normal ||
                    material.GetTexture("_MetallicGlossMap")!=mask)
                    throw new InvalidOperationException("Review the existing URP material before reuse: "+path);
                continue;
            }
            material=new Material(lit) { name="PVT Visual "+definition.Name,enableInstancing=true };
            AssetDatabase.CreateAsset(material,path);
            material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BaseMap",albedo);
            material.SetTexture("_BumpMap",normal);
            material.SetFloat("_BumpScale",1f);
            material.SetTexture("_MetallicGlossMap",mask);
            material.SetFloat("_Metallic",1f);
            material.SetFloat("_Smoothness",1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (definition.Cutout)
            {
                material.SetFloat("_AlphaClip",1f);
                material.SetFloat("_Cutoff",0.45f);
                material.SetFloat("_Cull",0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.doubleSidedGI=true;
            }
            EditorUtility.SetDirty(material);
        }
        CreateWater(lit);
        CreateGlass(lit);
        AssetDatabase.SaveAssets();
    }

    private static void CreateWater(Shader lit)
    {
        string path=Output+"/Water.mat";
        Material existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing!=null)
        {
            if (existing.shader!=lit || existing.GetFloat("_Surface")!=1f)
                throw new InvalidOperationException("Review the existing visual water material: "+path);
            return;
        }
        var water=new Material(lit) {name="PVT Visual Water"};
        AssetDatabase.CreateAsset(water,path);
        water.SetColor("_BaseColor",new Color(0.13f,0.29f,0.34f,0.72f));
        water.SetFloat("_Surface",1f);
        water.SetFloat("_Blend",0f);
        water.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        water.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        water.SetFloat("_ZWrite",0f);
        water.SetFloat("_Smoothness",0.9f);
        water.SetFloat("_Metallic",0f);
        water.SetOverrideTag("RenderType","Transparent");
        water.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
        water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(water);
    }

    private static void CreateGlass(Shader lit)
    {
        string path=Output+"/Glass.mat";
        Material existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing!=null)
        {
            if (existing.shader!=lit || existing.GetFloat("_Surface")!=1f)
                throw new InvalidOperationException("Review the existing visual glass material: "+path);
            return;
        }
        var glass=new Material(lit) {name="PVT Visual Glass"};
        AssetDatabase.CreateAsset(glass,path);
        glass.SetColor("_BaseColor",new Color(0.48f,0.66f,0.69f,0.38f));
        glass.SetFloat("_Surface",1f);
        glass.SetFloat("_Blend",0f);
        glass.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite",0f);
        glass.SetFloat("_Smoothness",0.93f);
        glass.SetFloat("_Metallic",0f);
        glass.SetOverrideTag("RenderType","Transparent");
        glass.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(glass);
    }

    public static Material Load(string name)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/"+name+".mat");
        if (material==null || material.shader==null ||
            material.shader.name!="Universal Render Pipeline/Lit" ||
            (name!="Water" && name!="Glass" && (material.GetTexture("_BaseMap")==null ||
            material.GetTexture("_BumpMap")==null)))
            throw new InvalidOperationException("Prepare the CC0 visual material: "+name);
        return material;
    }

    private static Texture2D Import(string path,bool normal,bool alpha,bool linear=false)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required CC0 texture is missing",path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer==null) throw new InvalidOperationException("TextureImporter missing: "+path);
        bool changed=importer.maxTextureSize!=1024 || !importer.mipmapEnabled ||
            importer.textureType!=(normal ? TextureImporterType.NormalMap : TextureImporterType.Default) ||
            importer.sRGBTexture==(normal || linear) || (alpha && !importer.alphaIsTransparency) ||
            importer.textureCompression!=TextureImporterCompression.CompressedHQ;
        if (changed)
        {
            importer.maxTextureSize=1024;
            importer.mipmapEnabled=true;
            importer.textureType=normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture=!(normal || linear);
            importer.alphaIsTransparency=alpha;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture==null) throw new InvalidOperationException("Texture failed import: "+path);
        return texture;
    }
}
#endif
