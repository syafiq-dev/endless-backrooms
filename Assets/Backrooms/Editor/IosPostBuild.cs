#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Sets the Xcode project's Info.plist after every iOS build. (The AdMob plugin adds its own app ID and SKAdNetwork IDs.)
public static class IosPostBuild
{
    [PostProcessBuild]
    static void OnPostBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        var plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;

        // No custom encryption: skips the export-compliance question on every TestFlight upload.
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

        plist.WriteToFile(plistPath);
    }
}
#endif
