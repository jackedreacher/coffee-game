#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Live dial for how the revolver sits in the hand.
//
// The gun exists for about a second during a shot, which is nowhere near long
// enough to judge a rotation by eye -- so this holds it in the hand and leaves
// it there. The held gun is built by the same code the shot uses, so what is
// being adjusted is the real thing rather than a stand-in that might sit
// somewhere else.
//
// Everything is written straight to Assets/Resources/Hats/Hat Powers.asset,
// which is what the game reads. There is no separate editor-only copy to get
// out of step, and a value dialled here works in a build.
public sealed class RevolverTuner : EditorWindow
{
    private HatPowerCatalogue book;

    [MenuItem("Cooked Fast/Sapka/Silah Ince Ayar", priority = 228)]
    public static void Open()
    {
        RevolverTuner window = GetWindow<RevolverTuner>("Silah Ince Ayar");

        window.minSize = new Vector2(360f, 400f);
        window.Show();
    }

    // The player moves and the hand bone with it, so the held gun has to be
    // repainted rather than drawn once when the window opens.
    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnDisable()
    {
        // Handing the gun back to the hat on the way out. Forced out without a
        // cowboy hat it would otherwise stay out for the rest of the session,
        // with no window left to switch it off.
        //
        // Only while playing: this also fires on domain reload and on leaving
        // Play, and Destroy outside Play Mode is a warning nobody asked for.
        if (!EditorApplication.isPlaying)
            return;

        RevolverPower power = Power(false);

        if (power != null)
            power.HidePreview();
    }

    private void OnGUI()
    {
        if (book == null)
            book = AssetDatabase.LoadAssetAtPath<HatPowerCatalogue>(
                HatPowerCatalogue.AssetPath);

        if (book == null)
        {
            EditorGUILayout.HelpBox(
                "Hat Powers.asset yok.\n\n" +
                "Once: Cooked Fast > Sapka > Sapka Yeteneklerini Kur",
                MessageType.Warning);

            return;
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode'da calistir. Silah oyuncunun el kemigine takiliyor; " +
                "Edit Mode'da ortada oyuncu yok.\n\n" +
                "Akis: Play > asagidaki 'Silahi Elde Tut' > sliderlar > Kaydet.",
                MessageType.Info);
        }

        RevolverPower power = Power(EditorApplication.isPlaying);

        DrawHold(power);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Durus", EditorStyles.boldLabel);

        // Free fields, not clamped sliders. The right rotation for a model
        // whose axes nobody documented is not knowable in advance, so any range
        // picked here would eventually be the thing in the way -- the same
        // lesson the hat tuner already learned.
        book.gunTurn = new Vector3(
            Dial(new GUIContent("Cevir X",
                "Namlu yukari/asagi bakiyorsa."), book.gunTurn.x, -180f, 180f),
            Dial(new GUIContent("Cevir Y",
                "Namlu geriye bakiyorsa 180 yap."), book.gunTurn.y, -180f, 180f),
            Dial(new GUIContent("Cevir Z",
                "Kabza yukari bakiyorsa 180 yap."), book.gunTurn.z, -180f, 180f));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Yer", EditorStyles.boldLabel);

        book.gunReach = Dial(new GUIContent("Ileri",
            "Yumruktan one dogru. Govdenin icinde kaliyorsa buyut. " +
            "0 = varsayilan 0.12"), book.gunReach, 0f, 1f);

        book.gunLift = Dial(new GUIContent("Yukari",
            "Eksi = asagi. 0 = elin hizasinda."), book.gunLift, -.5f, .5f);

        book.gunSide = Dial(new GUIContent("Yana",
            "Eksi = sola. 0 = elin uzerinde."), book.gunSide, -.5f, .5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Boy ve menzil", EditorStyles.boldLabel);

        book.gunSize = Dial(new GUIContent("Silah boyu",
            "Iskelet uzunlugunun kati, yani her hayvanda ayni oranda. " +
            "0 = varsayilan 2.5"), book.gunSize, .2f, 4f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Kursun", EditorStyles.boldLabel);

        book.aimAtTarget = EditorGUILayout.Toggle(
            new GUIContent("Namluyu hedefe cevir",
                "DENEYSEL, kapali gelir. Acarsan namlu ates ederken hedefe " +
                "doner ve iz namlunun ucundan cikar -- ama yukaridaki 'Cevir' " +
                "degerleri gecersiz olur ve silah elde bambaska bir acida durur."),
            book.aimAtTarget);

        if (book.aimAtTarget)
            EditorGUILayout.HelpBox(
                "'Cevir X / Y / Z' su an GECERSIZ -- nisan acisi onlarin " +
                "yerine geciyor. Silahin eldeki durusu bozulduysa sebebi bu; " +
                "kapat.",
                MessageType.Warning);

        if (book.aimAtTarget)
        {
            book.aimTurn = new Vector3(
                Dial(new GUIContent("Nisan X",
                    "Namlu hedefin uzerinden/altindan geciyorsa."),
                    book.aimTurn.x, -180f, 180f),
                Dial(new GUIContent("Nisan Y",
                    "TERS yone ates ediyorsa 180 yap."),
                    book.aimTurn.y, -180f, 180f),
                Dial(new GUIContent("Nisan Z",
                    "Kabza yukari bakiyorsa 180 yap."),
                    book.aimTurn.z, -180f, 180f));

            EditorGUILayout.HelpBox(
                "Ates aninda yukaridaki 'Cevir' degerleri gecerli DEGIL -- " +
                "onlar silahin elde durus acisi. Nisan alirken silahin acisini " +
                "buradan duzelt.",
                MessageType.None);
        }

        book.muzzleHeight = Dial(new GUIContent("Cikis yuksekligi",
            "Kursun oyuncunun ayagindan kac birim yukaridan cikar. " +
            "0 = varsayilan 1"),
            book.muzzleHeight, 0f, 3f);

        book.muzzleAhead = Dial(new GUIContent("Cikis mesafesi",
            "Kursun oyuncunun kac birim onunden baslar. " +
            "0 = varsayilan 0.6"),
            book.muzzleAhead, 0f, 2f);

        EditorGUILayout.HelpBox(
            "Kursun artik SILAHTAN degil OYUNCUDAN cikiyor. Silahin yerini " +
            "degistirmek atisi kaydirmaz -- ikisi birbirinden bagimsiz.",
            MessageType.None);

        book.tracerWidth = Dial(new GUIContent("Iz kalinligi",
            "0 = varsayilan 0.045"),
            book.tracerWidth, 0f, .3f);

        book.tracerSeconds = Dial(new GUIContent("Iz suresi",
            "Kac saniye ekranda kalir. 0 = varsayilan 0.07"),
            book.tracerSeconds, 0f, .5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Zamanlama", EditorStyles.boldLabel);

        book.fireAt = Dial(new GUIContent("Ates ani",
            "Quickdraw basladiktan kac saniye sonra ates cikar. " +
            "Klibi izleyip horozun dustugu ana getir. 0 = varsayilan 0.45"),
            book.fireAt, 0f, 2f);

        book.holdAfter = Dial(new GUIContent("Sonra tut",
            "Atistan sonra silah kac saniye elde kalir. Bir sonraki " +
            "dokunus bu sureyi kesip yeni atisi baslatabilir. " +
            "0 = varsayilan 0.45"),
            book.holdAfter, 0f, 2f);

        book.clipSpeed = Dial(new GUIContent("Klip hizi",
            "Cekme animasyonu kac kat hizli oynar. Ates ani da ayni sayiya " +
            "bolunuyor, yani atis klibin ayni KARESINDE kalir, sadece daha " +
            "erken gelir. 0 = varsayilan 1.7"),
            book.clipSpeed, .5f, 3f);

        book.followSpeed = Dial(new GUIContent("Ard arda hizi",
            "Ates ederken gelen ikinci dokunus ilk atisin bitmesini " +
            "beklemiyor: horoz duser dusmez yeni hedefe donup tekrar ates " +
            "eder. O ikinci atis kac kat hizli oynar -- 2 = yarisi kadar " +
            "surer. 0 = varsayilan 2"),
            book.followSpeed, 1f, 4f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tabak", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Elde tabak varken ates edilirse tabak havaya atilir ve ayni yere " +
            "geri yakalanir. Yukselme EKRANA gore olculuyor: kamera tepeden " +
            "baktigi icin dunya yukarisi ekranda neredeyse hic yer " +
            "degistirmiyor.", MessageType.None);

        book.tossLift = Dial(new GUIContent("Firlatma yuksekligi",
            "Tabak ekranda ne kadar yukari cikar. 0 = varsayilan 1.6"),
            book.tossLift, 0f, 4f);

        book.tossSpins = Dial(new GUIContent("Takla sayisi",
            "Havadayken kac tam tur atar. Takla goruse dik eksende, cunku " +
            "yuvarlak bir tabagin kendi ekseninde donmesi gorunmuyor. " +
            "0 = varsayilan 1"),
            book.tossSpins, 0f, 4f);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(book);

            // Nudged, never rebuilt. Rebuilding would destroy the object the
            // scene view has selected, and the selection is the whole point --
            // it would drop the move handles on every frame of a drag.
            if (power != null)
                power.Apply(book);
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Kaydet", GUILayout.Height(26f)))
                Save();

            if (GUILayout.Button("Durusu Sifirla", GUILayout.Height(26f)))
            {
                book.gunTurn = Vector3.zero;
                book.gunReach = 0f;
                book.gunLift = 0f;
                book.gunSide = 0f;

                EditorUtility.SetDirty(book);

                if (power != null)
                    power.Apply(book);
            }

            using (new EditorGUI.DisabledScope(power == null))
            {
                if (GUILayout.Button("Test Atisi", GUILayout.Height(26f)))
                    TestShot(power);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Silah ele child olarak takiliyor ve bir daha dokunulmuyor -- "
            + "hareketin tamamini Quickdraw yapiyor. Buradaki alanlar silahin "
            + "ELIN ICINDEKI sabit durusu.\n\n"
            + "Namlu otomatik hizalaniyor -- modelin en uzun ekseni namlu "
            + "sayiliyor. 'Cevir' alanlari bunun USTUNE ekleniyor, yani hepsi 0 "
            + "iken tahmin dogru demektir. Ters duruyorsa once Cevir Y = 180 "
            + "dene, kabza yukaridaysa Cevir Z = 180.\n\n"
            + "Ileri/Yukari/Yana: 'Eli takip et' kapaliyken dunya yonunde, "
            + "acikken elin kendi yonunde olculur.",
            MessageType.None);
    }

    private void DrawHold(RevolverPower power)
    {
        EditorGUILayout.Space();

        if (power == null)
        {
            EditorGUILayout.HelpBox(
                "Sahnede TapToServe tasiyan oyuncu bulunamadi.",
                MessageType.Warning);

            return;
        }

        bool holding = power.PreviewOn;

        // The gun is permanent now -- it is in the hand for as long as the
        // cowboy hat is on, and this button no longer puts it there and takes
        // it away. What it does is force it out WITHOUT the hat, so the
        // placement can be dialled in without changing hats first.
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(power.Forced
                    ? "Sapka Kontrolune Birak"
                    : "Sapkasiz da Elde Tut",
                    GUILayout.Height(30f)))
            {
                if (power.Forced)
                    power.HidePreview();
                else
                {
                    string why = power.ShowPreview();

                    if (!string.IsNullOrEmpty(why))
                        EditorUtility.DisplayDialog("Silah", why, "Tamam");
                    else
                        Grab(power);
                }
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                // The hand is animated even while the character stands still,
                // so the handles drift under the cursor. Pausing freezes the
                // pose and turns this into ordinary scene editing.
                if (GUILayout.Button(
                        EditorApplication.isPaused ? "Devam Et" : "Oyunu Durdur",
                        GUILayout.Height(30f)))
                    EditorApplication.isPaused = !EditorApplication.isPaused;
            }
        }

        if (!holding)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sahnede Sec (W / E ile tut)",
                    GUILayout.Height(24f)))
                Grab(power);

            if (GUILayout.Button("Sahnedeki Yerinden Kaydet",
                    GUILayout.Height(24f)))
                CaptureFromScene(power);
        }

        EditorGUILayout.HelpBox(
            "Silah Hierarchy'de COWBOY REVOLVER. Sahne penceresinde W ile "
            + "tasi, E ile cevir, R ile boyutlandir -- elin icine oturtana "
            + "kadar. Sonra 'Sahnedeki Yerinden Kaydet'.\n\n"
            + "Kaydetmek asset'e yaziyor, sahneye degil, o yuzden Play'i "
            + "durdurunca kaybolmuyor. Kaydetmeden durdurursan kaybolur.",
            MessageType.Info);
    }

    // Hands the held gun to Unity's own move and rotate handles. Placing it by
    // dragging it is the same act as placing anything else in a scene, and a
    // great deal more direct than three sliders describing a rotation nobody
    // can picture.
    private static void Grab(RevolverPower power)
    {
        GameObject gun = power == null ? null : power.PreviewObject;

        if (gun == null)
            return;

        Selection.activeGameObject = gun;

        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
            SceneView.lastActiveSceneView.Focus();
        }
    }

    private void CaptureFromScene(RevolverPower power)
    {
        if (power == null || book == null || !power.Capture(book))
        {
            EditorUtility.DisplayDialog("Kaydet",
                "Elde tutulan silah yok. Once 'Silahi Elde Tut'.", "Tamam");

            return;
        }

        Save();
    }

    // A slider with no floor and no ceiling: the box takes anything typed and
    // the ends step aside for a value that lives outside them, but stay put for
    // ordinary ones, which is where the fine control has to be.
    private const float boxWidth = 58f;
    private const float gap = 6f;

    private static float Dial(GUIContent label, float value, float lo, float hi)
    {
        // Rounded OUTWARD to a tenth rather than offset from the value.
        //
        // An end computed as "value minus a margin" moves every time the value
        // does, so holding the handle against it drags the end away and the
        // number runs off on its own. Rounded, the end only shifts when the
        // value crosses a tenth -- and the slider itself cannot produce a value
        // outside its own range, so during a drag it never shifts at all.
        float min = Mathf.Min(lo, Mathf.Floor(value * 10f - 2f) / 10f);
        float max = Mathf.Max(hi, Mathf.Ceil(value * 10f + 2f) / 10f);

        Rect row = EditorGUILayout.GetControlRect();
        Rect box = new Rect(row.xMax - boxWidth, row.y, boxWidth, row.height);
        Rect bar = EditorGUI.PrefixLabel(new Rect(row.x, row.y,
            row.width - boxWidth - gap, row.height), label);

        float thick = GUI.skin.horizontalSlider.fixedHeight;

        if (thick > 0f && thick < bar.height)
        {
            bar.y += (bar.height - thick) * .5f;
            bar.height = thick;
        }

        value = GUI.HorizontalSlider(bar, value, min, max);

        return EditorGUI.FloatField(box, value);
    }

    private static RevolverPower Power(bool make)
    {
        TapToServe player = Object.FindFirstObjectByType<TapToServe>(
            FindObjectsInactive.Include);

        if (player == null)
            return null;

        RevolverPower power = player.GetComponent<RevolverPower>();

        if (power == null && make)
            power = player.gameObject.AddComponent<RevolverPower>();

        return power;
    }

    // Fires at whatever is nearest and has something a shot could do, so the
    // whole draw-spin-bang can be watched without walking the player anywhere.
    private static void TestShot(RevolverPower power)
    {
        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Interactable best = null;
        float nearest = float.MaxValue;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is not IShootable shootable || !shootable.CanTakeShot)
                continue;

            Interactable door = all[i].GetComponentInChildren<Interactable>(true);

            if (door == null)
                door = all[i].GetComponentInParent<Interactable>();

            if (door == null)
                continue;

            float distance = Vector3.SqrMagnitude(
                shootable.ShotAimPoint - power.transform.position);

            if (distance >= nearest)
                continue;

            nearest = distance;
            best = door;
        }

        if (best == null)
        {
            EditorUtility.DisplayDialog("Test Atisi",
                "Su an vurulacak bir sey yok.\n\n" +
                "Bos bir fritoz gerekiyor -- silah baska istasyona islemiyor.",
                "Tamam");

            return;
        }

        // The shot fires from the gun already in the hand, so there is
        // nothing to put away first any more.
        if (!power.TryShoot(best))
            EditorUtility.DisplayDialog("Test Atisi",
                "Silah reddetti. Sebebi Console'da yaziyor -- " +
                "genelde kovboy sapkasi takili degildir.", "Tamam");
    }

    private void Save()
    {
        EditorUtility.SetDirty(book);
        AssetDatabase.SaveAssets();

        // The runtime caches the asset on first use. It is the same object in
        // memory here, so nothing needs reloading -- but saying so beats leaving
        // somebody wondering whether Play has to be restarted.
        Debug.Log("[Silah] Ayarlar yazildi: " + HatPowerCatalogue.AssetPath +
                  "\n  cevir " + book.gunTurn +
                  "\n  ileri " + book.gunReach.ToString("0.000") +
                  "   yukari " + book.gunLift.ToString("0.000") +
                  "   yana " + book.gunSide.ToString("0.000") +
                  "\n  boy " + book.GunSize.ToString("0.000") +
                  "\n  klip hizi x" + book.ClipSpeed.ToString("0.0"), book);
    }
}
#endif
