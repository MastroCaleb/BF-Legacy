using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AggressiveGarbageCollection : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => 0;

    // 1. Clears memory right before the build pipeline starts
    public void OnPreprocessBuild(BuildReport report)
    {
        ExecuteDeepCleanup("Global Build Start");
    }

    // 2. Automatically triggers during the build every time Unity finishes processing a scene
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // Only run cleanup if the editor is actually compiling a build (not when entering Play Mode)
        if (BuildPipeline.isBuildingPlayer)
        {
            ExecuteDeepCleanup($"Compiling scene: {scene.name}");
        }
    }

    private void ExecuteDeepCleanup(string context)
    {
        // Unloads native editor graphics descriptors and texture data from memory
        EditorUtility.UnloadUnusedAssetsImmediate();
        
        // Flushes the internal rendering engine cache
        Resources.UnloadUnusedAssets();
        
        // Forces the managed C# Garbage Collector to collect and wait
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        
        Debug.Log($"[Memory Cleaner] Deep cleanup executed successfully at: {context}");
    }
}
