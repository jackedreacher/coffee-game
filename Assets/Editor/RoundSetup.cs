#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Writes the 50 round assets and wires a RoundManager to them.
//
// The table is here rather than typed into fifty inspectors because fifty
// hand-entered pairs is fifty chances to put 6.2 where 6.5 belongs, and a curve
// with one wrong step in it is a curve nobody can read off the game
public static class RoundSetup
{
    private const string folder = "Assets/Tiny Coffee Shop/Data/Rounds";

    // Musteri sayisi, gelis araligi. Raund 1'den 50'ye
    private static readonly (int customers, float interval)[] table =
    {
        (3, 15.0f), (4, 14.0f), (4, 13.0f), (5, 12.5f), (5, 12.0f),
        (6, 11.5f), (6, 11.0f), (7, 10.5f), (7, 10.0f), (8, 9.5f),
        (8, 9.2f), (9, 9.0f), (9, 8.8f), (10, 8.5f), (10, 8.2f),
        (11, 8.0f), (11, 7.8f), (12, 7.6f), (12, 7.4f), (13, 7.2f),
        (13, 7.0f), (14, 6.9f), (14, 6.8f), (15, 6.7f), (15, 6.6f),
        (16, 6.5f), (17, 6.2f), (17, 6.0f), (18, 5.8f), (19, 5.5f),
        (20, 5.3f), (20, 5.0f), (21, 4.8f), (22, 4.6f), (23, 4.4f),
        (24, 4.2f), (25, 4.0f), (26, 3.9f), (27, 3.8f), (28, 3.7f),
        (30, 3.6f), (32, 3.5f), (34, 3.4f), (35, 3.3f), (38, 3.2f),
        (40, 3.1f), (42, 3.0f), (45, 2.9f), (48, 2.8f), (50, 2.5f)
    };

    // How many DIFFERENT things one customer may ask for.
    //
    // Straight out of the design document: the early rounds teach the basic
    // moves, and round 11 opens "malzemelerin cesitlendigi asama". Before that
    // an order is one thing however many of it, which is the shape the player
    // learns on
    private static int Types(int round)
    {
        return round <= 10 ? 1 : 2;
    }

    // The four bands the table was written in. Kept so a round asset says what
    // it is for when somebody opens it on its own
    private static string Band(int round)
    {
        if (round <= 10) return "Erken: temel mekanikleri ogrenme";
        if (round <= 25) return "Orta: tempo artiyor";
        if (round <= 40) return "Ileri: kaos basliyor";
        return "Usta: saniyeler kritik";
    }

    [MenuItem("Cooked Fast/Oyun/Raund: 50 Raundu Uret", priority = 230)]
    public static void Generate()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Raundlar",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();

        RoundData[] rounds = new RoundData[table.Length];
        int made = 0;
        int updated = 0;

        for (int i = 0; i < table.Length; i++)
        {
            int round = i + 1;
            string path = folder + "/Round " + round.ToString("00") + ".asset";

            // Reused rather than replaced. Creating a new asset breaks every
            // reference to the old one, and the RoundManager array is exactly
            // that -- fifty references somebody may have already tuned
            RoundData data = AssetDatabase.LoadAssetAtPath<RoundData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RoundData>();
                AssetDatabase.CreateAsset(data, path);
                made++;
            }
            else
            {
                updated++;
            }

            SerializedObject so = new SerializedObject(data);

            so.FindProperty("totalCustomers").intValue = table[i].customers;
            so.FindProperty("spawnInterval").floatValue = table[i].interval;
            so.FindProperty("maxOrderTypes").intValue = Types(round);
            so.FindProperty("note").stringValue = "Raund " + round + " -- " + Band(round);

            so.ApplyModifiedPropertiesWithoutUndo();

            rounds[i] = data;
        }

        AssetDatabase.SaveAssets();

        string report = "- " + made + " raund olusturuldu, " + updated + " raund guncellendi\n" +
                        "- " + folder + "\n" +
                        "- Raund 1-10: tek cesit siparis.  Raund 11-50: iki cesit\n";

        Wire(rounds, ref report);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Raundlar\n" + report);
        EditorUtility.DisplayDialog("Raundlar", report, "Tamam");
    }

    // Every round set to two kinds, for looking at the thing rather than
    // playing eleven rounds to reach it.
    //
    // A command rather than an edit to the table above, because the way back
    // has to be something that already exists: re-running the generator writes
    // the real curve over this, and nobody has to remember what was changed
    [MenuItem("Cooked Fast/Oyun/Raund: TEST - Hepsi 2 Cesit", priority = 233)]
    public static void TestTwoTypes()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Raundlar",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:RoundData", new[] { folder });

        if (guids.Length <= 0)
        {
            EditorUtility.DisplayDialog("Raundlar",
                "Once raundlari uret:\nCooked Fast > Oyun > Raund: 50 Raundu Uret", "Tamam");
            return;
        }

        foreach (string guid in guids)
        {
            RoundData data = AssetDatabase.LoadAssetAtPath<RoundData>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (data == null)
                continue;

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("maxOrderTypes").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();

        string report = guids.Length + " raund 2 cesite ayarlandi.\n" +
                        "Raund 1'den itibaren musteriler iki farkli urun isteyecek.\n\n" +
                        "Musteri basina adet degismedi: 3'luk bir siparis 2 + 1 olur.\n" +
                        "Tek urun gormek icin bir tezgahta tek yemek olmasi yeterli --\n" +
                        "menude tek cesit varsa satir da tek olur.\n\n" +
                        "GERI ALMAK ICIN: Cooked Fast > Oyun > Raund: 50 Raundu Uret\n" +
                        "Gercek egriyi (1-10 tek, 11-50 cift) uzerine yazar.";

        Debug.Log("Raundlar\n" + report);
        EditorUtility.DisplayDialog("Raundlar", report, "Tamam");
    }

    private static void Wire(RoundData[] rounds, ref string report)
    {
        RoundManager manager = Object.FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);

        if (manager == null)
        {
            GameObject host = new GameObject("Round Manager");
            Undo.RegisterCreatedObjectUndo(host, "Create Round Manager");

            manager = Undo.AddComponent<RoundManager>(host);
            report += "- Round Manager kuruldu\n";
        }
        else
        {
            report += "- Mevcut Round Manager kullanildi\n";
        }

        SerializedObject so = new SerializedObject(manager);

        SerializedProperty list = so.FindProperty("rounds");
        list.arraySize = rounds.Length;

        for (int i = 0; i < rounds.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = rounds[i];

        // Filled in here rather than left to the runtime lookup, so the list is
        // something that can be looked at and edited before pressing play
        FoodServingCustomerManager[] counters =
            Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        SerializedProperty wired = so.FindProperty("counters");
        wired.arraySize = counters.Length;

        for (int i = 0; i < counters.Length; i++)
            wired.GetArrayElementAtIndex(i).objectReferenceValue = counters[i];

        so.ApplyModifiedProperties();

        report += "- " + counters.Length + " tezgah baglandi";

        if (counters.Length <= 0)
            report += "  UYARI: tezgah yok, hic musteri gelmez";

        report += "\n\nTezgahlarin kendi Customer Interval degeri artik kullanilmiyor:\n" +
                  "raund suresini RoundManager veriyor. Maks slot sayisi hala\n" +
                  "tezgahta -- wave 20 kisi olsa da ayni anda o kadari durur.";
    }
}
#endif
