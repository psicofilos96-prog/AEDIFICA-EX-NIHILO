#if UNITY_EDITOR
using System;
using System.Linq;
using Aedifica.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class Pvt2SceneBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/Pvt2IntegratedWorld.unity";
    private const string MaterialPath = "Assets/Game/Materials/ConstructionLab";
    private const string EnvironmentMaterialPath = "Assets/Game/Materials/Pvt2";

    [MenuItem("Tools/Aedifica/PVT-2/Create Integrated World Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException(ScenePath + " exists. Review it before replacing it.");
        Material neutral=Load("Neutral"), stone=Load("Stone"), brick=Load("Brick"), plaster=Load("Plaster");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cameraObject=new GameObject("PVT-2 Integrated Camera");
        cameraObject.tag="MainCamera";
        Camera camera=cameraObject.AddComponent<Camera>();
        camera.fieldOfView=60f;
        camera.nearClipPlane=0.3f;
        camera.farClipPlane=2500f;
        camera.clearFlags=CameraClearFlags.Skybox;
        camera.transform.position=new Vector3(0f,160f,-240f);
        camera.transform.LookAt(new Vector3(0f,14f,0f));
        var sunObject=new GameObject("PVT-2 Sun");
        Light sun=sunObject.AddComponent<Light>();
        sun.type=LightType.Directional;
        sun.intensity=1.2f;
        sun.shadows=LightShadows.Soft;
        sunObject.transform.rotation=Quaternion.Euler(50f,-30f,0f);
        var runner=new GameObject("PVT-2 Integrated World").AddComponent<Pvt2IntegratedRunner>();
        runner.Configure(camera,neutral,stone,brick,plaster);
        ConfigureEnvironment(runner);
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(item=>item.path==ScenePath))
            scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("PVT-2 scene created. Play without -pvt2-run for visual preview; launch the Release Player with -pvt2-run for the full benchmark.");
    }

    [MenuItem("Tools/Aedifica/PVT-2/Update Material References In Existing Scene")]
    public static void UpdateMaterialReferences()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)
            throw new InvalidOperationException("Create the PVT-2 scene before updating its materials.");
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var runner=UnityEngine.Object.FindFirstObjectByType<Pvt2IntegratedRunner>();
        if (runner==null) throw new InvalidOperationException("PVT-2 runner is missing from the scene.");
        ConfigureEnvironment(runner);
        EditorUtility.SetDirty(runner);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("PVT-2 persistent environment materials assigned and scene saved. Rebuild the Windows Player.");
    }

    private static void ConfigureEnvironment(Pvt2IntegratedRunner runner)
    {
        runner.ConfigureEnvironment(LoadEnvironment("Terrain"),LoadEnvironment("Bark"),
            LoadEnvironment("Foliage"),LoadEnvironment("Grass"),
            LoadEnvironment("Road"),LoadEnvironment("Water"));
    }

    private static Material LoadEnvironment(string name)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(EnvironmentMaterialPath+name+".mat");
        if (material==null) throw new InvalidOperationException("Missing PVT-2 material asset: "+name);
        return material;
    }

    // The generated scene is local. Verify its serialized dependencies before any Player build.
    public sealed class BuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!EditorBuildSettings.scenes.Any(item=>item.enabled && item.path==ScenePath)) return;
            if (GraphicsSettings.defaultRenderPipeline==null)
                throw new BuildFailedException("PVT-2 requires a default URP Render Pipeline Asset in Graphics Settings.");
            if (GraphicsSettings.defaultRenderPipeline.name!="PC_RPAsset")
                throw new BuildFailedException("PVT-2 requires PC_RPAsset as the default Render Pipeline Asset.");
            string[] dependencies=AssetDatabase.GetDependencies(ScenePath,true);
            foreach (string name in new[] { "Terrain", "Bark", "Foliage", "Grass", "Road", "Water" })
            {
                string path=EnvironmentMaterialPath+name+".mat";
                if (!dependencies.Contains(path))
                    throw new BuildFailedException("PVT-2 scene does not reference "+path+
                        ". Run Tools/Aedifica/PVT-2/Update Material References In Existing Scene.");
                LoadEnvironment(name);
            }
        }
    }

    private static Material Load(string name)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath+name+".mat");
        if (material==null) throw new InvalidOperationException("Missing URP material: "+name);
        return material;
    }
}
#endif
