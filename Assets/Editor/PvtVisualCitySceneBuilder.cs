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

public static class PvtVisualCitySceneBuilder
{
    public const string ScenePath="Assets/Game/Scenes/PvtVisualCity.unity";

    [MenuItem("Tools/Aedifica/Visual Milestone/Create Historic City Review Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)
            throw new InvalidOperationException("Preserve the existing visual review scene: "+ScenePath);
        PvtVisualAssetBuilder.Prepare();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraObject=new GameObject("Historic City Review Camera");
        cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>();
        camera.fieldOfView=60f;
        camera.nearClipPlane=0.15f;
        camera.farClipPlane=1600f;
        camera.allowHDR=true;
        camera.allowMSAA=true;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(0.65f,0.70f,0.74f);
        RenderSettings.ambientEquatorColor=new Color(0.49f,0.47f,0.42f);
        RenderSettings.ambientGroundColor=new Color(0.29f,0.26f,0.23f);
        RenderSettings.fog=true;
        RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogColor=new Color(0.70f,0.72f,0.71f);
        RenderSettings.fogStartDistance=150f;
        RenderSettings.fogEndDistance=650f;
        var sunObject=new GameObject("Afternoon sun");
        Light sun=sunObject.AddComponent<Light>();
        sun.type=LightType.Directional;
        sun.intensity=1.2f;
        sun.shadows=LightShadows.Soft;
        sun.shadowStrength=0.84f;
        sunObject.transform.rotation=Quaternion.Euler(42f,-125f,0f);
        var runner=new GameObject("PVT Historic City Review").AddComponent<PvtVisualCityRunner>();
        string ground=PvtVisualMaterialBuilder.Root+"/Textures/Ground/Ground_";
        runner.Configure(camera,PvtVisualMaterialBuilder.Load("Stone"),
            PvtVisualMaterialBuilder.Load("Brick"),PvtVisualMaterialBuilder.Load("Wood"),
            PvtVisualMaterialBuilder.Load("Plaster"),PvtVisualMaterialBuilder.Load("Roof"),
            PvtVisualMaterialBuilder.Load("Road"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Pvt2Terrain.mat"),
            PvtVisualMaterialBuilder.Load("Water"),PvtVisualMaterialBuilder.Load("Glass"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(ground+"Albedo_1k.jpg"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(ground+"Normal_1k.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(ground+"MetallicSmoothness_1k.png"),
            PvtVisualAssetBuilder.Load("Pine"),PvtVisualAssetBuilder.Load("Shrub"),
            PvtVisualAssetBuilder.Load("CastleDoor"),PvtVisualAssetBuilder.Load("IronGate"),
            PvtVisualAssetBuilder.Load("GothicStatue"));
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=EditorBuildSettings.scenes.Where(item=>item.path!=ScenePath).ToList();
        scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Historic city review scene created. Run Play, inspect five views, and approve visuals manually before benchmarking.");
    }

    public sealed class BuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EditorBuildSettings.scenes.Any(item=>item.enabled && item.path==ScenePath)) return;
            if (GraphicsSettings.defaultRenderPipeline==null ||
                GraphicsSettings.defaultRenderPipeline.name!="PC_RPAsset")
                throw new BuildFailedException("Historic city review requires PC_RPAsset.");
            string[] dependencies=AssetDatabase.GetDependencies(ScenePath,true);
            foreach (string material in new[] {"Stone","Brick","Wood","Plaster","Roof",
                "Road","PineBark","PineTwig","Shrub","Door","Gate","Statue","Water","Glass"})
            {
                string path=PvtVisualMaterialBuilder.Output+"/"+material+".mat";
                PvtVisualMaterialBuilder.Load(material);
                if (!dependencies.Contains(path))
                    throw new BuildFailedException("Visual city scene does not reference "+path);
            }
            foreach (string name in new[] {"Pine","Shrub","CastleDoor","IronGate","GothicStatue"})
                if (!dependencies.Contains(PvtVisualAssetBuilder.Folder+"/"+name+".prefab"))
                    throw new BuildFailedException("Visual city scene lacks CC0 prefab "+name);
        }
    }
}
#endif
