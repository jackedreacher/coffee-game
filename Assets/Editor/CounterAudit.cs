#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Which counters the player can actually serve, and what to do about the rest.
//
// A counter missing from TapToServe's list still spawns customers. Nobody can
// be walked to them, so every one of them times out and takes a life -- and
// none of it is on screen, because the counter is somewhere the camera never
// goes. From the player's seat, hearts disappear on their own
public static class CounterAudit
{
    [MenuItem("Cooked Fast/Musteri: Tezgahlari Denetle", priority = 217)]
    public static void Audit()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Tezgahlar",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        FoodServingCustomerManager[] counters =
            Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (counters.Length <= 0)
        {
            Show("Sahnede hic tezgah yok.");
            return;
        }

        TapToServe[] players = Object.FindObjectsByType<TapToServe>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        List<FoodServingCustomerManager> orphans = new List<FoodServingCustomerManager>();

        string report = "";

        foreach (FoodServingCustomerManager counter in counters)
        {
            if (counter == null)
                continue;

            if (counter.Closed)
            {
                report += "- " + counter.name + ": KAPALI, musteri uretmiyor\n";
                continue;
            }

            if (Served(counter, players))
            {
                report += "- " + counter.name + ": servis edilebilir\n";
                continue;
            }

            report += "- " + counter.name + ": SERVIS EDILEMEZ -- her musterisi can goturur\n";
            orphans.Add(counter);
        }

        if (orphans.Count <= 0)
        {
            Show(report + "\nHer acik tezgah servis edilebiliyor. Kendiliginden giden can yok.");
            return;
        }

        bool close = EditorUtility.DisplayDialog("Tezgahlar",
            report + "\n" + orphans.Count + " tezgahi hicbir oyuncu servis edemiyor.\n" +
            "Bunlari kapatayim mi? (Musteri uretmeyi birakirlar)\n\n" +
            "Servis EDILMELERI gerekiyorsa onun yerine Player > Tap To Serve >\n" +
            "Customer Managers listesine ekle.",
            "Kapat", "Dokunma");

        if (!close)
        {
            Debug.Log("Tezgahlar\n" + report);
            return;
        }

        foreach (FoodServingCustomerManager counter in orphans)
        {
            SerializedObject so = new SerializedObject(counter);
            so.FindProperty("closed").boolValue = true;
            so.ApplyModifiedProperties();

            report += "- " + counter.name + " kapatildi\n";
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Show(report);
    }

    // Written rather than left to the field initialisers.
    //
    // These fields already exist in the saved scene at whatever they were when
    // they were first added. A new default in C# never reaches a component that
    // has been serialised once -- the scene file wins, and it is holding 10
    private const float minimumPatience = 20f;
    private const int keepBusy = 1;
    private const float refillDelay = 1.5f;

    [MenuItem("Cooked Fast/Musteri: Sabir ve Tempo Ayarlari", priority = 218)]
    public static void Settings()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Sabir ve Tempo",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        FoodServingCustomerManager[] counters =
            Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (counters.Length <= 0)
        {
            Show("Sahnede hic tezgah yok.");
            return;
        }

        string report = "";

        foreach (FoodServingCustomerManager counter in counters)
        {
            if (counter == null)
                continue;

            SerializedObject so = new SerializedObject(counter);

            so.FindProperty("minimumPatience").floatValue = minimumPatience;
            so.FindProperty("keepBusy").intValue = keepBusy;
            so.FindProperty("refillDelay").floatValue = refillDelay;

            so.ApplyModifiedProperties();

            report += "- " + counter.name + " ayarlandi\n";
        }

        report += "\nEn az sabir: " + minimumPatience + " sn\n" +
                  "Kuyrukta tutulmaya calisilan: " + keepBusy + " musteri\n" +
                  "Kuyruk incelince yeni musteri: " + refillDelay + " sn icinde";

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Show(report);
    }

    private static bool Served(FoodServingCustomerManager counter, TapToServe[] players)
    {
        foreach (TapToServe player in players)
        {
            if (player != null && player.Serves(counter))
                return true;
        }

        return false;
    }

    private static void Show(string report)
    {
        Debug.Log("Tezgahlar\n" + report);
        EditorUtility.DisplayDialog("Tezgahlar", report, "Tamam");
    }
}
#endif
