#if UNITY_EDITOR
using System;
using System.Linq;
using Aedifica.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Pvt2SceneBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/Pvt2IntegratedWorld.unity";
    private const string MaterialPath = "Assets/Game/Materials/ConstructionLab";

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
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(item=>item.path==ScenePath))
            scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("PVT-2 scene created. Play without -pvt2-run for visual preview; launch the Release Player with -pvt2-run for the full benchmark.");
    }

    private static Material Load(string name)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath+name+".mat");
        if (material==null) throw new InvalidOperationException("Missing URP material: "+name);
        return material;
    }
}
#endif
