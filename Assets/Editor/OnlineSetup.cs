#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

// Installs the three packages online co-op needs, and then tells you whether
// everything else it needs is true as well.
//
// Installed from here rather than by editing Packages/manifest.json by hand,
// and deliberately WITHOUT version numbers: asked this way the package manager
// picks the newest version that is compatible with this editor. A pinned
// version is a number that is right today and wrong after the next editor
// upgrade, and a wrong one fails to resolve -- which looks exactly like the
// registry being down.
public static class OnlineSetup
{
    private static readonly string[] packages =
    {
        // Sessions, Relay, Lobby and Authentication behind one API. The old
        // standalone Relay package is not the one to install on Unity 6
        "com.unity.services.multiplayer",

        // The actual networking: NetworkObject, NetworkBehaviour, RPCs
        "com.unity.netcode.gameobjects",

        // Two players out of one editor. Without it, testing co-op means
        // making a build every single time
        "com.unity.multiplayer.playmode",
    };

    private static AddAndRemoveRequest running;

    [MenuItem("Cooked Fast/Online/1 - Paketleri Kur", priority = 240)]
    public static void Install()
    {
        if (running != null)
        {
            EditorUtility.DisplayDialog("Online", "Kurulum zaten suruyor.", "Tamam");

            return;
        }

        if (!EditorUtility.DisplayDialog("Online paketleri",
            "Su paketler kurulacak:\n\n" +
            "  com.unity.services.multiplayer\n" +
            "  com.unity.netcode.gameobjects\n" +
            "  com.unity.multiplayer.playmode\n\n" +
            "Internet gerekiyor ve birkac dakika surebilir. Unity indirme\n" +
            "bitince kendini yeniden derler.\n\n" +
            "Surum numarasi verilmiyor: bu Unity surumune uyan en yenisi\n" +
            "kurulur.", "Kur", "Vazgec"))
            return;

        running = Client.AddAndRemove(packages, null);

        EditorApplication.update += Poll;

        Debug.Log("Online: paketler isteniyor...");
    }

    private static void Poll()
    {
        if (running == null || !running.IsCompleted)
            return;

        EditorApplication.update -= Poll;

        if (running.Status == StatusCode.Failure)
        {
            string message = running.Error != null ? running.Error.message : "bilinmiyor";

            running = null;

            Debug.LogError("Online: kurulum basarisiz -- " + message);

            EditorUtility.DisplayDialog("Online",
                "Paketler kurulamadi.\n\n" + message + "\n\n" +
                "Internet baglantisini ve Package Manager'i kontrol et.", "Tamam");

            return;
        }

        running = null;

        Debug.Log("Online: paketler kuruldu.");

        EditorUtility.DisplayDialog("Online",
            "Paketler kuruldu.\n\n" +
            "Unity simdi yeniden derleyecek. Derleme bitince\n" +
            "COOP_ONLINE tanimi otomatik acilir ve online kodu\n" +
            "derlenmeye baslar.\n\n" +
            "Sonra: Cooked Fast > Online > 2 - Kurulumu Kontrol Et", "Tamam");
    }

    [MenuItem("Cooked Fast/Online/2 - Kurulumu Kontrol Et", priority = 241)]
    public static void Check()
    {
        StringBuilder report = new StringBuilder();

        // ---- packages -------------------------------------------------------
        report.AppendLine("PAKETLER");

        UnityEditor.PackageManager.PackageInfo[] all = null;

        try
        {
            all = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        }
        catch
        {
            // Not fatal. The type check below is the one that actually decides
        }

        for (int i = 0; i < packages.Length; i++)
        {
            string version = Version(all, packages[i]);

            report.AppendLine(version == null
                ? "  YOK  " + packages[i]
                : "  var  " + packages[i] + "  " + version);
        }

        // ---- code -----------------------------------------------------------
        report.AppendLine();
        report.AppendLine("KOD");

        bool ready = OnlineDefines.Ready();

        report.AppendLine(ready
            ? "  var  COOP_ONLINE acik, online kodu derleniyor"
            : "  YOK  COOP_ONLINE kapali, online kodu derlenmiyor");

        if (!ready)
        {
            report.AppendLine("  Bulunamayan tipler:");
            report.Append(OnlineDefines.Missing());
        }

        // ---- cloud ----------------------------------------------------------
        report.AppendLine();
        report.AppendLine("UNITY CLOUD");

        string project = CloudProjectSettings.projectId;

        report.AppendLine(string.IsNullOrEmpty(project)
            ? "  YOK  Proje bir Unity Cloud projesine bagli degil"
            : "  var  Project ID " + project);

        report.AppendLine("  Dashboard'da acik olmasi gerekenler:");
        report.AppendLine("    Authentication  (anonim giris)");
        report.AppendLine("    Multiplayer / Relay");

        // ---- the test scene -------------------------------------------------
        report.AppendLine();
        report.AppendLine("TEST");

        bool prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoopTestSetup.prefabPath) != null;
        bool scene = System.IO.File.Exists(CoopTestSetup.scenePath);

        report.AppendLine(prefab
            ? "  var  " + CoopTestSetup.prefabPath
            : "  YOK  Kapsul prefabi (3 - Test Sahnesini Kur)");

        report.AppendLine(scene
            ? "  var  " + CoopTestSetup.scenePath
            : "  YOK  Test sahnesi (3 - Test Sahnesini Kur)");

        // ---- what to do next ------------------------------------------------
        report.AppendLine();

        if (!ready)
            report.AppendLine("SIRADAKI: 1 - Paketleri Kur");
        else if (!prefab || !scene)
            report.AppendLine("SIRADAKI: 3 - Test Sahnesini Kur");
        else
            report.AppendLine(
                "SIRADAKI: Test sahnesini ac, Multiplayer Play Mode'dan\n" +
                "ikinci oyuncuyu ac, birinde ODA KUR, digerinde kodla KATIL.");

        string text = report.ToString();

        Debug.Log("Online kurulum\n" + text);
        EditorUtility.DisplayDialog("Online kurulum", text, "Tamam");
    }

    private static string Version(UnityEditor.PackageManager.PackageInfo[] all, string id)
    {
        if (all == null)
            return null;

        for (int i = 0; i < all.Length; i++)
            if (all[i].name == id)
                return all[i].version;

        return null;
    }
}
#endif
