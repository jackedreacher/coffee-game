#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using TMPro;
using UnityEditor.Animations;
using UnityEngine;

// Builds the asset the hat powers read their props out of.
//
// The revolver lives in the Dead West folder, which is not under Resources, and
// the player prefab has no field to hold it. The alternatives were moving a
// third-party asset out of its own folder or adding a serialized field to a
// component already saved in the scene -- so instead one ScriptableObject is
// written here, under Resources, and the runtime asks for it by path.
//
// Re-running is safe: an existing asset is updated in place, and anything
// already filled in by hand is left alone.
public static class HatPowerSetup
{
    private const string revolverPath =
        "Assets/Dead West - Animated Western Weapons/Prefabs/Revovler.prefab";

    private const string hatFolder = "Assets/LowpolyHats/Prefabs";

    private const string gunshotPath =
        "Assets/sesler/revolver-firing_CpBMZHu.mp3";

    private const string finePath =
        "Assets/Design Toolbox/Fonts/Coolvetica SDF.asset";

    private const string deadEmojiPath =
        "Assets/Emojis 45/Prefabs/Stroked emojis in World Space Canvas/" +
        "Canvas Emoji dead Stroke.prefab";

    private const string westFolder =
        "Assets/YashMakesGames/Wild West Animation Pack/";

    private const string waiterFolder =
        "Assets/VFXPACK_FIRE_WALLCOEUR/Waiter_Anims/Art/Animations/";

    // How the cowboy stands, walks and says hello.
    //
    // Every one of these is a pose built around a gun that is already in the
    // hand, which is why the gun stopped being something the shot conjures up
    // and started being something the hat carries. Duel_Idle was the old idle
    // and is the wrong one now: it is a man hovering over a holster he has
    // already emptied.
    private static readonly string[][] stanceDefaults =
    {
        new[] { "Idle", westFolder + "Idle/Idle_w_Revolver.fbx" },
        new[] { "Walk", westFolder + "Run/Run_w_Gun.fbx" },
        new[] { "Greet_Start", waiterFolder + "Waiter_Idle_Greeting_Single.fbx" },
    };

    [MenuItem("Cooked Fast/Sapka/Sapka Yeteneklerini Kur", priority = 226)]
    public static void Setup()
    {
        StringBuilder report = new StringBuilder();

        HatPowerCatalogue book =
            AssetDatabase.LoadAssetAtPath<HatPowerCatalogue>(
                HatPowerCatalogue.AssetPath);

        bool made = book == null;

        if (made)
        {
            string folder = Path.GetDirectoryName(HatPowerCatalogue.AssetPath);

            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            book = ScriptableObject.CreateInstance<HatPowerCatalogue>();

            // A fresh asset starts holding the gun properly. Reach, lift and
            // side mean zero when they are zero, so an empty ScriptableObject
            // is a revolver stuck through the character's wrist -- and the
            // first thing anybody would do is dial back the numbers that are
            // already written down here.
            book.UseBasePlacement();
            book.placementVersion = HatPowerCatalogue.PlacementVersion;

            AssetDatabase.CreateAsset(book, HatPowerCatalogue.AssetPath);
        }

        report.AppendLine((made ? "Olusturuldu: " : "Guncellendi: ") +
                          HatPowerCatalogue.AssetPath);
        report.AppendLine();

        // Only when empty. Somebody who swapped the revolver for another model
        // meant it, and a setup command that undoes hand work every time it runs
        // is a command nobody dares run.
        if (book.revolverPrefab == null)
        {
            book.revolverPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(revolverPath);

            report.AppendLine("Silah: " + (book.revolverPrefab == null
                ? "BULUNAMADI -- " + revolverPath
                : book.revolverPrefab.name + "  (" + revolverPath + ")"));
        }
        else
            report.AppendLine("Silah: " + book.revolverPrefab.name +
                              "  (zaten dolu, dokunulmadi)");

        if (book.gunshot == null)
        {
            book.gunshot = AssetDatabase.LoadAssetAtPath<AudioClip>(gunshotPath);

            report.AppendLine("Atis sesi: " + (book.gunshot == null
                ? "BULUNAMADI -- " + gunshotPath
                : book.gunshot.name));
        }
        else
            report.AppendLine("Atis sesi: " + book.gunshot.name +
                              "  (zaten dolu, dokunulmadi)");

        // The fine is drawn by an object that does not exist until somebody is
        // shot, so there is no prefab field anywhere to hold its font.
        if (book.fineFont == null)
        {
            book.fineFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(finePath);

            report.AppendLine("Ceza yazisi fontu: " + (book.fineFont == null
                ? "BULUNAMADI -- " + finePath
                : book.fineFont.name));
        }
        else
            report.AppendLine("Ceza yazisi fontu: " + book.fineFont.name +
                              "  (zaten dolu, dokunulmadi)");

        if (book.deadEmoji == null)
        {
            book.deadEmoji =
                AssetDatabase.LoadAssetAtPath<GameObject>(deadEmojiPath);

            report.AppendLine("Olu emojisi: " + (book.deadEmoji == null
                ? "BULUNAMADI -- " + deadEmojiPath
                : book.deadEmoji.name));
        }
        else
            report.AppendLine("Olu emojisi: " + book.deadEmoji.name +
                              "  (zaten dolu, dokunulmadi)");

        // One-time clear of trim dialled against a placement that is gone.
        if (book.placementVersion < HatPowerCatalogue.PlacementVersion)
        {
            report.AppendLine("Eski durus ayarlari taban duruma alindi:");
            report.AppendLine("  cevir " + book.gunTurn +
                              "  ileri " + book.gunReach.ToString("0.00") +
                              "  yukari " + book.gunLift.ToString("0.00") +
                              "  yana " + book.gunSide.ToString("0.00"));
            report.AppendLine("  Bunlar silah havada dururken yapilan " +
                              "duzeltmelerdi; silah artik elde.");

            book.UseBasePlacement();
            book.placementVersion = HatPowerCatalogue.PlacementVersion;
        }

        report.AppendLine("Silah boyu: " + book.GunSize.ToString("0.00") +
                          " (iskeletin kati)");
        report.AppendLine("Elde kayma: ileri " + book.gunReach.ToString("0.00") +
                          "  yukari " + book.gunLift.ToString("0.00") +
                          "  yana " + book.gunSide.ToString("0.00"));
        report.AppendLine("Ek cevirme: " + book.gunTurn +
                          "   (ters/yan duruyorsa asset'ten cevir)");
        report.AppendLine("Ates ani: " + book.FireAt.ToString("0.00") +
                          " sn  (klip basindan, x" +
                          book.ClipSpeed.ToString("0.0") + " hizda " +
                          (book.FireAt / book.ClipSpeed).ToString("0.00") + " sn)");
        report.AppendLine("Menzil ve bekleme yok: her mesafeden, " +
                          "cekebildigi kadar hizli ates eder.");
        report.AppendLine("Musteri vurmak: " + book.CustomerFine +
                          " para, ceset " + book.BodySeconds.ToString("0.0") +
                          " sn yerde kalir, digerleri kacar.");
        report.AppendLine();
        report.Append(Stance(book));
        report.AppendLine("Namlu parlamasi: " +
                          (book.muzzleFlash == null ? "bos (istege bagli)"
                              : book.muzzleFlash.name));
        report.AppendLine("Atis sesi: " +
                          (book.gunshot == null ? "bos (sessiz ateş eder)"
                              : book.gunshot.name));

        EditorUtility.SetDirty(book);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        HatPowerCatalogue.Forget();

        report.AppendLine();
        report.Append(HatReport());

        report.AppendLine();
        report.AppendLine("Silah iki ise yariyor:");
        report.AppendLine("  bos fritoz -> uzaktan calisir");
        report.AppendLine("  musteri    -> cift dokunusla vurulur");
        report.AppendLine("Yemek yok etmiyor -- yanan patates de yanan et de");
        report.AppendLine("elle aliniyor. Yapacak isi olmayan istasyona");
        report.AppendLine("dokunmak eskisi gibi yuruttur.");
        report.AppendLine("Istasyonun COLLIDERININ ICINDEYKEN ates etmez:");
        report.AppendLine("dibindeysen el zaten orada, elle kullanir.");

        Debug.Log("[Sapka Yetenek]\n" + report);
        EditorUtility.DisplayDialog("Sapka Yetenekleri", report.ToString(), "Tamam");
    }

    // Everything the shot checks, checked out loud, in the order it checks it.
    //
    // "The gun does not appear" has seven possible causes and six of them are
    // silent -- no asset, no prefab in it, wrong hat on, no hand bone on the
    // rig, nothing shootable in range, a station with nothing to do. Running the
    // game and squinting cannot tell them apart. This can.
    [MenuItem("Cooked Fast/Sapka/Sapka Yetenegini Denetle", priority = 227)]
    public static void Inspect()
    {
        StringBuilder report = new StringBuilder();

        if (!EditorApplication.isPlaying)
            report.AppendLine("(Edit Mode -- oyuncu ve sapka icin Play'de calistir)\n");

        // ---- the asset ------------------------------------------------------
        HatPowerCatalogue book = AssetDatabase.LoadAssetAtPath<HatPowerCatalogue>(
            HatPowerCatalogue.AssetPath);

        report.AppendLine("1. Asset");

        if (book == null)
        {
            report.AppendLine("   YOK -- once 'Sapka Yeteneklerini Kur' calistir.");
            Finish(report);
            return;
        }

        report.AppendLine("   var: " + HatPowerCatalogue.AssetPath);
        report.AppendLine("   silah   : " + (book.revolverPrefab == null
            ? "BOS -- kurulum komutunu tekrar calistir"
            : book.revolverPrefab.name));
        report.AppendLine("   boy     : " + book.GunSize.ToString("0.00"));
        report.AppendLine("   kayma   : " + book.gunReach.ToString("0.00") + " / " +
                          book.gunLift.ToString("0.00") + " / " +
                          book.gunSide.ToString("0.00"));
        report.AppendLine("   cevirme : " + book.gunTurn);

        // ---- the hat --------------------------------------------------------
        report.AppendLine();
        report.AppendLine("2. Sapka");

        CharacterSkinPreview wardrobe = Object.FindFirstObjectByType<
            CharacterSkinPreview>(FindObjectsInactive.Include);

        if (wardrobe == null)
            report.AppendLine("   sahnede CharacterSkinPreview yok");
        else
        {
            string key = wardrobe.SelectedHatKey;

            report.AppendLine("   takili prefab : " +
                              (string.IsNullOrEmpty(key) ? "(sapka yok)" : key));
            report.AppendLine("   yetenek       : " + HatPowerBook.For(key));

            if (HatPowerBook.For(key) != HatPower.Revolver)
                report.AppendLine("   -> Kovboy sapkasi takili degil. Ates etmez.");
        }

        // ---- the hand -------------------------------------------------------
        report.AppendLine();
        report.AppendLine("3. El kemigi");

        PlayerAnimator player = Object.FindFirstObjectByType<PlayerAnimator>(
            FindObjectsInactive.Include);

        Animator rig = player == null ? null : player.CurrentAnimator;

        if (rig == null)
            report.AppendLine("   Player / Animator bulunamadi");
        else if (!rig.isHuman)
            report.AppendLine("   rig Humanoid degil -- el kemigi sorulamaz");
        else
        {
            Transform right = rig.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = rig.GetBoneTransform(HumanBodyBones.LeftHand);

            report.AppendLine("   sag el : " + (right == null ? "YOK" : right.name));
            report.AppendLine("   sol el : " + (left == null ? "YOK" : left.name));
            report.AppendLine("   iskelet boyu : " +
                              RevolverPower.RigHeight(rig).ToString("0.000"));

            if (right == null && left == null)
                report.AppendLine("   -> Hicbir el yok. Silah gorunmez, atis yine de isler.");
        }

        // ---- the gun's own measurements -------------------------------------
        report.AppendLine();
        report.AppendLine("4. Silah olculeri");

        if (book.revolverPrefab == null)
            report.AppendLine("   prefab bos, olculemedi");
        else
        {
            GameObject probe = Object.Instantiate(book.revolverPrefab);

            try
            {
                if (RevolverPower.Measure(probe.transform, probe.transform,
                        out Bounds box))
                {
                    float length = Mathf.Max(box.size.x,
                        Mathf.Max(box.size.y, box.size.z));

                    int live = RevolverPower.Visible(probe.transform);
                    int total = probe.GetComponentsInChildren<Renderer>(true).Length;

                    report.AppendLine("   renderer: " + live + " gorunur / " +
                                      total + " toplam" +
                                      (live < total
                                          ? "   <-- KAPALI OLANLAR VAR, silah acar"
                                          : ""));
                    int skinned = probe
                        .GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;

                    report.AppendLine("   skinned : " + skinned +
                                      (skinned > 0
                                          ? "   (kemikle animasyonlu -- kirpilma " +
                                            "kapatiliyor)"
                                          : ""));
                    report.AppendLine("   kutu    : " + box.size.ToString("0.000"));
                    report.AppendLine("   uzunluk : " + length.ToString("0.000"));
                    report.AppendLine("   merkez  : " + box.center.ToString("0.000") +
                                      (box.center.magnitude > length
                                          ? "   <-- pivot mesh'ten uzakta, ele ortalaniyor"
                                          : ""));
                }
                else
                    report.AppendLine("   RENDERER YOK -- bu prefabin icinde " +
                                      "cizilecek bir sey bulunamadi.");
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        // ---- what is actually shootable right now ---------------------------
        report.AppendLine();
        report.AppendLine("5. Su an vurulabilecekler");

        MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Vector3 from = player == null ? Vector3.zero : player.transform.position;
        int listed = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is not IShootable shootable)
                continue;

            Vector3 flat = shootable.ShotAimPoint - from;
            flat.y = 0f;

            // Distance is reported but no longer judged -- everything on this
            // list is reachable, because range is not a thing any more.
            report.AppendLine("   " + all[i].name +
                              "  mesafe " + flat.magnitude.ToString("0.0") +
                              (shootable.CanTakeShot ? "  HAZIR" : "  yapacak is yok"));
            listed++;
        }

        if (listed <= 0)
            report.AppendLine("   sahnede IShootable yok " +
                              "(FryerStation / CookingStation bekleniyordu)");

        Finish(report);
    }

    private static void Finish(StringBuilder report)
    {
        Debug.Log("[Sapka Yetenek Denetimi]\n" + report);
        EditorUtility.DisplayDialog("Sapka Yetenegi Denetimi",
            report + "\nTam metin Console'da.", "Tamam");
    }

    // The two halves of the stance swap, both found rather than typed.
    //
    // An AnimatorOverrideController is keyed on the clip being REPLACED, and
    // only the controller knows which clip its Idle state is carrying -- it is
    // Waiter_Pitcher_Idle today and whatever command 2 decides tomorrow. Read
    // here, so a change over there cannot leave this silently overriding a clip
    // nothing plays any more.
    private static string Stance(HatPowerCatalogue book)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Sapka takiliyken durus");

        List<StanceSwap> rows = new List<StanceSwap>();

        if (book.stance != null)
            rows.AddRange(book.stance);

        // Missing rows are added; rows that are already there are left exactly
        // as they are. Somebody who dropped a different clip into Walk meant
        // it, and a setup command that undoes hand work every time it runs is a
        // command nobody dares run twice.
        for (int i = 0; i < stanceDefaults.Length; i++)
        {
            string state = stanceDefaults[i][0];

            if (rows.Exists(row => row != null && row.state == state))
                continue;

            rows.Add(new StanceSwap
            {
                state = state,
                clip = FirstClip(stanceDefaults[i][1]),
            });
        }

        book.stance = rows.ToArray();

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                CapsuleCharacterSetup.PlayerControllerPath);

        if (controller == null)
        {
            report.AppendLine("  Capsule Player.controller yok -- once " +
                              "Cooked Fast > Karakter > 2 calistir.");

            return report.ToString();
        }

        for (int i = 0; i < rows.Count; i++)
        {
            StanceSwap row = rows[i];

            if (row == null || string.IsNullOrEmpty(row.state))
                continue;

            // Rewritten every run on purpose. Which clip a state is carrying is
            // a fact about the controller rather than a preference, and the
            // controller is rebuilt by another command that knows nothing about
            // this file.
            row.replaces = StateClip(controller, row.state);

            report.AppendLine("  " + row.state.PadRight(12) + " : " +
                              (row.clip == null
                                  ? "(bos -- bu state degismez)"
                                  : row.clip.name) +
                              "   <- " + (row.replaces == null
                                  ? "STATE YOK ya da klipsiz"
                                  : row.replaces.name));
        }

        report.AppendLine("  Silah artik sapka takiliyken hep elde -- " +
                          "atista oturup atistan sonra kaybolmuyor.");

        return report.ToString();
    }

    private static AnimationClip StateClip(AnimatorController controller,
        string state)
    {
        if (controller.layers.Length <= 0 ||
            controller.layers[0].stateMachine == null)
            return null;

        foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            if (child.state != null && child.state.name == state)
                return child.state.motion as AnimationClip;

        return null;
    }

    private static AnimationClip FirstClip(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return null;
    }

    // Which hats in the catalogue currently carry a power, said out loud.
    //
    // The lookup is a name fragment, so it is worth showing what it actually
    // matched rather than trusting that "the cowboy one" is spelled the way
    // HatPowerBook expects.
    private static string HatReport()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Sapkalar");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { hatFolder });

        if (guids.Length <= 0)
        {
            report.AppendLine("  " + hatFolder + " icinde prefab yok");

            return report.ToString();
        }

        int armed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string hat = Path.GetFileNameWithoutExtension(path);
            HatPower power = HatPowerBook.For(hat);

            if (power == HatPower.None)
                continue;

            report.AppendLine("  " + hat + "  ->  " + power);
            armed++;
        }

        if (armed <= 0)
            report.AppendLine("  hicbiri -- HatPowerBook'taki fragman listesine bak");
        else
            report.AppendLine("  digerlerinin yetenegi yok (HatPower.None)");

        return report.ToString();
    }
}
#endif
