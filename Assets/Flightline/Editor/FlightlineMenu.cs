using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Flightline.EditorTools
{
    public static class FlightlineMenu
    {
        const string Scene = "Assets/Flightline/Scenes/Flightline.unity";
        const string OutDir = "Builds/Android";
        public const string Version = "1.0.0";

        [MenuItem("Flightline/Open Game Scene")]
        static void OpenScene() => EditorSceneManager.OpenScene(Scene);

        // ---------------------------------------------------------------- Android: release configuration
        [MenuItem("Flightline/Android/Prepare Release Settings (icons, splash, player)")]
        public static void ApplyAndroidSettings()
        {
            var t = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.companyName = "Pradhyuman";
            PlayerSettings.productName = "Flightline";
            PlayerSettings.SetApplicationIdentifier(t, "com.pradhyuman.flightline");
            PlayerSettings.bundleVersion = Version;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;

            // orientation + display
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.androidIsGame = true;

            // code + targets: 64-bit IL2CPP (Play requirement), modern API levels
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            EditorUserBuildSettings.development = false;
            { // no hardware stats; the app is fully offline (no public API for this in Unity 6)
                var pso = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
                var sa = pso.FindProperty("submitAnalytics"); if (sa != null) { sa.boolValue = false; pso.ApplyModifiedPropertiesWithoutUndo(); }
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
            Branding.ApplyToPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Flightline: release settings applied. v{PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), com.pradhyuman.flightline, IL2CPP ARM64, API 26+ / target latest.");
        }

        // ---------------------------------------------------------------- Android: builds
        [MenuItem("Flightline/Android/Build Test APK (install on your phone)")]
        public static void BuildApk() => Build(false, false);

        [MenuItem("Flightline/Android/Build and Run on Connected Phone")]
        public static void BuildAndRun() => Build(false, true);

        [MenuItem("Flightline/Android/Build Release AAB (Google Play)")]
        public static void BuildAab() => Build(true, false);

        static bool Supported()
        {
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) return true;
            EditorUtility.DisplayDialog("Android Build Support missing",
                "Install it in Unity Hub: Installs > 6000.6.4f1 > Add modules > Android Build Support (with OpenJDK and Android SDK & NDK Tools). Then restart Unity.", "OK");
            return false;
        }

        static void Build(bool release, bool run)
        {
            if (!Supported()) return;
            ApplyAndroidSettings();
            Directory.CreateDirectory(OutDir);

            bool hadCustom = PlayerSettings.Android.useCustomKeystore;
            if (release)
            {
                // Release must be signed with your own upload key. Passwords are entered by you in Unity, never stored here.
                bool ready = hadCustom && File.Exists(PlayerSettings.Android.keystoreName)
                             && !string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) && !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass);
                if (!ready)
                {
                    EditorUtility.DisplayDialog("Release keystore needed",
                        "Google Play needs the app signed with your own upload key.\n\n" +
                        "1. Edit > Project Settings > Player > Android > Publishing Settings.\n" +
                        "2. Keystore Manager > Create New > Anywhere. Save it outside the project and back it up.\n" +
                        "3. Tick Custom Keystore, pick it, and enter both passwords.\n\n" +
                        "Unity forgets the passwords when it restarts, so enter them again before each release build.", "OK");
                    return;
                }
                PlayerSettings.Android.bundleVersionCode++;
            }
            else PlayerSettings.Android.useCustomKeystore = false; // test builds use the debug key

            string file = release ? $"{OutDir}/Flightline-{Version}-{PlayerSettings.Android.bundleVersionCode}.aab" : $"{OutDir}/Flightline.apk";
            EditorUserBuildSettings.buildAppBundle = release;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = file,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };
            bool ok = false;
            try
            {
                var report = BuildPipeline.BuildPlayer(opts);
                var sum = report.summary;
                ok = sum.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
                string msg = $"{sum.result} | {sum.totalErrors} errors | {sum.totalSize / (1024f * 1024f):0.0} MB | {sum.totalTime:mm\\:ss} | {Path.GetFullPath(file)}";
                File.WriteAllText($"{OutDir}/last_build.txt", msg);
                Debug.Log("Flightline Android build: " + msg);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = hadCustom;
                EditorUserBuildSettings.buildAppBundle = false;
                // A failed release build never shipped, so give its version code back; a good one is saved to disk right away.
                if (release && !ok) PlayerSettings.Android.bundleVersionCode--;
                if (release && ok) AssetDatabase.SaveAssets();
            }
        }

        // ---------------------------------------------------------------- tools
        // Renders the game camera plus UI to a PNG (used for review screenshots and store screenshots).
        public static void Capture(string path, int w = 540, int h = 1170)
        {
            var cam = Camera.main; var ui = GameObject.Find("UI"); if (cam == null || ui == null) return;
            var canvas = ui.GetComponent<Canvas>(); if (canvas == null) return;
            var rt = new RenderTexture(w, h, 24);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prevMode = canvas.renderMode; var prevCam = canvas.worldCamera; var prevDist = canvas.planeDistance;
            var prevTarget = cam.targetTexture; var prevActive = RenderTexture.active;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 5;
                cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                // always hand the camera and canvas back exactly as they were, even if the write fails
                RenderTexture.active = prevActive; cam.targetTexture = prevTarget;
                canvas.renderMode = prevMode; canvas.worldCamera = prevCam; canvas.planeDistance = prevDist;
                UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [MenuItem("Flightline/Reset Save Data")]
        static void ResetSave()
        {
            // Save.Load restores from the .bak copy when the main key is missing, so all three keys must go.
            PlayerPrefs.DeleteKey("flightline.save.v1"); PlayerPrefs.DeleteKey("flightline.save.v1.bak"); PlayerPrefs.DeleteKey("flightline.save.v1.corrupt");
            PlayerPrefs.Save(); Debug.Log("Flightline save data cleared.");
        }

        [MenuItem("Flightline/Give 5000 Coins (testing)")]
        static void Coins() { Save.Load(); Save.D.coins += 5000; Save.Write(); Debug.Log("Added 5000 coins."); }

        [MenuItem("Flightline/Set Phone Game View (1080x2340)")]
        static void PhoneView()
        {
            var asm = typeof(EditorWindow).Assembly;
            var gvsType = asm.GetType("UnityEditor.GameViewSizes");
            var single = typeof(ScriptableSingleton<>).MakeGenericType(gvsType);
            var inst = single.GetProperty("instance").GetValue(null);
            var groupType = gvsType.GetProperty("currentGroupType").GetValue(inst);
            var group = gvsType.GetMethod("GetGroup").Invoke(inst, new object[] { (int)groupType });
            var gt = group.GetType();
            var texts = (string[])gt.GetMethod("GetDisplayTexts").Invoke(group, null);
            int idx = Array.FindIndex(texts, s => s.Contains("Flightline"));
            if (idx < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var enumType = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { enumType, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.Parse(enumType, "FixedResolution"), 1080, 2340, "Flightline Phone" });
                gt.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                idx = (int)gt.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            var gvType = asm.GetType("UnityEditor.GameView");
            var gv = EditorWindow.GetWindow(gvType);
            var m = gvType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            m?.Invoke(gv, new object[] { idx, null });
            Debug.Log("Game view set to Flightline Phone (index " + idx + ")");
        }
    }
}
