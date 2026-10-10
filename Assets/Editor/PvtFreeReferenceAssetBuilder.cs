#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Converts checked-in CC0 FBX meshes to shared URP prefabs on the user's Unity.
public static class PvtFreeReferenceAssetBuilder
{
    public const string Folder="Assets/Game/Art/FreeReference/Prefabs";
    private const string Models="Assets/Game/Art/FreeReference/Models/";

    [MenuItem("Tools/Aedifica/Free Asset Reference/Prepare CC0 Assets")]
    public static void Prepare()
    {
        PvtVisualAssetBuilder.Prepare();
        System.IO.Directory.CreateDirectory(Folder);
        BuildLod("BermudaGrass",0.4f,true);
        BuildLod("WeatheredRock",0.9f,false);
        BuildPot();
        AssetDatabase.SaveAssets();
    }

    public static GameObject Load(string name)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab");
        if (prefab==null) throw new InvalidOperationException("Missing prepared CC0 prefab: "+name);
        return prefab;
    }

    private static void BuildLod(string name,float targetHeight,bool noShadow)
    {
        string path=Folder+"/"+name+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) return;
        var root=new GameObject("CC0 "+name+" LOD");
        try
        {
            var lods=new LOD[3];
            Material material=PvtVisualMaterialBuilder.Load(name);
            for (int level=0;level<3;level++)
            {
                GameObject instance=Instance(name+"_LOD"+level);
                instance.transform.SetParent(root.transform,false);
                MeshRenderer[] renderers=instance.GetComponentsInChildren<MeshRenderer>();
                Assign(renderers,material,noShadow);
                FitHeight(instance,renderers,targetHeight);
                lods[level]=new LOD(level==0 ? 0.55f : level==1 ? 0.17f : 0.025f,renderers);
            }
            var group=root.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildPot()
    {
        string path=Folder+"/CeramicPot.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) return;
        var root=new GameObject("CC0 CeramicPot");
        try
        {
            GameObject instance=Instance("CeramicPot");
            instance.transform.SetParent(root.transform,false);
            MeshRenderer[] renderers=instance.GetComponentsInChildren<MeshRenderer>();
            Assign(renderers,PvtVisualMaterialBuilder.Load("CeramicPot"),false);
            FitHeight(instance,renderers,0.55f);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static GameObject Instance(string name)
    {
        string path=Models+name+".fbx";
        GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model==null) throw new InvalidOperationException("Required CC0 FBX has not imported: "+path);
        return (GameObject)PrefabUtility.InstantiatePrefab(model);
    }

    private static void Assign(MeshRenderer[] renderers,Material material,bool noShadow)
    {
        if (renderers.Length==0) throw new InvalidOperationException("CC0 FBX has no MeshRenderer.");
        foreach (MeshRenderer renderer in renderers)
        {
            Material[] slots=renderer.sharedMaterials;
            if (slots.Length==0) slots=new Material[1];
            for (int i=0;i<slots.Length;i++) slots[i]=material;
            renderer.sharedMaterials=slots;
            renderer.shadowCastingMode=noShadow ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
    }

    private static void FitHeight(GameObject root,Renderer[] renderers,float height)
    {
        Bounds bounds=renderers[0].bounds;
        for (int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y<=0f) throw new InvalidOperationException("CC0 model height is zero: "+root.name);
        root.transform.localScale*=height/bounds.size.y;
        bounds=renderers[0].bounds;
        for (int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds);
        root.transform.position+=Vector3.down*bounds.min.y;
    }
}
#endif
