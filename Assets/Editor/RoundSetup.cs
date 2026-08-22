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
    private const int concurrentCustomers = 4;

    // Musteri sayisi, gelis araligi. Raund 1'den 50'ye
    private static readonly (int customers, float interval)[] table =
    {
        (4, 12.0f), (4, 11.5f), (5, 11.0f), (5, 10.5f), (6, 10.0f),
        (6, 9.7f), (7, 9.4f), (7, 9.1f), (8, 8.8f), (8, 8.5f),
        (9, 8.2f), (9, 8.0f), (10, 7.8f), (10, 7.6f), (11, 7.4f),
        (11, 7.2f), (12, 7.0f), (12, 6.8f), (13, 6.6f), (13, 6.4f),
        (14, 6.2f), (14, 6.0f), (15, 5.9f), (15, 5.8f), (16, 5.7f),
        (16, 5.6f), (17, 5.5f), (17, 5.4f), (18, 5.3f), (18, 5.2f),
        (19, 5.1f), (19, 5.0f), (20, 4.9f), (20, 4.8f), (21, 4.7f),
        (21, 4.6f), (22, 4.5f), (22, 4.4f), (23, 4.3f), (23, 4.2f),
        (24, 4.1f), (24, 4.0f), (25, 3.9f), (25, 3.8f), (26, 3.7f),
        (26, 3.6f), (27, 3.5f), (28, 3.4f), (29, 3.3f), (30, 3.2f)
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

    [MenuItem("Cooked Fast/Oyun/Raund: 4 Musterilik 50 Raundu Kur", priority = 230)]
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

        string report = "- 4 ayni-anlik musteriye gore dengelendi\n" +
                        "- " + made + " raund olusturuldu, " + updated + " raund guncellendi\n" +
                        "- " + folder + "\n" +
                        "- Raund 1-10: tek cesit siparis.  Raund 11-50: iki cesit\n" +
                        "- Ilk raund 4, final raund 30 toplam musteri\n";

        Wire(rounds, ref report);

        report += SoundSetup.InstallRoundIntro(false)
            ? "- Mellow Hint 2 raund giris sesine baglandi\n"
            : "- UYARI: raund giris sesi baglanamadi\n";

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

    // Four customers in one burst for checking the four-slot layout. They are
    // separated by two tenths rather than instantiated on the exact same frame:
    // four agents born on one point push each other before any of them has a
    // path, which tests NavMesh overlap instead of the queue the designer wants
    // to see. Re-running Generate restores the real 12-second first round.
    [MenuItem("Cooked Fast/Oyun/Raund: TEST - Ilk Raunda 4 Musteri Birden", priority = 234)]
    public static void TestFourAtOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raundlar",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        string path = folder + "/Round 01.asset";
        RoundData first = AssetDatabase.LoadAssetAtPath<RoundData>(path);

        if (first == null)
        {
            EditorUtility.DisplayDialog("Raundlar",
                "Round 01 bulunamadi. Once su komutu calistir:\n" +
                "Cooked Fast > Oyun > Raund: 4 Musterilik 50 Raundu Kur",
                "Tamam");
            return;
        }

        SerializedObject so = new SerializedObject(first);
        so.FindProperty("totalCustomers").intValue = 4;
        so.FindProperty("spawnInterval").floatValue = .2f;
        so.FindProperty("maxOrderTypes").intValue = 1;
        so.FindProperty("note").stringValue =
            "TEST -- ilk raundda 4 musteri 0.2 saniye arayla";
        so.ApplyModifiedPropertiesWithoutUndo();

        // The round says how many people are owed; the counter's own slot
        // array says how many may exist at once. Changing only the asset leaves
        // an old one-slot scene physically unable to spawn customer two, so
        // this test must set both halves of the rule.
        FoodServingCustomerManager[] counters =
            Object.FindObjectsByType<FoodServingCustomerManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        int countersSet = 0;

        for (int i = 0; i < counters.Length; i++)
        {
            if (counters[i] == null)
                continue;

            SerializedObject counter = new SerializedObject(counters[i]);
            counter.FindProperty("maxCustomers").intValue = 4;
            counter.FindProperty("customersPerRow").intValue = 4;
            SetFourWideSpacing(counter);
            counter.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(counters[i]);
            countersSet++;
        }

        RoundManager manager = Object.FindFirstObjectByType<RoundManager>(
            FindObjectsInactive.Include);

        if (manager != null)
        {
            SerializedObject roundManager = new SerializedObject(manager);
            roundManager.FindProperty("startRound").intValue = 1;
            roundManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string report =
            "Round 01 test moduna alindi.\n\n" +
            "- Toplam 4 musteri\n" +
            "- 0.2 saniye aralik\n" +
            "- " + countersSet + " kasanin kapasitesi 4, satiri 4 yapildi\n" +
            "- Baslangic raundu 1 yapildi\n" +
            "- 4 slot hizlica dolacak\n\n" +
            "Normale donmek icin:\n" +
            "Cooked Fast > Oyun > Raund: 4 Musterilik 50 Raundu Kur";

        Debug.Log("Raundlar\n" + report, first);
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
        {
            wired.GetArrayElementAtIndex(i).objectReferenceValue = counters[i];

            SerializedObject counter = new SerializedObject(counters[i]);
            counter.FindProperty("maxCustomers").intValue = concurrentCustomers;
            counter.FindProperty("customersPerRow").intValue = concurrentCustomers;
            SetFourWideSpacing(counter);
            counter.ApplyModifiedProperties();
        }

        so.ApplyModifiedProperties();

        report += "- " + counters.Length + " tezgah baglandi; her biri yan yana " +
                  concurrentCustomers + " musteri";

        if (counters.Length <= 0)
            report += "  UYARI: tezgah yok, hic musteri gelmez";

        report += "\n\nTezgahlarin kendi Customer Interval degeri artik kullanilmiyor:\n" +
                  "raund suresini RoundManager veriyor. Maks slot sayisi hala\n" +
                  "tezgahta -- wave 20 kisi olsa da ayni anda o kadari durur.";
    }

    private static void SetFourWideSpacing(SerializedObject counter)
    {
        SerializedProperty spacing = counter.FindProperty("sideSpacing");
        Vector3 direction = spacing.vector3Value;

        if (direction.sqrMagnitude < .0001f)
            direction = Vector3.forward;

        // Four positions span three gaps. Capsule bodies need substantially
        // more than one unit to leave visible air between their outlines; 1.5
        // is wide enough to read as four separate service spots. The centre
        // offset below keeps the block aligned with the useful counter area.
        spacing.vector3Value = direction.normalized * 1.50f;

        SerializedProperty centre = counter.FindProperty("sideCentreOffset");

        if (centre != null)
            centre.floatValue = -.30f;

        SerializedProperty bubbleScale =
            counter.FindProperty("fourWideBubbleScale");

        if (bubbleScale != null)
            bubbleScale.floatValue = .68f;
    }
}
#endif
