#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Writes a starting prep time onto every food prefab.
//
// A starting point, not an answer. The right number for a burger is however
// long it actually takes to build and cook one in THIS kitchen, and that is
// something only playing it tells you -- but a table of guesses that are the
// right shape beats five prefabs all sitting at zero
public static class PrepTimeSetup
{
    // Yemek adi -> bir tanesi kac saniyelik is.
    //
    // Ready off a shelf at the bottom, built and cooked at the top. The order
    // matters more than the numbers: whatever these become, a drink must stay
    // below a burger or the clock is lying about the work
    // Burger is measured, not guessed: three of them is meant to be 45 seconds,
    // and the formula is base + count x this. (45 - 3.5) / 3 = 13.83.
    //
    // Pizza moved with it. It was above burger before and the kitchen has not
    // changed -- a pizza is at least as much work -- so leaving it at 12 would
    // have made it the cheap order purely because nobody measured it yet.
    //
    // The rest are still the guesses. They are the dishes that come ready off a
    // shelf, and none of them have been the ones running out. The SATIS line in
    // the console gives the real figure for each: its PARCA BASI is exactly the
    // number that belongs here
    private static readonly Dictionary<string, float> times = new Dictionary<string, float>
    {
        { "drink", 6f },
        { "cup", 6f },
        { "salad", 6f },
        { "fries", 9f },
        { "pizza", 15f },
        { "burger", 13.83f }
    };

    private const float fallback = 9f;

    [MenuItem("Cooked Fast/Musteri/Yemek Sureleri", priority = 231)]
    public static void Setup()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        List<SpawnableFood> foods = new List<SpawnableFood>();
        int alreadySet = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null || !prefab.TryGetComponent(out SpawnableFood food))
                continue;

            // Ingredients are never ordered, so a wait time for one is a number
            // that can only ever be wrong in the inspector
            if (food.IngredientOnly)
                continue;

            foods.Add(food);

            if (food.PrepSeconds > .01f)
                alreadySet++;
        }

        // Asked rather than assumed. This used to refuse to touch a value that
        // was already set, which protected hand tuning and also made the
        // command unable to correct its own bad first guess
        bool overwrite = alreadySet <= 0 || EditorUtility.DisplayDialog("Yemek Sureleri",
            alreadySet + " yemekte zaten sure yazili.\n\n" +
            "Uzerine yazayim mi? Elle ayarladiysan HAYIR de --\n" +
            "oynayarak bulunmus bir sayi tablodaki tahminden degerlidir.",
            "Uzerine yaz", "Dokunma");

        string report = "";
        int written = 0;
        int kept = 0;

        foreach (SpawnableFood food in foods)
        {
            GameObject prefab = food.gameObject;

            if (food.PrepSeconds > .01f && !overwrite)
            {
                report += "- " + prefab.name + ": " + food.PrepSeconds.ToString("0.0") +
                          " sn (dokunulmadi)\n";
                kept++;
                continue;
            }

            float seconds = Guess(prefab.name);

            SerializedObject so = new SerializedObject(food);
            so.FindProperty("prepSeconds").floatValue = seconds;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(prefab);

            report += "- " + prefab.name + ": " + seconds.ToString("0.0") + " sn\n";
            written++;
        }

        AssetDatabase.SaveAssets();

        if (written + kept <= 0)
        {
            report = "Hic servis edilebilir yemek prefabi bulunamadi.";
        }
        else
        {
            report = "- " + written + " yemege sure yazildi, " + kept + " tanesine dokunulmadi\n\n" +
                     report +
                     "\nMusteri sabri = 3.5 + adet x bu sure, en az 20 sn.\n" +
                     "1 burger: 20 sn (taban kazanir).  2 burger: 31 sn.  3 burger: 45 sn.\n\n" +
                     "Bunlar hala tahmin. Oyna, konsoldaki SATIS satirlarina bak:\n" +
                     "PARCA BASI rakami dogrudan buraya yazilacak sayidir.";
        }

        Debug.Log("Yemek Sureleri\n" + report);
        EditorUtility.DisplayDialog("Yemek Sureleri", report, "Tamam");
    }

    // Matched on the name because that is what the prefabs are distinguished
    // by. "burger 1" and "Burger" are the same dish and the same work
    private static float Guess(string name)
    {
        string plain = name.ToLowerInvariant();

        foreach (KeyValuePair<string, float> entry in times)
        {
            if (plain.Contains(entry.Key))
                return entry.Value;
        }

        return fallback;
    }
}
#endif
