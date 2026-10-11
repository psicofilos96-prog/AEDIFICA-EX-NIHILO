#if UNITY_EDITOR
using System;
using System.Linq;
using Aedifica.Rendering;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PvtGrandRiverVillageSceneBuilder
{
    public const string ScenePath="Assets/Game/Scenes/PvtGrandRiverVillage.unity";
    public const string ScalePath="Assets/Game/Scenes/PvtGrandRiverScaleLab.unity";
    private const string Free="Assets/Game/Art/FreeReference/";

    [MenuItem("Tools/Aedifica/Grand River Village/Create Village Scene")]
    public static void CreateScene() => Create(false);

    [MenuItem("Tools/Aedifica/Grand River Village/Create Scale Lab Scene")]
    public static void CreateScaleScene() => Create(true);

    private static void Create(bool scale)
    {
        string path=scale ? ScalePath : ScenePath;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path)!=null)
            throw new InvalidOperationException("Existing scene will not be overwritten: "+path);
        PvtGrandVillageAssetBuilder.Prepare();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraObject=new GameObject("Grand River Village Camera");
        cameraObject.tag="MainCamera";
        Camera camera=cameraObject.AddComponent<Camera>();
        camera.fieldOfView=58f;camera.nearClipPlane=0.1f;camera.farClipPlane=scale ? 3500f : 350f;
        camera.allowHDR=true;camera.allowMSAA=true;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(0.62f,0.68f,0.74f);
        RenderSettings.ambientEquatorColor=new Color(0.5f,0.47f,0.41f);
        RenderSettings.ambientGroundColor=new Color(0.3f,0.26f,0.22f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogColor=new Color(0.7f,0.72f,0.72f);
        RenderSettings.fogStartDistance=70f;RenderSettings.fogEndDistance=210f;
        var sunObject=new GameObject("Late afternoon sun");
        Light sun=sunObject.AddComponent<Light>();
        sun.type=LightType.Directional;sun.intensity=1.15f;
        sun.shadows=LightShadows.Soft;sun.shadowStrength=0.82f;
        sunObject.transform.rotation=Quaternion.Euler(43f,-130f,0f);
        var host=new GameObject(scale ? "Grand River Scale Lab" : "Editable Grand River Village");
        string oldGround=PvtVisualMaterialBuilder.Root+"/Textures/Ground/Ground_";
        Material stone=PvtVisualMaterialBuilder.Load("OldSandstone"),
            brick=PvtVisualMaterialBuilder.Load("Brick"),wood=PvtVisualMaterialBuilder.Load("Wood"),
            plaster=PvtVisualMaterialBuilder.Load("Plaster"),roof=PvtVisualMaterialBuilder.Load("Roof"),
            road=PvtVisualMaterialBuilder.Load("Road"),
            terrain=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Pvt2Terrain.mat"),
            glass=PvtVisualMaterialBuilder.Load("Glass"),water=PvtVisualMaterialBuilder.Load("Water");
        Texture2D dryA=Texture("DryGround","Albedo_1k.jpg"),
            dryN=Texture("DryGround","Normal_1k.png"),
            dryM=Texture("DryGround","MetallicSmoothness_1k.png"),
            groundA=AssetDatabase.LoadAssetAtPath<Texture2D>(oldGround+"Albedo_1k.jpg"),
            groundN=AssetDatabase.LoadAssetAtPath<Texture2D>(oldGround+"Normal_1k.png"),
            groundM=AssetDatabase.LoadAssetAtPath<Texture2D>(oldGround+"MetallicSmoothness_1k.png");
        GameObject grass=PvtFreeReferenceAssetBuilder.Load("BermudaGrass"),
            rock=PvtFreeReferenceAssetBuilder.Load("WeatheredRock"),
            pot=PvtFreeReferenceAssetBuilder.Load("CeramicPot"),
            shrub=PvtVisualAssetBuilder.Load("Shrub"),
            coastal=PvtGrandVillageAssetBuilder.LoadPrefab(),pine=PvtVisualAssetBuilder.Load("Pine");
        if (scale)
            host.AddComponent<PvtGrandVillageScaleRunner>().Configure(camera,stone,brick,wood,
                plaster,roof,road,terrain,glass,water,dryA,dryN,dryM,groundA,groundN,groundM,
                coastal,pine,shrub,rock);
        else
        {
            var runner=host.AddComponent<PvtFreeAssetsReferenceRunner>();
            runner.Configure(camera,stone,
                brick,wood,plaster,roof,road,terrain,glass,dryA,dryN,dryM,
                groundA,groundN,groundM,grass,rock,pot,shrub);
            runner.ConfigureGrand(coastal,pine,water);
        }
        EditorSceneManager.SaveScene(scene,path);
        var scenes=EditorBuildSettings.scenes.Where(item=>item.path!=path).ToList();
        scenes.Insert(0,new EditorBuildSettingsScene(path,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log(scale ? "Grand river scale lab scene created: run a Release Player with -grand-scale-run."
            : "Grand river village scene created: run Play and review six views in Game view.");
    }

    private static Texture2D Texture(string name,string suffix)
    {
        string path=Free+name+"/"+name+"_"+suffix;
        Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture==null) throw new InvalidOperationException("Required CC0 map missing: "+path);
        return texture;
    }

    public sealed class BuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            string scene=EditorBuildSettings.scenes.Any(item=>item.enabled && item.path==ScalePath)
                ? ScalePath : ScenePath;
            if (!EditorBuildSettings.scenes.Any(item=>item.enabled && item.path==scene)) return;
            if (GraphicsSettings.defaultRenderPipeline==null ||
                GraphicsSettings.defaultRenderPipeline.name!="PC_RPAsset")
                throw new BuildFailedException("Grand river village requires PC_RPAsset.");
            string[] dependencies=AssetDatabase.GetDependencies(scene,true);
            foreach (string name in new[] {"DryGround","OldSandstone","BermudaGrass",
                "WeatheredRock","CeramicPot"})
            {
                string material=PvtVisualMaterialBuilder.Output+"/"+name+".mat";
                if (name=="OldSandstone" && !dependencies.Contains(material))
                    throw new BuildFailedException("Village scene lacks persistent URP material: "+material);
                Material checkedMaterial=PvtVisualMaterialBuilder.Load(name);
                if (checkedMaterial.shader==null ||
                    checkedMaterial.shader.name!="Universal Render Pipeline/Lit")
                    throw new BuildFailedException("Incorrect URP shader on "+material);
            }
            foreach (string suffix in new[] {"Albedo_1k.jpg","Normal_1k.png",
                "MetallicSmoothness_1k.png"})
            {
                string texture=Free+"DryGround/DryGround_"+suffix;
                if (!dependencies.Contains(texture))
                    throw new BuildFailedException("Village scene lacks CC0 terrain map: "+texture);
            }
            string coastal=PvtGrandVillageAssetBuilder.PrefabPath;
            if (!dependencies.Contains(coastal))
                throw new BuildFailedException("Grand river scene lacks CC0 coastal tree: "+coastal);
            foreach (string name in new[] {"CoastalTrunk","CoastalBranches","CoastalLeaves"})
            {
                string material=PvtGrandVillageAssetBuilder.Folder+"/"+name+".mat";
                Material checkedMaterial=AssetDatabase.LoadAssetAtPath<Material>(material);
                if (!dependencies.Contains(material) || checkedMaterial==null ||
                    checkedMaterial.shader==null ||
                    checkedMaterial.shader.name!="Universal Render Pipeline/Lit")
                    throw new BuildFailedException("Grand river scene lacks CC0 URP tree material: "+material);
            }
            string water=PvtVisualMaterialBuilder.Output+"/Water.mat";
            if (!dependencies.Contains(water))
                throw new BuildFailedException("Grand river scene lacks persistent URP water: "+water);
            string pine=PvtVisualAssetBuilder.Folder+"/Pine.prefab";
            if (!dependencies.Contains(pine))
                throw new BuildFailedException("Grand river scene lacks CC0 pine LOD: "+pine);
            foreach (string name in scene==ScalePath
                ? new[] {"WeatheredRock"} : new[] {"BermudaGrass","WeatheredRock","CeramicPot"})
            {
                string prefab=PvtFreeReferenceAssetBuilder.Folder+"/"+name+".prefab";
                if (!dependencies.Contains(prefab))
                    throw new BuildFailedException("Village scene lacks persistent CC0 prefab: "+prefab);
            }
        }
    }
}
#endif
