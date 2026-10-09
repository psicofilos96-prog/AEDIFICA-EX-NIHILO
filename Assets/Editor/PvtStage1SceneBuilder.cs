#if UNITY_EDITOR
using System;
using System.Linq;
using Aedifica.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PvtStage1SceneBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/PvtStage1Benchmark.unity";
    private const string MaterialPath = "Assets/Game/Materials/ConstructionLab";

    [MenuItem("Tools/Aedifica/PVT-1/Create Stage 1 Benchmark Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException($"{ScenePath} already exists. Review it before replacing it.");
        Material neutral = Load("Neutral"), stone = Load("Stone"), brick = Load("Brick"), plaster = Load("Plaster");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("PVT-1 Benchmark Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 2500f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.transform.position = new Vector3(0f, 160f, -240f);
        camera.transform.LookAt(Vector3.zero);
        var sunObject = new GameObject("PVT-1 Sun");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var runner = new GameObject("PVT-1 Visual Benchmark").AddComponent<PvtStage1Runner>();
        runner.Configure(camera, neutral, stone, brick, plaster);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(item => item.path == ScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {ScenePath} with paired individual/combined rendering, camera and URP materials. Build Windows x64 with AEDIFICA_COMMIT set.");
    }

    private static Material Load(string name)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + name + ".mat");
        if (material == null) throw new InvalidOperationException("Missing benchmark material: " + name);
        return material;
    }
}
#endif
