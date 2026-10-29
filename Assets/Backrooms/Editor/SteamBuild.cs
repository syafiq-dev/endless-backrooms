using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu Backrooms > Build for Steam: Windows and Linux (Steam Deck) players into Builds/Steam/, where steam/app_build.vdf uploads them from.
// Needs the Windows Build Support (Mono) and Linux Build Support (Mono) modules in Unity Hub.
public static class SteamBuild
{
    [MenuItem("Backrooms/Build for Steam (Windows + Linux)")]
    static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) scenes = new[] { EditorSceneManager.GetActiveScene().path }; // no Build Profiles scene list: build the open scene
        if (string.IsNullOrEmpty(scenes[0])) { Debug.LogError("Save the scene first."); return; }
        if (Make(scenes, BuildTarget.StandaloneWindows64, "Builds/Steam/windows/EndlessBackrooms.exe") &&
            Make(scenes, BuildTarget.StandaloneLinux64, "Builds/Steam/linux/EndlessBackrooms.x86_64"))
            EditorUtility.RevealInFinder("Builds/Steam");
    }

    static bool Make(string[] scenes, BuildTarget target, string path)
    {
        var report = BuildPipeline.BuildPlayer(scenes, path, target, BuildOptions.None);
        if (report.summary.result == BuildResult.Succeeded) return true;
        Debug.LogError($"{target} build failed: {report.summary.totalErrors} errors, see the Console.");
        return false;
    }
}
