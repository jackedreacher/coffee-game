#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Live dial for where each animal's hat sits and how big it is.
//
// Both numbers are multiples of the feet-to-head-bone distance, which is the
// one measurement every capsule animal agrees on. They are per animal on
// purpose: the bull's skull sits lower than the pug's while its horns own the
// top of the silhouette, so no single shared proportion can seat both.
//
// Slider moves are applied immediately and kept in memory. Saving writes them
// to Assets/Resources/Hats/hat_fit.json, which is what the game reads -- so a
// saved value works in a build too, unlike an editor-only tweak.
public sealed class HatTuner : EditorWindow
{
    private const float boxWidth = 58f;
    private const float gap = 6f;

    private Vector2 scroll;

    [MenuItem("Cooked Fast/Sapka/Sapka Ince Ayar", priority = 225)]
    public static void Open()
    {
        HatTuner window = GetWindow<HatTuner>("Sapka Ince Ayar");
        window.minSize = new Vector2(360f, 330f);
        window.Show();
    }

    // The wardrobe's selection changes while the game runs, so the window has
    // to keep looking rather than reading once when it opens.
    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        CharacterSkinPreview wardrobe = FindFirstObjectByType<
            CharacterSkinPreview>(FindObjectsInactive.Include);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode'da calistir. Sapka runtime'da takiliyor; Edit " +
                "Mode'da gorunecek bir sey yok.\n\n" +
                "Akis: Play > ana menu > karakter ekrani > oklarla hayvani sec " +
                "> buradaki uc ayar > Kaydet > sonraki hayvan.",
                MessageType.Info);
        }

        if (wardrobe == null)
        {
            EditorGUILayout.HelpBox("Sahnede CharacterSkinPreview yok.",
                MessageType.Warning);
            DrawTable();
            return;
        }

        string animal = wardrobe.SelectedAnimal;

        if (string.IsNullOrWhiteSpace(animal))
        {
            EditorGUILayout.HelpBox("Vitrinde secili hayvan okunamadi.",
                MessageType.Warning);
            DrawTable();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Su anki hayvan", animal.ToUpperInvariant(),
            EditorStyles.boldLabel);

        bool tuned = HatFitBook.TryGet(animal, out float crown, out float width,
            out float forward);

        EditorGUILayout.LabelField(tuned
            ? "Bu hayvanin kendi ayari var."
            : "Bu hayvan henuz ayarlanmadi -- ortak varsayilanlari kullaniyor.");

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        crown = Dial(new GUIContent("Yukseklik",
                "Buyurse sapka yukari cikar. Kenarin oturdugu yer."),
            crown, 1.1f, 2.1f);

        width = Dial(new GUIContent("Genislik", "Sapkanin boyu."),
            width, .35f, 1.2f);

        forward = Dial(new GUIContent("Ileri / Geri",
                "Arti = burna dogru, eksi = enseye dogru. 0 = kemigin ustunde."),
            forward, -.25f, .25f);

        if (EditorGUI.EndChangeCheck())
        {
            HatFitBook.Set(animal, crown, width, forward);
            wardrobe.RefreshHats();
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Kaydet", GUILayout.Height(26f)))
                Save();

            using (new EditorGUI.DisabledScope(!tuned))
            {
                if (GUILayout.Button("Bu Hayvani Sifirla", GUILayout.Height(26f)))
                {
                    HatFitBook.Forget(animal);
                    wardrobe.RefreshHats();
                }
            }
        }

        if (GUILayout.Button("Sapkayi Yeniden Tak"))
            wardrobe.RefreshHats();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Sagdaki kutuya her sayi yazilabilir -- eksi de dahil. Slider "
            + "sadece kolaylik: sinirlarin disinda bir deger yazarsan uclari "
            + "kendiliginden acilir. Degisiklikler hemen ekranda, ama sadece "
            + "bellekte: Kaydet'e basmadan Play'i kapatirsan kaybolur.",
            MessageType.None);

        DrawTable();
    }

    // A slider with no floor and no ceiling.
    //
    // EditorGUILayout.Slider clamps its typed box to the slider's own ends,
    // which is the wrong bargain here: what seats a hat on a bull is not
    // knowable up front, so any range picked in advance is a guess that ends up
    // in the way. The box takes whatever is typed -- zero, negative, anything
    // -- and the two ends step aside to make room for it.
    //
    // They step aside only for it. An ordinary value leaves them where they
    // were, because that is where the fine control has to live: a slider wide
    // enough for every value is precise enough for none of them, and these get
    // dialled to three decimals by eye.
    private static float Dial(GUIContent label, float value, float lo, float hi)
    {
        float min = Mathf.Min(lo, Mathf.Floor(value * 10f - 2f) / 10f);
        float max = Mathf.Max(hi, Mathf.Ceil(value * 10f + 2f) / 10f);

        Rect row = EditorGUILayout.GetControlRect();
        Rect box = new Rect(row.xMax - boxWidth, row.y, boxWidth, row.height);
        Rect bar = EditorGUI.PrefixLabel(new Rect(row.x, row.y,
            row.width - boxWidth - gap, row.height), label);

        // The slider graphic is shorter than a control row, and GUI draws it
        // flush to the top of whatever rect it is handed. Centre it so it lines
        // up with the number beside it.
        float thick = GUI.skin.horizontalSlider.fixedHeight;

        if (thick > 0f && thick < bar.height)
        {
            bar.y += (bar.height - thick) * .5f;
            bar.height = thick;
        }

        value = GUI.HorizontalSlider(bar, value, min, max);

        return EditorGUI.FloatField(box, value);
    }

    private void DrawTable()
    {
        List<HatFitBook.Entry> entries = HatFitBook.All();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ayarlanmis hayvanlar (" + entries.Count + ")",
            EditorStyles.boldLabel);

        if (entries.Count <= 0)
        {
            EditorGUILayout.LabelField("  -- henuz yok --");
            return;
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.animal, b.animal));

        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = 0; i < entries.Count; i++)
        {
            HatFitBook.Entry entry = entries[i];
            EditorGUILayout.LabelField("  " + entry.animal,
                "yukseklik " + entry.crown.ToString("0.###") +
                "   genislik " + entry.width.ToString("0.###") +
                "   ileri " + entry.forward.ToString("0.###"));
        }

        EditorGUILayout.EndScrollView();
    }

    private static void Save()
    {
        string folder = Path.GetDirectoryName(HatFitBook.AssetPath);

        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        File.WriteAllText(HatFitBook.AssetPath, HatFitBook.ToJson());
        AssetDatabase.ImportAsset(HatFitBook.AssetPath);
        AssetDatabase.Refresh();

        // The cache is deliberately left alone. It already holds exactly what
        // was written, and dropping it here would make the hat jump back to the
        // defaults for one frame if the reimport had not finished -- which is
        // the one symptom this whole exercise exists to get rid of.
        Debug.Log("[Sapka] Ayarlar yazildi: " + HatFitBook.AssetPath);
    }
}
#endif
