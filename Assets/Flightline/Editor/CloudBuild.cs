using UnityEditor;
using UnityEngine;

namespace Flightline.EditorTools
{
    // Unity Build Automation hook. Set the target's "Pre-Export Method Name" to:
    //   Flightline.EditorTools.CloudBuild.PreExport
    // Player settings (icons, splash, bundle id, iOS/Android options) are already saved in ProjectSettings,
    // so this only stamps a unique, increasing build number that App Store Connect / Play require per upload.
    public static class CloudBuild
    {
#if UNITY_CLOUD_BUILD
        public static void PreExport(UnityEngine.CloudBuild.BuildManifestObject manifest)
        {
            Apply(manifest.GetValue<string>("buildNumber"));
        }
#else
        public static void PreExport() => Apply(null);
#endif

        static void Apply(string buildNumber)
        {
            EditorUserBuildSettings.development = false;
            PlayerSettings.bundleVersion = FlightlineMenu.Version;
            if (int.TryParse(buildNumber, out var n) && n > 0)
            {
                // Offset keeps cloud numbers above anything uploaded from local builds.
                int code = 1000 + n;
                PlayerSettings.iOS.buildNumber = code.ToString();
                PlayerSettings.Android.bundleVersionCode = code;
            }
            Debug.Log($"Flightline cloud build: v{PlayerSettings.bundleVersion} build {PlayerSettings.iOS.buildNumber} (Build Automation #{buildNumber ?? "local"})");
        }
    }
}
