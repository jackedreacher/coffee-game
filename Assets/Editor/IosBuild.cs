#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

// The iOS half of the APK menu.
//
// It looks like the same job and it is not. An Android build ends in a file you
// copy to a phone; an iOS build ends in an XCODE PROJECT -- a folder of C++ that
// Unity transpiled from the C#, plus the engine libraries and the packed assets.
// Nothing in it is signed and nothing in it can run yet. The compiler that turns
// it into an app only exists on macOS, so the last step happens on the Mac and
// there is no way around that: Apple does not ship a code signer for Windows.
//
// What that buys is that the Mac needs Xcode and NOTHING ELSE. No Unity licence
// over there, no project copy, no version match. The folder this produces is
// self contained. So the flow is: build here, copy the folder, open it there,
// press play.
//
// The settings this checks are the ones that produce a folder which opens on the
// Mac and then fails -- a simulator SDK, a bundle id Apple will not sign, the
// two render settings that were already wrong on Android. Every one of them
// costs a full rebuild to discover, and a rebuild here is ten to thirty minutes
public static class IosBuild
{
    private const string scenePath = "Assets/Tiny Coffee Shop/Game Scenes/Kitchen.unity";

    private const string outputFolder = "Builds/iOS";

    // Written into the generated folder so the Mac end of this does not depend
    // on anybody remembering a chat window
    private const string notesName = "MAC-ADIMLAR.txt";

    // Reverse domain, and it has to be unique across the App Store even for a
    // build that never goes near it -- Xcode refuses to sign a duplicate. The
    // project is still carrying Unity's template id, which is not a name anybody
    // chose and is shared with every other urp-blank project on earth
    private const string identifier = "com.jackedreacher.cookedfast";

    private const string mobileAssetPath = "Assets/Settings/Mobile_RPAsset.asset";

    private const string edgeShaderName = "Hidden/Edge Detection";

    // ---- 1: what would stop it ----------------------------------------------

    [MenuItem("Cooked Fast/iOS/1 - Ayarlari Kontrol Et", priority = 320)]
    public static void Check()
    {
        StringBuilder report = new StringBuilder();

        report.Append(Inspect(out bool canBuild, out int warnings));

        report.Insert(0, canBuild
            ? warnings > 0
                ? "SONUC: derlenir, ama " + warnings + " uyari var\n\n"
                : "SONUC: derlemeye hazir\n\n"
            : "SONUC: once yukaridaki EKSIK satirlarini duzelt\n\n");

        report.AppendLine();
        report.AppendLine("Duzeltilebilenler icin:");
        report.AppendLine("  Cooked Fast > iOS > 2 - Ayarlari Duzelt");

        Debug.Log("[iOS]\n" + report);
        EditorUtility.DisplayDialog("iOS Kontrol", report.ToString(), "Tamam");
    }

    private static string Inspect(out bool canBuild, out int warnings)
    {
        StringBuilder report = new StringBuilder();

        canBuild = true;
        warnings = 0;

        // -- the scene ---------------------------------------------------------

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

        // -- the module --------------------------------------------------------

        report.AppendLine();
        report.AppendLine("iOS modulu");

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            report.AppendLine("  EKSIK: iOS Build Support kurulu degil.");
            report.AppendLine("  Unity Hub > Installs > 6000.3.16f1 > disli > Add modules");
            report.AppendLine("  iOS Build Support");
            canBuild = false;
        }
        else
        {
            report.AppendLine("  kurulu (Windows'ta Xcode projesi uretmeye yeter)");
        }

        // -- the bundle id -----------------------------------------------------
        //
        // The one failure that happens on the Mac instead of here. Xcode signs
        // per bundle id, so a template id is not a cosmetic problem: it is
        // already taken, and the error it produces over there is about
        // provisioning profiles and says nothing about Unity

        report.AppendLine();
        report.AppendLine("Bundle Identifier");

        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

        if (Template(id))
        {
            report.AppendLine("  VARSAYILAN: " + id);
            report.AppendLine("  Bu Unity'nin sablon kimligi -- Xcode imzalamaz.");
            report.AppendLine("  Onerilen: " + identifier);
            canBuild = false;
        }
        else
        {
            report.AppendLine("  " + id);
        }

        // -- device or simulator -----------------------------------------------
        //
        // Silent, and the whole build is wasted. A simulator SDK project compiles
        // on the Mac, runs on a simulated iPhone on the desktop, and refuses to
        // install on real hardware -- with the device greyed out in Xcode's
        // dropdown rather than an error

        report.AppendLine();
        report.AppendLine("Hedef");

        if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.DeviceSDK)
        {
            report.AppendLine("  EKSIK: SDK = Simulator.");
            report.AppendLine("  Gercek telefona kurulamaz, sadece Mac'teki simulatorde acilir.");
            canBuild = false;
        }
        else
        {
            report.AppendLine("  cihaz (Device SDK)");
        }

        report.AppendLine("  aygit        : " + PlayerSettings.iOS.targetDevice);
        report.AppendLine("  en dusuk iOS : " + PlayerSettings.iOS.targetOSVersionString);
        report.AppendLine("  backend      : " + PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS) +
                          "  (iOS'ta hep IL2CPP, secenek yok)");

        // -- signing -----------------------------------------------------------
        //
        // A warning and never an error. The team id belongs to the Mac -- it is
        // tied to the Apple ID logged in over there -- so the honest thing this
        // side can do is write the automatic flag and say where the rest goes

        report.AppendLine();
        report.AppendLine("Imza");

        if (!PlayerSettings.iOS.appleEnableAutomaticSigning)
        {
            report.AppendLine("  UYARI: otomatik imzalama kapali.");
            report.AppendLine("  Acik olsa Xcode kendi profilini uretirdi.");
            warnings++;
        }
        else
        {
            report.AppendLine("  otomatik");
        }

        report.AppendLine("  Team ID Mac'te secilir (Xcode > Signing & Capabilities),");
        report.AppendLine("  burada bos olmasi normal.");

        // -- orientation -------------------------------------------------------
        //
        // Not an iOS setting -- it is the shared one, so the phone build has the
        // same problem and nobody has noticed because Android was tested face on.
        // The kitchen is drawn for a tall screen; a landscape rotation crops the
        // counter off both ends

        report.AppendLine();
        report.AppendLine("Ekran yonu");

        bool landscape = PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation &&
                         (PlayerSettings.allowedAutorotateToLandscapeLeft ||
                          PlayerSettings.allowedAutorotateToLandscapeRight);

        if (landscape)
        {
            report.AppendLine("  UYARI: " + PlayerSettings.defaultInterfaceOrientation + ", yatay serbest.");
            report.AppendLine("  Oyun dikey tasarlandi -- telefon yan cevrilince mutfak kirpilir.");
            warnings++;
        }
        else
        {
            report.AppendLine("  " + PlayerSettings.defaultInterfaceOrientation);
        }

        // -- the two settings that were already wrong on Android ---------------
        //
        // iPhone quality level is 0, which is Mobile, which is the same render
        // pipeline asset Android draws with. So both phone fixes are one fix and
        // an iOS build made before they were applied looks exactly as washed out

        report.AppendLine();
        report.AppendLine("Telefon gorunumu (Android ile ayni ayarlar)");

        report.Append(LookLines(ref warnings));

        return report.ToString();
    }

    // Reads the two things PhoneLookFix writes, without writing anything. They
    // live in different files -- one asset, one project setting -- and both are
    // invisible from the editor because the editor draws with the PC level
    private static string LookLines(ref int warnings)
    {
        StringBuilder report = new StringBuilder();

        RenderPipelineAsset mobile =
            AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(mobileAssetPath);

        if (mobile == null)
        {
            report.AppendLine("  " + mobileAssetPath + " bulunamadi, atlandi.");
            return report.ToString();
        }

        SerializedProperty fast =
            new SerializedObject(mobile).FindProperty("m_UseFastSRGBLinearConversion");

        if (fast != null && fast.boolValue)
        {
            report.AppendLine("  UYARI: Fast sRGB/Linear Conversions acik -> renkler solgun cikar.");
            warnings++;
        }
        else
        {
            report.AppendLine("  Fast sRGB kapali");
        }

        report.Append(ShaderLine(ref warnings));

        report.AppendLine("  Ikisini de duzelten komut:");
        report.AppendLine("    Cooked Fast > APK > 3 - Telefon Gorunumunu Duzelt");

        return report.ToString();
    }

    private static string ShaderLine(ref int warnings)
    {
        UnityEngine.Object[] assets =
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");

        if (assets == null || assets.Length <= 0 || assets[0] == null)
            return "  GraphicsSettings okunamadi, atlandi.\n";

        SerializedProperty list =
            new SerializedObject(assets[0]).FindProperty("m_AlwaysIncludedShaders");

        if (list == null)
            return "  Always Included Shaders listesi bulunamadi, atlandi.\n";

        Shader edge = Shader.Find(edgeShaderName);

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue != edge)
                continue;

            return "  " + edgeShaderName + " derlemeye dahil\n";
        }

        warnings++;

        return "  UYARI: " + edgeShaderName + " Always Included listesinde yok.\n" +
               "  Derlemeden atilir, konturlar kaybolur, Xcode konsolu\n" +
               "  shader is null satirlariyla dolar.\n";
    }

    // Unity's template identifier, in the three shapes it appears in. Matched on
    // the pieces rather than the whole string because the Android one lost its
    // hyphens somewhere and is just as unusable
    private static bool Template(string id)
    {
        if (string.IsNullOrEmpty(id))
            return true;

        return id.Contains("unity.template") ||
               id.Contains("Unity-Technologies") ||
               id.Contains("UnityTechnologies") ||
               id.Contains("DefaultCompany");
    }

    // ---- 2: fix what can be fixed from here ---------------------------------

    [MenuItem("Cooked Fast/iOS/2 - Ayarlari Duzelt", priority = 321)]
    public static void Prepare()
    {
        StringBuilder report = new StringBuilder();

        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

        report.AppendLine("Bundle Identifier");

        if (Template(id))
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, identifier);

            report.AppendLine("  " + id);
            report.AppendLine("  -> " + identifier);
            report.AppendLine("  Baskasini istersen IosBuild.cs'in basindaki");
            report.AppendLine("  identifier satirini degistir, ya da");
            report.AppendLine("  Player Settings > iOS > Identification.");
        }
        else
        {
            report.AppendLine("  " + id + " (elle konmus, dokunulmadi)");
        }

        report.AppendLine();
        report.AppendLine("Hedef");

        if (PlayerSettings.iOS.sdkVersion != iOSSdkVersion.DeviceSDK)
        {
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            report.AppendLine("  Simulator -> Device");
        }
        else
        {
            report.AppendLine("  zaten cihaz");
        }

        report.AppendLine();
        report.AppendLine("Imza");

        if (!PlayerSettings.iOS.appleEnableAutomaticSigning)
        {
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            report.AppendLine("  otomatik imzalama acildi");
        }
        else
        {
            report.AppendLine("  otomatik imzalama zaten acik");
        }

        report.AppendLine("  Team ID Mac'te secilecek, burada bos birakiliyor.");

        // Asked rather than written, because this one is not an iOS setting.
        //
        // There is a single orientation shared by every platform, so locking it
        // changes the apk too. That is very probably the right thing -- the game
        // is portrait and the apk has been free to rotate all along -- but it is
        // a change to something that was not the subject, and those get asked
        report.AppendLine();
        report.AppendLine("Ekran yonu");

        bool landscape = PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation &&
                         (PlayerSettings.allowedAutorotateToLandscapeLeft ||
                          PlayerSettings.allowedAutorotateToLandscapeRight);

        if (!landscape)
        {
            report.AppendLine("  " + PlayerSettings.defaultInterfaceOrientation + ", dokunulmadi");
        }
        else if (EditorUtility.DisplayDialog("Ekran Yonu",
                     "Oyun dikey tasarlandi ama su an yatay donmeye acik.\n\n" +
                     "Dikeye kilitleyeyim mi?\n\n" +
                     "Bu ayar platformlar arasi ortak -- APK'yi da etkiler.",
                     "Dikeye kilitle", "Dokunma"))
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            report.AppendLine("  AutoRotation -> Portrait (APK de dahil)");
        }
        else
        {
            report.AppendLine("  yatay serbest birakildi");
        }

        AssetDatabase.SaveAssets();

        report.AppendLine();
        report.AppendLine("Sonra:");
        report.AppendLine("  Cooked Fast > iOS > 3 - Xcode Projesi Olustur");

        Debug.Log("[iOS]\n" + report);
        EditorUtility.DisplayDialog("iOS Ayarlari", report.ToString(), "Tamam");
    }

    // ---- 3: the folder that goes to the Mac ---------------------------------

    [MenuItem("Cooked Fast/iOS/3 - Xcode Projesi Olustur", priority = 322)]
    public static void Build()
    {
        string problems = Inspect(out bool canBuild, out int _);

        if (!canBuild)
        {
            EditorUtility.DisplayDialog("iOS Derlenemez",
                problems + "\nOnce bunlari duzelt.", "Tamam");
            return;
        }

        // Same reasoning as the apk: switching platform reimports every texture
        // in the project against a different compression format and takes longer
        // than the build. Asked, not done behind their back
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
        {
            if (!EditorUtility.DisplayDialog("Platform iOS degil",
                    "Su anki platform: " + EditorUserBuildSettings.activeBuildTarget +
                    "\n\niOS'a gecmek butun asset'leri yeniden import eder ve\n" +
                    "derlemenin kendisinden uzun surer. Bir kere yapilir.\n\n" +
                    "Android'e geri donmek de ayni sureyi alir.\n\n" +
                    "Simdi gecilsin mi?",
                    "Gec", "Vazgec"))
                return;

            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.iOS, BuildTarget.iOS);

            EditorUtility.DisplayDialog("Platform degisti",
                "Import bitince 'iOS: 3 - Xcode Projesi Olustur' komutunu tekrar calistir.",
                "Tamam");
            return;
        }

        // A FOLDER, not a file. Unity writes Unity-iPhone.xcodeproj, Classes,
        // Libraries and Data into it and the four together are the deliverable --
        // which is also why it is wiped first: a stale Libraries next to a fresh
        // Data is a link error on the Mac with nothing to point at the cause
        string path = outputFolder;

        if (Directory.Exists(path))
        {
            if (!EditorUtility.DisplayDialog("Eski proje var",
                    Path.GetFullPath(path) +
                    "\n\nSilinip bastan yazilacak.\n\n" +
                    "Xcode'da elle bir sey degistirdiysen gider.",
                    "Sil ve derle", "Vazgec"))
                return;

            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = path,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,

            // Development so the Debug.Log lines this project leans on reach the
            // Xcode console. Every station reports why it refused and none of
            // that survives a release build
            options = BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        BuildReport result = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = result.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[iOS] derleme basarisiz: " + summary.result +
                           "  (" + summary.totalErrors + " hata)");

            EditorUtility.DisplayDialog("iOS",
                "Derleme basarisiz: " + summary.result + "\n\n" +
                "Console'daki ilk kirmizi satir sebebi soyler.", "Tamam");
            return;
        }

        string full = Path.GetFullPath(path);

        WriteNotes(path);

        Debug.Log("[iOS] hazir: " + full +
                  "\n  sure: " + summary.totalTime.ToString(@"mm\:ss") +
                  "\n  " + notesName + " icinde Mac adimlari var");

        EditorUtility.DisplayDialog("Xcode Projesi Hazir",
            full +
            "\n\nsure: " + summary.totalTime.ToString(@"mm\:ss") +
            "\n\nBu KLASORUN TAMAMINI Mac'e kopyala.\n" +
            "Mac'te Unity gerekmiyor, sadece Xcode.\n\n" +
            "Adimlar klasordeki " + notesName + " dosyasinda.",
            "Tamam");

        EditorUtility.RevealInFinder(full);
    }

    // The instructions travel with the folder rather than staying here.
    //
    // Everything else in this file runs on the machine that has the project on
    // it; this is the only part that has to work on a different computer, days
    // later, with none of this open
    private static void WriteNotes(string folder)
    {
        StringBuilder notes = new StringBuilder();

        notes.AppendLine("COOKED FAST -- iOS");
        notes.AppendLine("Uretildi: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        notes.AppendLine("Unity   : " + Application.unityVersion);
        notes.AppendLine("Surum   : " + PlayerSettings.bundleVersion);
        notes.AppendLine("Bundle  : " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS));
        notes.AppendLine("En dusuk iOS: " + PlayerSettings.iOS.targetOSVersionString);
        notes.AppendLine();
        notes.AppendLine("Bu klasor kendi kendine yeter. Mac'te Unity KURULU OLMASINA");
        notes.AppendLine("GEREK YOK, sadece Xcode yeter.");
        notes.AppendLine();
        notes.AppendLine("----------------------------------------------------------");
        notes.AppendLine("MAC'TE");
        notes.AppendLine("----------------------------------------------------------");
        notes.AppendLine();
        notes.AppendLine("1) Klasorun TAMAMINI kopyala. Icindeki");
        notes.AppendLine("   Unity-iPhone.xcodeproj / Classes / Libraries / Data /");
        notes.AppendLine("   Il2CppOutputProject / UnityFramework");
        notes.AppendLine("   hepsi gerekli, biri eksikse derlenmez.");
        notes.AppendLine();

        // The one step that is here only because the build machine was Windows.
        //
        // Xcode runs process_symbols.sh as a build phase and that script runs
        // usymtool next to it. Both are executable in the folder Unity wrote,
        // and NTFS to zip to macOS drops the executable bit on the way -- so the
        // build phase dies with "Permission denied" and an error that names a
        // shell script nobody has heard of. One chmod, before opening Xcode
        notes.AppendLine("2) ONEMLI -- once izinleri geri ver.");
        notes.AppendLine("   Windows'ta zip'lenince dosyalarin calistirilabilir");
        notes.AppendLine("   bayragi kayboluyor, Xcode de derleme sirasinda bu");
        notes.AppendLine("   script'leri calistiriyor. Terminal'de klasorun icinde:");
        notes.AppendLine();
        notes.AppendLine("     chmod +x *.sh usymtool usymtoolarm64");
        notes.AppendLine("     chmod -R +x Il2CppOutputProject/IL2CPP/build/");
        notes.AppendLine();
        notes.AppendLine("   Atlarsan hata: Permission denied / process_symbols.sh");
        notes.AppendLine();
        notes.AppendLine("3) Unity-iPhone.xcodeproj dosyasini cift tikla.");
        notes.AppendLine();
        notes.AppendLine("4) Sol panelde en ustteki Unity-iPhone'a tikla,");
        notes.AppendLine("   TARGETS > Unity-iPhone > Signing & Capabilities:");
        notes.AppendLine();
        notes.AppendLine("     Automatically manage signing : ACIK");
        notes.AppendLine("     Team                         : kendi Apple ID'n");
        notes.AppendLine("     Bundle Identifier            : benzersiz olmali");
        notes.AppendLine();
        notes.AppendLine("   AYNISINI UnityFramework TARGET'INDA DA YAP.");
        notes.AppendLine("   Iki target var, ikisi de imzalanmadan kurulum olmaz.");
        notes.AppendLine();
        notes.AppendLine("   Apple ID'n listede yoksa:");
        notes.AppendLine("   Xcode > Settings > Accounts > + > Apple ID");
        notes.AppendLine("   App Store'a cikmayacaksan ucretsiz hesap yeter.");
        notes.AppendLine();
        notes.AppendLine("5) iPhone'u kabloyla bagla, telefonda cikan");
        notes.AppendLine("   guven sorusuna Guven de. Telefonda");
        notes.AppendLine("   Ayarlar > Gizlilik ve Guvenlik > Gelistirici Modu");
        notes.AppendLine("   ACIK olmali (iOS 16+), telefon yeniden baslar.");
        notes.AppendLine();
        notes.AppendLine("6) Xcode'un ust barindaki cihaz secicisinden telefonu sec,");
        notes.AppendLine("   Cmd+R.");
        notes.AppendLine();
        notes.AppendLine("   ILK DERLEME UZUN. IL2CPP bu klasordeki 1 GB'lik uretilmis");
        notes.AppendLine("   C++'i Mac'te derliyor -- 15-40 dakika normal, donmus");
        notes.AppendLine("   degil. Sonraki derlemeler cok daha kisa.");
        notes.AppendLine();
        notes.AppendLine("7) Ilk acilista telefon guvenilmeyen gelistirici derse:");
        notes.AppendLine("   Ayarlar > Genel > VPN ve Aygit Yonetimi > hesabin > Guven.");
        notes.AppendLine();
        notes.AppendLine("----------------------------------------------------------");
        notes.AppendLine("BILINMESI GEREKENLER");
        notes.AppendLine("----------------------------------------------------------");
        notes.AppendLine();
        notes.AppendLine("- Mac'te ~10 GB bos yer isteyin. Klasor 1.8 GB, derleme");
        notes.AppendLine("  artifact'leri onun ustune biniyor.");
        notes.AppendLine();
        notes.AppendLine("- Xcode 15 ya da uzeri gerekli (hedef iOS 15.0).");
        notes.AppendLine();
        notes.AppendLine("- IL2CPP asamasi usymtool ile ilgili bir Windows yolundan");
        notes.AppendLine("  sikayet ederse: Xcode'da Unity-iPhone target >");
        notes.AppendLine("  Build Phases > Run Script (IL2CPP) icindeki");
        notes.AppendLine("  --usymtool-path=... satirini sil. O satir sadece crash");
        notes.AppendLine("  sembolleri icin, oyunun calismasiyla ilgisi yok.");
        notes.AppendLine();
        notes.AppendLine("- Ucretsiz Apple ID ile kurulan uygulama 7 gun sonra acilmaz.");
        notes.AppendLine("  Tekrar Cmd+R yeter. Ucretli hesapta 1 yil.");
        notes.AppendLine();
        notes.AppendLine("- Bu klasorde ELLE degisiklik yapma. Unity tarafinda bir sey");
        notes.AppendLine("  degisince komut klasoru silip bastan yaziyor ve elle yapilan");
        notes.AppendLine("  her sey gidiyor. Kalici olmasi gereken ayar varsa Unity");
        notes.AppendLine("  tarafinda Player Settings'e yazilmali.");
        notes.AppendLine();
        notes.AppendLine("- Development build: Xcode konsolunda oyunun kendi");
        notes.AppendLine("  Debug.Log satirlari gorunur. Istasyonlar neden");
        notes.AppendLine("  reddettiklerini oraya yaziyor -- bir sey calismazsa");
        notes.AppendLine("  once oraya bak.");
        notes.AppendLine();
        notes.AppendLine("- Xcode konsolunda shader is null satirlari gorursen bu");
        notes.AppendLine("  derleme Telefon Gorunumu duzeltmesinden onceki ayarlarla");
        notes.AppendLine("  alinmis demektir. Windows'ta");
        notes.AppendLine("  Cooked Fast > APK > 3 - Telefon Gorunumunu Duzelt,");
        notes.AppendLine("  sonra bu projeyi yeniden uret.");

        File.WriteAllText(Path.Combine(folder, notesName), notes.ToString());
    }
}
#endif
