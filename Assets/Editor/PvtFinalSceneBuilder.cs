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

public static class PvtFinalSceneBuilder
{
    public const string ScenePath="Assets/Game/Scenes/PvtFinalProductionStress.unity";

    [MenuItem("Tools/Aedifica/PVT-FINAL/Create Production Stress Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)
            throw new InvalidOperationException(ScenePath+" already exists; preserve the existing scene.");
        PvtFinalMaterialBuilder.Generate();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraObject=new GameObject("PVT-Final Camera");
        cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>();
        camera.fieldOfView=60f; camera.nearClipPlane=0.3f; camera.farClipPlane=2500f;
        camera.transform.position=new Vector3(0f,160f,-240f);
        camera.transform.LookAt(new Vector3(0f,14f,0f));
        var sunObject=new GameObject("PVT-Final Sun");
        var sun=sunObject.AddComponent<Light>();
        sun.type=LightType.Directional; sun.intensity=1.2f; sun.shadows=LightShadows.Soft;
        sunObject.transform.rotation=Quaternion.Euler(50f,-30f,0f);
        var runner=new GameObject("PVT-Final Production Stress").AddComponent<PvtFinalRunner>();
        runner.Configure(camera,PvtFinalMaterialBuilder.Load("Stone"),
            PvtFinalMaterialBuilder.Load("Stone"),PvtFinalMaterialBuilder.Load("Brick"),
            PvtFinalMaterialBuilder.Load("Plaster"));
        runner.ConfigureDetails(PvtFinalMaterialBuilder.Load("Wood"),
            PvtFinalMaterialBuilder.Load("Ceramic"));
        runner.ConfigureEnvironment(EnvironmentMaterial("Terrain"),EnvironmentMaterial("Bark"),
            EnvironmentMaterial("Foliage"),EnvironmentMaterial("Grass"),
            EnvironmentMaterial("Road"),EnvironmentMaterial("Water"));
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=EditorBuildSettings.scenes.Where(item=>item.path!=ScenePath).ToList();
        scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("PVT-Final scene created. Review generated materials and visual fidelity before benchmarking.");
    }

    private static Material EnvironmentMaterial(string name)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Pvt2"+name+".mat");
        if (material==null) throw new InvalidOperationException("Missing PVT-2 material: "+name);
        return material;
    }

    public sealed class BuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EditorBuildSettings.scenes.Any(item=>item.enabled && item.path==ScenePath)) return;
            if (GraphicsSettings.defaultRenderPipeline==null ||
                GraphicsSettings.defaultRenderPipeline.name!="PC_RPAsset")
                throw new BuildFailedException("PVT-Final requires PC_RPAsset as default URP asset.");
            string[] dependencies=AssetDatabase.GetDependencies(ScenePath,true);
            foreach (string name in PvtFinalMaterialBuilder.Names)
            {
                PvtFinalMaterialBuilder.Load(name);
                if (!dependencies.Contains(PvtFinalMaterialBuilder.Folder+"/"+name+".mat"))
                    throw new BuildFailedException("PVT-Final scene lacks material "+name+
                        ". Recreate the scene only after preserving local changes.");
            }
            if (!dependencies.Contains("Assets/Game/Materials/Pvt2Terrain.mat"))
                throw new BuildFailedException("PVT-Final scene lacks the persistent URP terrain material.");
        }
    }
}
#endif
