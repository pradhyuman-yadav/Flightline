using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Flightline.EditorTools
{
    // iOS: settings + Xcode project export. Windows can export; compiling, signing and uploading need a Mac with Xcode.
    public static class FlightlineIOS
    {
        const string Scene = "Assets/Flightline/Scenes/Flightline.unity";
        const string OutDir = "Builds/iOS";

        [MenuItem("Flightline/iOS/Prepare iOS Settings (icon, launch screen, player)")]
        public static void ApplyIosSettings()
        {
            var t = UnityEditor.Build.NamedBuildTarget.iOS;
            PlayerSettings.companyName = "Pradhyuman";
            PlayerSettings.productName = "Flightline";
            PlayerSettings.SetApplicationIdentifier(t, "com.pradhyuman.flightline");
            PlayerSettings.bundleVersion = FlightlineMenu.Version;
            if (!int.TryParse(PlayerSettings.iOS.buildNumber, out var b) || b < 1) PlayerSettings.iOS.buildNumber = "1";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;   // portrait phone game; runs on iPad in compatibility mode
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge; // a swipe up mid-flight needs a second swipe
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;           // pick your Team in Xcode or set the Team ID here
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(t, 1);                            // ARM64
            PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            EditorUserBuildSettings.development = false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };

            Branding.ApplyToPlayerSettings();
            Branding.ApplyIos();
            AssetDatabase.SaveAssets();
            Debug.Log($"Flightline: iOS settings applied. v{PlayerSettings.bundleVersion} ({PlayerSettings.iOS.buildNumber}), iPhone, iOS 15+.");
        }

        [MenuItem("Flightline/iOS/Export Xcode Project")]
        public static void ExportXcode()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                EditorUtility.DisplayDialog("iOS Build Support missing", "Install it in Unity Hub: Installs > 6000.6.4f1 > Add modules > iOS Build Support.", "OK");
                return;
            }
            // The Info.plist post-process below only compiles (UNITY_IOS) while iOS is the active target, so switch first.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                EditorUtility.DisplayDialog("Switched to iOS", "The active platform is now iOS. Wait for scripts to finish compiling, then run Flightline > iOS > Export Xcode Project again.", "OK");
                return;
            }
            ApplyIosSettings();
            string prevBuild = PlayerSettings.iOS.buildNumber;
            PlayerSettings.iOS.buildNumber = (int.Parse(prevBuild) + 1).ToString();
            Directory.CreateDirectory(OutDir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene }, locationPathName = OutDir + "/Flightline", target = BuildTarget.iOS, targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None
            });
            var sum = report.summary;
            // failed exports never reach App Store Connect: give the build number back; keep a good one on disk
            if (sum.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) PlayerSettings.iOS.buildNumber = prevBuild;
            else AssetDatabase.SaveAssets();
            string msg = $"{sum.result} | {sum.totalErrors} errors | {sum.totalTime:mm\\:ss} | build {PlayerSettings.iOS.buildNumber} | {Path.GetFullPath(OutDir + "/Flightline")}";
            File.WriteAllText($"{OutDir}/last_build.txt", msg);
            Debug.Log("Flightline iOS export: " + msg);
        }

#if UNITY_IOS
        // App Store Connect asks about encryption on every upload unless the plist answers it. Flightline uses none.
        [PostProcessBuild(100)]
        public static void OnPostBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument(); plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
        }
#endif
    }
}
