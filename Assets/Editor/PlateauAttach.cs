using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Hangs the tray off a hand bone instead of the body root. Parented to the root
// it keeps a fixed offset from the character's origin, so the arms swing through
// it and it hangs there dead while everything else animates
public static class PlateauAttach
{
    public const string customersFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";

    // Best to worst. A rig with no separate hand still ends its arm chain
    // somewhere, and the tip of the arm is where a hand would be
    private static readonly string[] handKeys = { "hand", "wrist", "palm", "grip", "socket", "attach" };
    private static readonly string[] armKeys = { "forearm", "arm" };

    // Finger bones. Used twice: a bone named after one is past the palm and must
    // not be picked, and a bone that PARENTS several of them is the palm itself
    private static readonly string[] fingerKeys =
    {
        "index", "thumb", "middle", "ring", "pinky", "little", "finger",
    };

    // Moves a tray that is on the wrong parent and leaves alone one that is
    // already right, so running it twice never costs an afternoon of hand placing
    [MenuItem("Cooked Fast/Arac/Attach Plateaus To Hands")]
    public static void AttachPlateausToHands()
    {
        Run(false);
    }

    // For when the placement is beyond saving and a clean start beats nudging
    [MenuItem("Cooked Fast/Arac/Reset Plateau Placement")]
    public static void ResetPlateauPlacement()
    {
        if (!EditorUtility.DisplayDialog("Plateau Sifirla",
                "Player ve 7 musterinin tepsi konumu sifirlanacak.\nElle yaptigin ayar gider.",
                "Sifirla", "Vazgec"))
            return;

        Run(true);
    }

    private static void Run(bool force)
    {
        string report = AttachPlayer(force) + AttachCustomerPrefabs(force);

        AssetDatabase.SaveAssets();

        Debug.Log("Plateau bagla:\n" + report);
        EditorUtility.DisplayDialog("Plateau Bagla", report, "Tamam");
    }

    private static string AttachPlayer(bool force)
    {
        PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (controllers.Length <= 0)
            return "- Sahnede PlayerController yok\n";

        Transform root = controllers[0].transform;
        Plateau plateau = FindPlateau(root);

        if (plateau == null)
            return "- Player: Plateau yok\n";

        // The body the PlayerAnimator actually drives, not whatever old model is
        // still sitting switched off next to it
        Transform visual = FindVisual(root);
        Transform hand = FindHand(visual, plateau.transform);

        if (hand == null)
            return "- Player: " + visual.name + " icinde el kemigi yok, liste konsolda\n" + DumpBones(visual);

        // Anything already hanging off a bone was put there deliberately, by the
        // matcher or by hand. Guessing again would throw away placement that has
        // been checked against the actual carry pose
        if (!force && IsOnBone(plateau.transform, visual))
            return "- Player: " + plateau.transform.parent.name + " uzerinde, ayar korundu\n";

        Vector3 worldScale = plateau.transform.lossyScale;

        Undo.SetTransformParent(plateau.transform, hand, "Attach plateau to hand");
        Undo.RecordObject(plateau.transform, "Place plateau in hand");

        PlaceInHand(plateau.transform, hand, worldScale);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        return "- Player: " + plateau.transform.parent.name + " (sifirlandi)\n";
    }

    private static string AttachCustomerPrefabs(bool force)
    {
        string report = "";

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Plateau plateau = FindPlateau(root.transform);

                if (plateau == null)
                {
                    report += "- " + name + ": Plateau yok\n";
                    continue;
                }

                Transform hand = FindHand(FindVisual(root.transform), plateau.transform);

                if (hand == null)
                {
                    report += "- " + name + ": el kemigi bulunamadi\n";
                    continue;
                }

                if (!force && IsOnBone(plateau.transform, FindVisual(root.transform)))
                {
                    report += "- " + name + ": " + plateau.transform.parent.name + ", ayar korundu\n";
                    continue;
                }

                Vector3 worldScale = plateau.transform.lossyScale;

                PlaceInHand(plateau.transform, hand, worldScale);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                report += "- " + name + ": " + hand.name + " (sifirlandi)\n";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return report;
    }

    // Sits the tray ON the hand rather than keeping where it used to hang.
    //
    // Keeping the world placement sounds safer and is the wrong answer here: the
    // reparent happens in the bind pose, arms down, while the tray was authored
    // out in front of the chest. That difference gets frozen into the offset, and
    // the moment a carry clip raises the arm the tray shoots off with it.
    // Every _Holding clip is animated on the assumption that what is carried sits
    // at the hand's own origin, so that is where it goes
    private static void PlaceInHand(Transform plateau, Transform hand, Vector3 worldScale)
    {
        plateau.SetParent(hand, false);

        plateau.localPosition = Vector3.zero;
        plateau.localRotation = Quaternion.identity;

        // Size is the one thing worth carrying over. Bones in this pack are
        // scaled about twenty five times up, so leaving local scale at one
        // would produce a serving tray the size of the room
        Vector3 handScale = hand.lossyScale;

        plateau.localScale = new Vector3(
            Divide(worldScale.x, handScale.x),
            Divide(worldScale.y, handScale.y),
            Divide(worldScale.z, handScale.z));

        // Offsets left on the slots were measured against the parent the tray
        // used to hang off, and mean nothing against this one. They are cleared
        // only here, on the deliberate start-over path -- Plateau now keeps the
        // first slot's offset as the base of the stack, so everywhere else it is
        // hand tuning that must survive
        foreach (FoodPosition slot in plateau.GetComponentsInChildren<FoodPosition>(true))
            slot.transform.localPosition = Vector3.zero;
    }

    private static float Divide(float world, float parent)
    {
        return Mathf.Abs(parent) < .0001f ? 1f : world / parent;
    }

    private static bool IsOnBone(Transform plateau, Transform visual)
    {
        Transform parent = plateau.parent;

        return parent != null && parent != visual && parent.IsChildOf(visual);
    }

    // The baseline, in a file rather than in these lines.
    //
    // It was literals, for a good reason: the only other copy was in git, and
    // reverting a .prefab through git throws away everything else in the file
    // with it. A json beside the script keeps that immunity -- it is not a
    // prefab, so reverting it costs nothing else -- and it can be rewritten
    // from the editor the moment a better placement is found by hand, which is
    // how every good placement in this project has actually come about.
    //
    // The literals below stay on as the factory default, for when the file is
    // missing or unreadable
    public const string knownGoodFile = "Assets/Editor/PlateauKnownGood.json";

    public static Placement KnownGoodCustomer
    {
        get
        {
            if (!System.IO.File.Exists(knownGoodFile))
                return Factory;

            Placement saved = JsonUtility.FromJson<Placement>(
                System.IO.File.ReadAllText(knownGoodFile));

            // A json that parses to nothing is worse than no json at all: a
            // zero scale is a placement that would stamp seven invisible trays
            return saved.trayScale == Vector3.zero ? Factory : saved;
        }
    }

    // Whatever is in the hand right now becomes the thing to come back to.
    //
    // Guarded by the same two checks that guard copying to all seven, because
    // this is the more consequential of the two writes -- a bad copy is undone
    // by the button next to it, and a bad baseline poisons the undo itself
    public static string RememberKnownGood(Plateau source, Transform sourceRoot)
    {
        if (source == null || sourceRoot == null)
            return "Plateau yok\n";

        if (!ReadPlacement(source, sourceRoot, out Placement placement))
            return "Plateau modelin disinda -- once elin kemigine bagla\n";

        string doubt = Suspicious(source, sourceRoot);

        if (doubt != null && !EditorUtility.DisplayDialog("Ayar supheli",
                doubt + "\n\nYine de \"bilinen iyi\" olarak kaydedilsin mi?",
                "Kaydet", "Vazgec"))
            return "Vazgecildi. Bilinen iyi ayar degismedi.\n";

        System.IO.File.WriteAllText(knownGoodFile, JsonUtility.ToJson(placement, true));

        AssetDatabase.Refresh();

        return "Bilinen iyi ayar guncellendi.\n" +
               "  kemik " + (placement.bonePath.Length <= 0 ? "(model koku)" : placement.bonePath) + "\n" +
               "  tepsi " + placement.trayPosition.ToString("0.0000") +
               "  olcek " + placement.trayScale.ToString("0.0000") + "\n" +
               "  yigin " + placement.stackPosition.ToString("0.0000") + "\n" +
               "  slot  " + placement.slotPosition.ToString("0.0000") + "\n" +
               "  dosya " + knownGoodFile + "\n";
    }

    // The placement the rabbits carried while the food last sat correctly in
    // their hands, read straight off Customer_Rabbit_Bald.prefab at the time
    private static Placement Factory => new Placement
    {
        bonePath = "CharacterArmature/Root/Body/Hips/Abdomen/Torso/Shoulder.R/UpperArm.R",

        trayPosition = new Vector3(-.0106f, .0128f, -.0194f),
        trayRotation = new Quaternion(-.734207f, -.17725316f, -.56558174f, .3311175f),
        trayScale = new Vector3(.02f, .02f, .02f),

        stackPosition = new Vector3(0f, .688382f, 0f),
        stackRotation = Quaternion.identity,
        stackScale = Vector3.one,

        slotPosition = new Vector3(1.304f, .683f, -.036f),
        slotRotation = Quaternion.identity,
        slotScale = Vector3.one,
    };

    public static string RestoreKnownGoodCustomers()
    {
        return "Bilinen iyi ayara donuluyor.\n\n" + CopyPlacementToCustomers(KnownGoodCustomer);
    }

    // FoodPosition.Push hands every food the slot's own rotation, so the slot is
    // shared ground: Cup and Pizza are both authored to read correctly at zero
    // rotation, and game-guide states the rule outright -- "Prefab renderer
    // localPosition must be zero". Turning a slot to suit one food turns it for
    // all of them, which is how fixing the salad broke the pizza.
    //
    // Only rotations are touched. The slot POSITIONS are hand tuning that took
    // a long time to get right and mean nothing to this rule
    [MenuItem("Cooked Fast/Arac/Fix Food Slot Convention")]
    public static void FixFoodSlotConvention()
    {
        string report = "YEMEK PREFABLARI (Renderer konum sifir, rotasyon sifir)\n" +
                        FixFoodPrefabs() +
                        "\nSLOT ROTASYONLARI (konumlara dokunulmadi)\n" +
                        FixSlotRotations();

        AssetDatabase.SaveAssets();

        Debug.Log("Yemek slotu kurali:\n" + report);
        EditorUtility.DisplayDialog("Yemek Slotu Kurali", report, "Tamam");
    }

    private static string FixFoodPrefabs()
    {
        string report = "";

        foreach (string guid in AssetDatabase.FindAssets(
                     "t:Prefab", new[] { "Assets/Tiny Coffee Shop/Prefabs/GamePlay" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (asset == null || asset.GetComponent<SpawnableFood>() == null)
                continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);

                if (filter == null)
                {
                    report += "- " + name + ": mesh yok\n";
                    continue;
                }

                Transform model = filter.transform;

                bool offset = model.localPosition.sqrMagnitude > .000001f;
                bool turned = Quaternion.Angle(model.localRotation, Quaternion.identity) > .01f;

                if (!offset && !turned)
                {
                    report += "- " + name + ": zaten kurala uygun\n";
                    continue;
                }

                report += "- " + name + ": konum " + model.localPosition.ToString("0.000") +
                          " rot " + model.localRotation.eulerAngles.ToString("0.0") + " -> sifirlandi\n";

                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return report;
    }

    private static string FixSlotRotations()
    {
        string report = "";

        PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (PlayerController controller in controllers)
        {
            Plateau plateau = FindPlateau(controller.transform);

            if (plateau == null)
                continue;

            report += "- Player: " + ZeroSlotRotation(plateau, true) + "\n";
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        // Station and table trays live in the scene too, and the same rule holds
        foreach (FoodSpawnerStation station in Object.FindObjectsByType<FoodSpawnerStation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Plateau plateau = station.GetComponentInChildren<Plateau>(true);

            if (plateau == null)
                continue;

            report += "- " + station.name + ": " + ZeroSlotRotation(plateau, true) + "\n";
            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
        }

        PrefabStage openStage = PrefabStageUtility.GetCurrentPrefabStage();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (openStage != null && openStage.assetPath == path)
            {
                report += "- " + name + ": Prefab Mode'da acik, atlandi\n";
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Plateau plateau = FindPlateau(root.transform);

                if (plateau == null)
                {
                    report += "- " + name + ": Plateau yok\n";
                    continue;
                }

                string line = ZeroSlotRotation(plateau, false);

                report += "- " + name + ": " + line + "\n";

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return report;
    }

    private static string ZeroSlotRotation(Plateau plateau, bool undo)
    {
        Transform slot = FirstSlot(plateau);
        Transform stack = slot == null ? null : slot.parent;

        if (stack == null)
            return "slot bulunamadi";

        string line = "";

        if (Quaternion.Angle(stack.localRotation, Quaternion.identity) > .01f)
        {
            if (undo)
                Undo.RecordObject(stack, "Slot rotation");

            line += "yigin " + stack.localRotation.eulerAngles.ToString("0.0") + " -> sifir  ";
            stack.localRotation = Quaternion.identity;
        }

        foreach (FoodPosition each in plateau.GetComponentsInChildren<FoodPosition>(true))
        {
            if (Quaternion.Angle(each.transform.localRotation, Quaternion.identity) <= .01f)
                continue;

            if (undo)
                Undo.RecordObject(each.transform, "Slot rotation");

            line += each.name + " " + each.transform.localRotation.eulerAngles.ToString("0.0") + " -> sifir  ";
            each.transform.localRotation = Quaternion.identity;
        }

        return line.Length <= 0 ? "zaten sifir" : line;
    }

    // The plate's own mesh node lives in the shared Plateau prefab, so it is not
    // a per-character setting. An instance that has overridden it stops hearing
    // about changes to the prefab and drifts away from every other tray in the
    // game -- reverting is how one gets back in line
    public static string RevertPlateVisualOnCustomers()
    {
        string report = "";

        PrefabStage openStage = PrefabStageUtility.GetCurrentPrefabStage();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (openStage != null && openStage.assetPath == path)
            {
                report += "- " + name + ": Prefab Mode'da acik, atlandi\n";
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Plateau plateau = FindPlateau(root.transform);
                MeshFilter filter = plateau == null
                    ? null
                    : plateau.GetComponentInChildren<MeshFilter>(true);

                if (filter == null)
                {
                    report += "- " + name + ": tabak mesh'i yok\n";
                    continue;
                }

                if (!PrefabUtility.IsPartOfPrefabInstance(filter.transform))
                {
                    report += "- " + name + ": override yok\n";
                    continue;
                }

                PrefabUtility.RevertObjectOverride(filter.transform, InteractionMode.AutomatedAction);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                report += "- " + name + ": temizlendi\n";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();

        return "Tabak override temizligi:\n" + report;
    }

    // There is deliberately no player-to-customer copy. The player is a panda and
    // the customers are rabbits: different proportions, different arm lengths,
    // and numbers that seat a pizza in one hand seat it in mid air on the other.
    // Copying only ever runs rabbit to rabbit, where the rig really is shared

    // For fixing one rabbit by hand and letting the other six follow. Reads
    // whatever is open in Prefab Mode, or whatever is selected -- a prefab in the
    // Project window and a customer in the scene both work
    [MenuItem("Cooked Fast/Arac/Copy Selected Customer Plateau To Others")]
    public static void CopySelectedCustomerPlateau()
    {
        GameObject source = FindSelectedCustomer(out string origin);

        if (source == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Kaynak yok.\n\nYa bir musteri prefabini Prefab Mode'da ac,\n" +
                "ya da Project/Hierarchy'den Plateau iceren bir musteri sec.",
                "Tamam");
            return;
        }

        Plateau plateau = FindPlateau(source.transform);
        string report = origin + "\n\n" + CopyPlacementFrom(plateau, source.transform);

        Debug.Log("Musteri -> musteriler:\n" + report);
        EditorUtility.DisplayDialog("Plateau Kopyala", report, "Tamam");
    }

    private static GameObject FindSelectedCustomer(out string origin)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage != null && FindPlateau(stage.prefabContentsRoot.transform) != null)
        {
            origin = "Kaynak (Prefab Mode): " + System.IO.Path.GetFileNameWithoutExtension(stage.assetPath);
            return stage.prefabContentsRoot;
        }

        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            origin = null;
            return null;
        }

        // Whatever is under the mouse when something looks wrong is the thing
        // that gets clicked, and that is usually the food slot or the tray, not
        // the customer. Walk out to the character rather than refusing
        Plateau plateau = selected.GetComponentInParent<Plateau>(true) ?? FindPlateau(selected.transform);

        if (plateau == null)
        {
            origin = null;
            return null;
        }

        Transform root = RootOf(plateau.transform);

        origin = "Kaynak (secili): " + root.name +
                 (EditorApplication.isPlaying ? "  [Play Mode -- prefablara yaziliyor]" : "");

        return root.gameObject;
    }

    // The character the tray belongs to. Found by the component that makes
    // something a character, not by climbing to the outermost ancestor with an
    // Animator somewhere beneath it -- that walked straight past the customer
    // and landed on Customer Manager, which contains every customer's animator
    public static Transform RootOf(Transform plateau)
    {
        for (Transform step = plateau; step != null; step = step.parent)
        {
            if (step.GetComponent<Customer>() != null ||
                step.GetComponent<PlayerController>() != null ||
                step.GetComponent<CustomerAnimator>() != null ||
                step.GetComponent<PlayerAnimator>() != null)
                return step;
        }

        // Nothing identifiable: the nearest ancestor holding an Animator itself,
        // which is the model root at worst rather than a manager
        for (Transform step = plateau; step != null; step = step.parent)
        {
            if (step.GetComponent<Animator>() != null)
                return step;
        }

        // A station tray: no character anywhere above it, no rig, no bones. Its
        // root is whatever it hangs off -- the zone. Answering with the tray
        // itself made the tray its own model, and then every question asked
        // relative to that model ("which bone is this parented to?") had no
        // answer, so saving a station tray's placement always refused
        return plateau.parent != null ? plateau.parent : plateau;
    }

    // Three transforms decide where a pizza ends up, and copying only some of
    // them produces a customer that is right in one axis and wrong in another.
    // They travel together
    [System.Serializable]
    public struct Placement
    {
        public string bonePath;

        public Vector3 trayPosition;
        public Quaternion trayRotation;
        public Vector3 trayScale;

        public Vector3 stackPosition;
        public Quaternion stackRotation;
        public Vector3 stackScale;

        public Vector3 slotPosition;
        public Quaternion slotRotation;
        public Vector3 slotScale;
    }

    // Null bone path means the tray is not inside the model, which no amount of
    // copying can make sense of
    public static bool ReadPlacement(Plateau source, Transform sourceRoot, out Placement placement)
    {
        placement = default;

        if (source == null || sourceRoot == null)
            return false;

        string bonePath = RelativePath(source.transform.parent, FindVisual(sourceRoot));

        if (bonePath == null)
            return false;

        Transform slot = FirstSlot(source);
        Transform stack = slot == null ? null : slot.parent;

        placement = new Placement
        {
            bonePath = bonePath,
            trayPosition = source.transform.localPosition,
            trayRotation = source.transform.localRotation,
            trayScale = source.transform.localScale,
            stackPosition = stack == null ? Vector3.zero : stack.localPosition,
            stackRotation = stack == null ? Quaternion.identity : stack.localRotation,
            stackScale = stack == null ? Vector3.one : stack.localScale,
            slotPosition = slot == null ? Vector3.zero : slot.localPosition,
            slotRotation = slot == null ? Quaternion.identity : slot.localRotation,
            slotScale = slot == null ? Vector3.one : slot.localScale,
        };

        return true;
    }

    // Plateau reads this same first slot at Awake and stacks from it, so it is
    // the one whose numbers matter
    public static Transform FirstSlot(Plateau plateau)
    {
        FoodPosition[] slots = plateau.GetComponentsInChildren<FoodPosition>(true);

        return slots.Length <= 0 ? null : slots[0].transform;
    }

    // Takes the whole placement off one character and stamps it onto every
    // customer variant
    public static string CopyPlacementFrom(Plateau source, Transform sourceRoot)
    {
        if (source == null || sourceRoot == null)
            return "Kaynak plateau yok\n";

        if (!ReadPlacement(source, sourceRoot, out Placement placement))
            return "Kaynak plateau modelin disinda: " +
                   (source == null || source.transform.parent == null ? "kok" : source.transform.parent.name) + "\n";

        string doubt = Suspicious(source, sourceRoot);

        // Copying is blind by design -- whatever the source says lands on all
        // seven. That is the point when the source is right and a disaster when
        // it is not, and the difference is measurable before anything is written
        if (doubt != null && !EditorUtility.DisplayDialog("Kaynak supheli",
                doubt + "\n\nYine de 7 tavsana yazilsin mi?", "Yaz", "Vazgec"))
            return "Vazgecildi. Kaynak duzeltilmeden kopyalama yapilmadi.\n";

        return CopyPlacementToCustomers(placement);
    }

    // The tray hangs off a hand. Sitting further from that hand than the
    // character is tall means it is not in the hand at all, and stamping that
    // onto every customer is how a queue ends up holding nothing
    private static string Suspicious(Plateau source, Transform sourceRoot)
    {
        Transform bone = source.transform.parent;

        if (bone == null)
            return "Tepsinin ebeveyni yok.";

        float height = CharacterHeight(sourceRoot);

        if (height < .0001f)
            return null;

        float reach = Vector3.Distance(source.transform.position, bone.position);

        if (reach > height * .5f)
            return "Tepsi bagli oldugu kemikten " + reach.ToString("0.00") + " birim uzakta.\n" +
                   "Karakterin boyu " + height.ToString("0.00") + " birim -- tepsi elinde degil.\n" +
                   "local pos " + source.transform.localPosition.ToString("0.000");

        // Distance alone missed a tray sitting right on the bone at four times
        // the size it should be, which copies just as badly
        Renderer plate = source.GetComponentInChildren<Renderer>(true);

        if (plate == null)
            return null;

        float width = Mathf.Max(plate.bounds.size.x, plate.bounds.size.z);

        if (width < height)
            return null;

        return "Tabak " + width.ToString("0.00") + " birim genis, karakterin boyu " +
               height.ToString("0.00") + " birim -- tepsi karakterden buyuk.\n" +
               "local scale " + source.transform.localScale.ToString("0.000");
    }

    private static float CharacterHeight(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        bool any = false;
        Bounds bounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (renderer.GetComponentInParent<Plateau>() != null)
                continue;

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return any ? bounds.size.y : 0f;
    }

    public static void ApplyPlacement(Plateau plateau, Transform bone, Placement placement)
    {
        plateau.transform.SetParent(bone, false);
        plateau.transform.localPosition = placement.trayPosition;
        plateau.transform.localRotation = placement.trayRotation;
        plateau.transform.localScale = placement.trayScale;

        Transform slot = FirstSlot(plateau);
        Transform stack = slot == null ? null : slot.parent;

        if (stack != null)
        {
            stack.localPosition = placement.stackPosition;
            stack.localRotation = placement.stackRotation;
            stack.localScale = placement.stackScale;
        }

        // Same rig, same tray, so the same numbers put the pizza in the same
        // place on their hand as on the one tuned by hand. Every slot, because
        // the extra ones the stack grows at runtime start from these
        foreach (FoodPosition each in plateau.GetComponentsInChildren<FoodPosition>(true))
        {
            each.transform.localPosition = placement.slotPosition;
            each.transform.localRotation = placement.slotRotation;
            each.transform.localScale = placement.slotScale;
        }
    }

    public static string CopyPlacementToCustomers(Placement placement)
    {
        string bonePath = placement.bonePath;

        string report = "Kaynak kemik: " + (bonePath.Length <= 0 ? "(model koku)" : bonePath) + "\n" +
                        "tepsi " + placement.trayPosition.ToString("0.0000") +
                        "  olcek " + placement.trayScale.ToString("0.0000") + "\n" +
                        "yigin  " + placement.stackPosition.ToString("0.0000") + "\n" +
                        "slot   " + placement.slotPosition.ToString("0.0000") + "\n\n";

        PrefabStage openStage = PrefabStageUtility.GetCurrentPrefabStage();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            // Writing the asset behind the back of an open Prefab Mode leaves the
            // two disagreeing, and the stage wins the moment it is saved
            if (openStage != null && openStage.assetPath == path)
            {
                report += "- " + name + ": Prefab Mode'da acik, atlandi\n";
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Plateau plateau = FindPlateau(root.transform);

                if (plateau == null)
                {
                    report += "- " + name + ": Plateau yok\n";
                    continue;
                }

                Transform visual = FindVisual(root.transform);
                Transform bone = ResolveBone(visual, bonePath);

                if (bone == null)
                {
                    report += "- " + name + ": kemik bulunamadi\n";
                    continue;
                }

                ApplyPlacement(plateau, bone, placement);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                report += "- " + name + ": " + bone.name + "\n";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();

        return report;
    }

    // Path first, because two bones in a rig can share a name. Falls back to a
    // plain name search so a rig that differs slightly still resolves
    public static Transform ResolveBone(Transform visual, string bonePath)
    {
        if (bonePath.Length <= 0)
            return visual;

        Transform found = visual.Find(bonePath);

        if (found != null)
            return found;

        int lastSlash = bonePath.LastIndexOf('/');
        string boneName = lastSlash >= 0 ? bonePath.Substring(lastSlash + 1) : bonePath;

        foreach (Transform candidate in visual.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name == boneName)
                return candidate;
        }

        return null;
    }

    // Null when the transform is not under root at all, which is a real answer:
    // it means the tray is not inside the character model
    public static string RelativePath(Transform transform, Transform root)
    {
        if (transform == null)
            return null;

        if (transform == root)
            return "";

        string path = "";

        while (transform != null && transform != root)
        {
            path = path.Length <= 0 ? transform.name : transform.name + "/" + path;
            transform = transform.parent;
        }

        return transform == root ? path : null;
    }

    // A character keeps the model it used to wear parked next to the one it
    // wears now, switched off -- and the retired model can still own a tray of
    // its own. Taking the first hit in hierarchy order picks that one, so every
    // edit lands on a tray nobody will ever see
    public static Plateau FindPlateau(Transform root)
    {
        Plateau[] found = root.GetComponentsInChildren<Plateau>(true);

        if (found.Length <= 0)
            return null;

        if (found.Length == 1)
            return found[0];

        Transform visual = FindVisual(root);

        foreach (Plateau candidate in found)
        {
            if (candidate.transform.IsChildOf(visual) && candidate.gameObject.activeInHierarchy)
                return candidate;
        }

        foreach (Plateau candidate in found)
        {
            if (candidate.gameObject.activeInHierarchy)
                return candidate;
        }

        foreach (Plateau candidate in found)
        {
            if (candidate.transform.IsChildOf(visual))
                return candidate;
        }

        return found[0];
    }

    public static int CountPlateaus(Transform root)
    {
        return root.GetComponentsInChildren<Plateau>(true).Length;
    }

    // Asks the animator component which body is live rather than guessing from
    // the hierarchy, which still holds every model the character has ever worn
    public static Transform FindVisual(Transform root)
    {
        Animator animator = ReadAnimatorField(root, "PlayerAnimator")
                            ?? ReadAnimatorField(root, "CustomerAnimator");

        if (animator != null)
            return animator.transform;

        foreach (Animator candidate in root.GetComponentsInChildren<Animator>(true))
        {
            if (candidate.gameObject.activeInHierarchy)
                return candidate.transform;
        }

        return root;
    }

    private static Animator ReadAnimatorField(Transform root, string componentName)
    {
        foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null || behaviour.GetType().Name != componentName)
                continue;

            SerializedProperty property = new SerializedObject(behaviour).FindProperty("animator");

            if (property != null)
                return property.objectReferenceValue as Animator;
        }

        return null;
    }

    // Highest score wins, and the SHALLOWEST bone breaks a tie. Depth used to
    // break it the other way, which walked the tray all the way out to
    // RightHandIndex3_end_end_end -- the tip of the index finger
    public static Transform FindHand(Transform root, Transform plateau)
    {
        Transform best = null;
        int bestScore = 0;
        int bestDepth = int.MaxValue;

        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            // The tray's own children would otherwise be fair game, and nesting
            // it inside itself is not a parent Unity accepts
            if (candidate == plateau || candidate.IsChildOf(plateau))
                continue;

            // A retired character model is left in the scene switched off. Its
            // bones still carry the tidiest names in the whole hierarchy, so
            // without this the tray moves into a body nobody can see
            if (!IsActiveUnder(candidate, root))
                continue;

            int score = Score(candidate);

            if (score <= 0)
                continue;

            int depth = Depth(candidate, root);

            if (score < bestScore || (score == bestScore && depth >= bestDepth))
                continue;

            best = candidate;
            bestScore = score;
            bestDepth = depth;
        }

        return best;
    }

    // activeInHierarchy answers for the whole scene, which is wrong inside
    // prefab contents that live in a scene of their own
    private static bool IsActiveUnder(Transform candidate, Transform root)
    {
        while (candidate != null)
        {
            if (!candidate.gameObject.activeSelf)
                return false;

            if (candidate == root)
                return true;

            candidate = candidate.parent;
        }

        return true;
    }

    private static int Score(Transform candidate)
    {
        string lower = candidate.name.ToLowerInvariant();

        // The armature root itself usually carries the rig name, which would
        // otherwise beat every real bone on an "arm" match
        if (lower.Contains("armature"))
            return 0;

        // Structure over naming. This pack's panda has no bone called "hand" at
        // all -- its chain runs Shoulder.R, UpperArm.R, LowerArm.R and then
        // straight into Index1.R and friends. Whatever the fingers hang off IS
        // the palm, whatever it happens to be called
        int score = FingerChildCount(candidate) >= 2 ? 200 : NameScore(lower);

        if (score <= 0)
            return 0;

        if (ContainsAny(lower, fingerKeys))
            score -= 40;

        // Exporters pad every chain with _end bones that carry no animation
        if (lower.Contains("_end") || lower.EndsWith("end"))
            score -= 20;

        return IsRightSide(lower) ? score + 10 : score;
    }

    private static int NameScore(string lower)
    {
        if (ContainsAny(lower, handKeys))
            return 100;

        // The far end of the arm is where a hand would be, so it beats the near
        // end. Scored rather than resolved by depth, which picked the shoulder
        if (lower.Contains("forearm") || lower.Contains("lowerarm") || lower.Contains("lower_arm"))
            return 60;

        if (lower.Contains("upperarm") || lower.Contains("upper_arm"))
            return 20;

        return ContainsAny(lower, armKeys) ? 50 : 0;
    }

    private static int FingerChildCount(Transform candidate)
    {
        int count = 0;

        foreach (Transform child in candidate)
        {
            if (ContainsAny(child.name.ToLowerInvariant(), fingerKeys))
                count++;
        }

        return count;
    }

    private static bool ContainsAny(string lower, string[] keys)
    {
        foreach (string key in keys)
        {
            if (lower.Contains(key))
                return true;
        }

        return false;
    }

    // Characters carry the tray in the right hand in every clip in the pack
    private static bool IsRightSide(string lower)
    {
        return lower.Contains("right")
            || lower.EndsWith(".r")
            || lower.EndsWith("_r")
            || lower.EndsWith("-r")
            || lower.EndsWith(" r");
    }

    private static int Depth(Transform transform, Transform root)
    {
        int depth = 0;

        while (transform != root && transform.parent != null)
        {
            transform = transform.parent;
            depth++;
        }

        return depth;
    }

    // Printed only when nothing matched, so the rig can be read and the keys
    // above widened rather than guessed at
    private static string DumpBones(Transform root)
    {
        List<string> names = new List<string>();

        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            names.Add(Depth(bone, root) + " " + bone.name);

        Debug.Log("Kemikler:\n" + string.Join("\n", names));

        return "";
    }
}
