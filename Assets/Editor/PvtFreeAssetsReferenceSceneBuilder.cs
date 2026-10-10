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

public static class PvtFreeAssetsReferenceSceneBuilder
{
    public const string ScenePath="Assets/Game/Scenes/PvtFreeAssetsReference.unity";
    private const string Free="Assets/Game/Art/FreeReference/";

    [MenuItem("Tools/Aedifica/Free Asset Reference/Create Reference Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)
            throw new InvalidOperationException("Existing reference scene will not be overwritten: "+ScenePath);
        PvtFreeReferenceAssetBuilder.Prepare();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraObject=new GameObject("Free Asset Reference Camera");
        cameraObject.tag="MainCamera";
        Camera camera=cameraObject.AddComponent<Camera>();
        camera.fieldOfView=58f;camera.nearClipPlane=0.1f;camera.farClipPlane=300f;
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
        var runner=new GameObject("One editable historic residence").AddComponent<PvtFreeAssetsReferenceRunner>();
        string oldGround=PvtVisualMaterialBuilder.Root+"/Textures/Ground/Ground_";
        runner.Configure(camera,PvtVisualMaterialBuilder.Load("OldSandstone"),
            PvtVisualMaterialBuilder.Load("Brick"),PvtVisualMaterialBuilder.Load("Wood"),
            PvtVisualMaterialBuilder.Load("Plaster"),PvtVisualMaterialBuilder.Load("Roof"),
            PvtVisualMaterialBuilder.Load("Road"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Pvt2Terrain.mat"),
            PvtVisualMaterialBuilder.Load("Glass"),
            Texture("DryGround","Albedo_1k.jpg"),Texture("DryGround","Normal_1k.png"),
            Texture("DryGround","MetallicSmoothness_1k.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(oldGround+"Albedo_1k.jpg"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(oldGround+"Normal_1k.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>(oldGround+"MetallicSmoothness_1k.png"),
            PvtFreeReferenceAssetBuilder.Load("BermudaGrass"),
            PvtFreeReferenceAssetBuilder.Load("WeatheredRock"),
            PvtFreeReferenceAssetBuilder.Load("CeramicPot"),PvtVisualAssetBuilder.Load("Shrub"));
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=EditorBuildSettings.scenes.Where(item=>item.path!=ScenePath).ToList();
        scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Free asset reference created: run Play and review the four views in Game view.");
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
            if (!EditorBuildSettings.scenes.Any(item=>item.enabled && item.path==ScenePath)) return;
            if (GraphicsSettings.defaultRenderPipeline==null ||
                GraphicsSettings.defaultRenderPipeline.name!="PC_RPAsset")
                throw new BuildFailedException("Free asset reference requires PC_RPAsset.");
            string[] dependencies=AssetDatabase.GetDependencies(ScenePath,true);
            foreach (string name in new[] {"DryGround","OldSandstone","BermudaGrass",
                "WeatheredRock","CeramicPot"})
            {
                string material=PvtVisualMaterialBuilder.Output+"/"+name+".mat";
                if (!dependencies.Contains(material) && name!="BermudaGrass" &&
                    name!="WeatheredRock" && name!="CeramicPot")
                    throw new BuildFailedException("Scene lacks persistent URP material: "+material);
                Material checkedMaterial=PvtVisualMaterialBuilder.Load(name);
                if (checkedMaterial.shader==null ||
                    checkedMaterial.shader.name!="Universal Render Pipeline/Lit")
                    throw new BuildFailedException("Incorrect URP shader on "+material);
            }
            foreach (string name in new[] {"BermudaGrass","WeatheredRock","CeramicPot"})
            {
                string prefab=PvtFreeReferenceAssetBuilder.Folder+"/"+name+".prefab";
                if (!dependencies.Contains(prefab))
                    throw new BuildFailedException("Scene lacks persistent CC0 prefab: "+prefab);
            }
        }
    }
}
#endif
