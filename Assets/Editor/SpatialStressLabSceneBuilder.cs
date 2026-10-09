#if UNITY_EDITOR
using System;
using System.Linq;
using Aedifica.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SpatialStressLabSceneBuilder
{
    private const string ScenePath = "Assets/Game/Scenes/SpatialStressLab.unity";

    [MenuItem("Tools/Aedifica/E2a/Create Spatial Stress Lab Scene")]
    public static void CreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException($"{ScenePath} already exists. Review it before replacing it.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("E2a Spatial Stress Lab").AddComponent<SpatialStressLabRunner>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(item => item.path == ScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {ScenePath} and enabled it in Build Settings. Build a Windows x64 Player, launch it with AEDIFICA_COMMIT set, and read the CSV in persistentDataPath.");
    }
}
#endif
