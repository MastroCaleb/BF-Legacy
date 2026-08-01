using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class ForceGarbageCollection : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        // Frees unused asset memory right before the build pipeline starts
        EditorUtility.UnloadUnusedAssetsImmediate();
        System.GC.Collect();
    }
}