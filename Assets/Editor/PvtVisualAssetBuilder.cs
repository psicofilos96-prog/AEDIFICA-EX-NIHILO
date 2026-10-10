#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PvtVisualAssetBuilder
{
    public const string Folder = "Assets/Game/Art/VisualPrefabs";
    private const string Models = PvtVisualMaterialBuilder.Root + "/Models/LOD/";

    [MenuItem("Tools/Aedifica/Visual Milestone/Prepare CC0 Models and LOD")]
    public static void Prepare()
    {
        PvtVisualMaterialBuilder.Prepare();
        Directory.CreateDirectory(Folder);
        BuildLod("Pine",new[] { "PineBark", "PineTwig" },7.5f);
        BuildLod("Shrub",new[] { "Shrub" },2.2f);
        BuildProp("CastleDoor","Door");
        BuildProp("IronGate","Gate");
        BuildProp("GothicStatue","Statue");
        AssetDatabase.SaveAssets();
    }

    public static GameObject Load(string name)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab");
        if (prefab==null) throw new InvalidOperationException("Prepare required CC0 prefab: "+name);
        return prefab;
    }

    private static void BuildLod(string name,string[] materials,float height)
    {
        string path=Folder+"/"+name+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) return;
        var root=new GameObject("PVT Visual "+name);
        try
        {
            var lods=new LOD[3];
            for (int i=0;i<3;i++)
            {
                GameObject source=Source(name+"_LOD"+i);
                GameObject level=(GameObject)PrefabUtility.InstantiatePrefab(source);
                level.name=name+" LOD"+i;
                level.transform.SetParent(root.transform,false);
                MeshRenderer[] renderers=level.GetComponentsInChildren<MeshRenderer>();
                Assign(renderers,materials);
                Bounds bounds=renderers[0].bounds;
                for (int j=1;j<renderers.Length;j++) bounds.Encapsulate(renderers[j].bounds);
                if (bounds.size.y<=0f)
                    throw new InvalidOperationException("CC0 LOD has zero height: "+level.name);
                level.transform.localScale*=height/bounds.size.y;
                bounds=renderers[0].bounds;
                for (int j=1;j<renderers.Length;j++) bounds.Encapsulate(renderers[j].bounds);
                level.transform.position+=Vector3.down*bounds.min.y;
                lods[i]=new LOD(i==0 ? 0.55f : i==1 ? 0.18f : 0.035f,renderers);
            }
            var group=root.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildProp(string sourceName,string materialName)
    {
        string path=Folder+"/"+sourceName+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null) return;
        GameObject source=Source(sourceName);
        GameObject root=(GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            root.name="PVT Visual "+sourceName;
            Assign(root.GetComponentsInChildren<MeshRenderer>(),new[] {materialName});
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static GameObject Source(string name)
    {
        string path=Models+name+".fbx";
        if (!File.Exists(path)) throw new FileNotFoundException("CC0 mesh is missing",path);
        GameObject source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source==null) throw new InvalidOperationException("FBX import failed: "+path);
        return source;
    }

    private static void Assign(IReadOnlyList<MeshRenderer> renderers,string[] names)
    {
        if (renderers.Count==0) throw new InvalidOperationException("CC0 model contains no renderer.");
        foreach (MeshRenderer renderer in renderers)
        {
            Material[] prior=renderer.sharedMaterials;
            if (prior.Length==0)
                throw new InvalidOperationException("Imported CC0 mesh has no material slots: "+renderer.name);
            var chosen=new Material[prior.Length];
            for (int i=0;i<chosen.Length;i++)
            {
                bool twig=names.Length>1 && ((prior[i]!=null &&
                    prior[i].name.IndexOf("twig",StringComparison.OrdinalIgnoreCase)>=0) || i>0);
                chosen[i]=PvtVisualMaterialBuilder.Load(names[twig ? 1 : 0]);
            }
            renderer.sharedMaterials=chosen;
        }
    }
}
#endif
