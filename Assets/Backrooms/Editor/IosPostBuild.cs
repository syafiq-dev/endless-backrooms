#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Fills the Xcode project's Info.plist after every iOS build, so nothing has to be set by hand in Xcode.
public static class IosPostBuild
{
    // Unity Ads' own SKAdNetwork id. If you add mediation networks, paste their ids here too.
    static readonly string[] AdNetworks = { "4dzt52r2t5.skadnetwork" };

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

        var items = root["SKAdNetworkItems"]?.AsArray() ?? root.CreateArray("SKAdNetworkItems");
        foreach (var id in AdNetworks)
        {
            bool found = false;
            foreach (var e in items.values)
                if (e.AsDict()["SKAdNetworkIdentifier"]?.AsString().ToLowerInvariant() == id) found = true;
            if (!found) items.AddDict().SetString("SKAdNetworkIdentifier", id);
        }

        plist.WriteToFile(plistPath);
    }
}
#endif
