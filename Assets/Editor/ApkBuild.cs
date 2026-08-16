using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

// One button, one apk.
//
// The scene list is passed straight to BuildPlayer rather than read from Build
// Settings, and that is deliberate: Build Settings currently holds SampleScene
// and nothing else, so a normal build produces an apk that opens an empty room.
// Passing the scene explicitly means the build is right without editing project
// settings that someone may have arranged that way on purpose
public static class ApkBuild
{
    private const string scenePath = "Assets/Tiny Coffee Shop/Game Scenes/Kitchen.unity";
    private const string outputFolder = "Builds";

    [MenuItem("Cooked Fast/APK: 1 - Ayarlari Kontrol Et", priority = 300)]
    public static void Check()
    {
        StringBuilder report = new StringBuilder();

        report.Append(Inspect(out bool canBuild));

        report.AppendLine();
        report.AppendLine("Hizlandirmak icin");
        report.AppendLine("  File > Build Profiles > Android > Player Settings");
        report.AppendLine();
        report.AppendLine("  IL2CPP + ARM64 : dogru ama yavas (10-30 dk)");
        report.AppendLine("  Mono + ARMv7   : cok daha hizli (2-5 dk), sadece test icin");
        report.AppendLine("    Scripting Backend = Mono");
        report.AppendLine("    Target Architectures = ARMv7 (ARM64'u KAPAT)");
        report.AppendLine("    Mono ARM64'u desteklemez, ikisi birlikte derlenmez.");
        report.AppendLine("    Cok yeni telefonlar 32-bit calistirmaz -- acilmazsa IL2CPP'ye don.");
        report.AppendLine();
        report.AppendLine("  Ilk derleme her halukarda yavas, sonrakiler cache'ten hizlanir.");

        report.Insert(0, canBuild
            ? "SONUC: derlemeye hazir\n\n"
            : "SONUC: once yukaridaki EKSIK satirlarini duzelt\n\n");

        Debug.Log("[APK]\n" + report);
        EditorUtility.DisplayDialog("APK Kontrol", report.ToString(), "Tamam");
    }

    [MenuItem("Cooked Fast/APK: 2 - Derle", priority = 301)]
    public static void Build()
    {
        string problems = Inspect(out bool canBuild);

        if (!canBuild)
        {
            EditorUtility.DisplayDialog("APK Derlenemez",
                problems + "\nOnce bunlari duzelt.", "Tamam");
            return;
        }

        // Switching platform reimports every asset in the project and takes far
        // longer than the build itself, so it is asked rather than done
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if (!EditorUtility.DisplayDialog("Platform Android degil",
                    "Su anki platform: " + EditorUserBuildSettings.activeBuildTarget +
                    "\n\nAndroid'e gecmek butun asset'leri yeniden import eder ve\n" +
                    "derlemenin kendisinden uzun surer. Bir kere yapilir.\n\n" +
                    "Simdi gecilsin mi?",
                    "Gec", "Vazgec"))
                return;

            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);

            EditorUtility.DisplayDialog("Platform degisti",
                "Import bitince 'APK: 2 - Derle' komutunu tekrar calistir.", "Tamam");
            return;
        }

        Directory.CreateDirectory(outputFolder);

        string name = "CookedFast-" + PlayerSettings.bundleVersion + "-" +
                      DateTime.Now.ToString("MMdd-HHmm") + ".apk";

        string path = Path.Combine(outputFolder, name);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,

            // Development so logcat carries the Debug.Log lines this project
            // leans on. Every station reports why it refused, and none of that
            // reaches a release build
            options = BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        BuildReport result = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = result.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[APK] derleme basarisiz: " + summary.result +
                           "  (" + summary.totalErrors + " hata)");

            EditorUtility.DisplayDialog("APK",
                "Derleme basarisiz: " + summary.result + "\n\n" +
                "Console'daki ilk kirmizi satir sebebi soyler.", "Tamam");
            return;
        }

        string full = Path.GetFullPath(path);

        Debug.Log("[APK] hazir: " + full +
                  "\n  boyut: " + (summary.totalSize / 1048576f).ToString("0.0") + " MB" +
                  "\n  sure : " + summary.totalTime.ToString(@"mm\:ss"));

        EditorUtility.DisplayDialog("APK Hazir",
            full +
            "\n\nboyut: " + (summary.totalSize / 1048576f).ToString("0.0") + " MB" +
            "\nsure : " + summary.totalTime.ToString(@"mm\:ss") +
            "\n\nTelefona kopyalayip kur, ya da:\nadb install -r \"" + full + "\"",
            "Tamam");

        EditorUtility.RevealInFinder(full);
    }

    // ---- what would stop it ------------------------------------------------

    private static string Inspect(out bool canBuild)
    {
        StringBuilder report = new StringBuilder();

        canBuild = true;

        report.AppendLine("Sahne");

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath) == null)
        {
            report.AppendLine("  EKSIK: " + scenePath + " bulunamadi");
            canBuild = false;
        }
        else
        {
            report.AppendLine("  " + scenePath);
            report.AppendLine("  (Build Settings'e bakilmiyor, bu sahne dogrudan veriliyor)");
        }

        report.AppendLine();
        report.AppendLine("Android modulu");

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            report.AppendLine("  EKSIK: Android Build Support kurulu degil.");
            report.AppendLine("  Unity Hub > Installs > 6000.3.16f1 > disli > Add modules");
            report.AppendLine("  Android Build Support + OpenJDK + Android SDK & NDK");
            canBuild = false;
        }
        else
        {
            report.AppendLine("  kurulu");
        }

        report.AppendLine();
        report.AppendLine("Paket adi");

        string id = PlayerSettings.GetApplicationIdentifier(
            UnityEditor.Build.NamedBuildTarget.Android);

        if (string.IsNullOrEmpty(id) || id.Contains("DefaultCompany"))
        {
            report.AppendLine("  EKSIK ya da varsayilan: \"" + id + "\"");
            report.AppendLine("  Player Settings > Other Settings > Package Name");
            report.AppendLine("  ornek: com.jackedreacher.cookedfast");
            canBuild = false;
        }
        else
        {
            report.AppendLine("  " + id);
        }

        report.AppendLine();
        report.AppendLine("Derleme ayarlari");
        report.AppendLine("  backend      : " + PlayerSettings.GetScriptingBackend(
            UnityEditor.Build.NamedBuildTarget.Android));
        report.AppendLine("  mimari       : " + PlayerSettings.Android.targetArchitectures);
        report.AppendLine("  min SDK      : " + PlayerSettings.Android.minSdkVersion);
        report.AppendLine("  imza         : " + (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName)
            ? "debug (sideload icin yeterli, Play Store icin degil)"
            : PlayerSettings.Android.keystoreName));

        return report.ToString();
    }
}
