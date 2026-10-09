#if UNITY_EDITOR
using System;
using System.Linq;
using Aedifica.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ContinuousSnapBenchmarkSceneBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/ContinuousSnapBenchmark.unity";

    [MenuItem("Tools/Aedifica/E2b.2/Create Continuous Snap Benchmark Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException($"{ScenePath} already exists. Review it before replacing it.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("E2b.2 Continuous Snap Benchmark").AddComponent<ContinuousSnapBenchmarkRunner>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(item => item.path == ScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {ScenePath} as the first enabled build scene. Build Windows x64 and set AEDIFICA_COMMIT.");
    }
}
#endif
