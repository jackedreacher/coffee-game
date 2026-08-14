using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Every number that decides where the tray, the plate and the food end up, in
// one panel, editable. Nothing here works anything out on its own: no bone is
// guessed, no offset is fitted, no placement is reset. The tool's whole job is
// to surface the values and write them where they need to go
public class PlateauHandWindow : EditorWindow
{
    // The tray is a scene object on the player, seven prefabs on the customers,
    // and a throwaway clone once the game is running. "Secili obje" covers the
    // last one, which is the only way to tune against a real spawned customer
    private static readonly string[] targets = { "Player", "SAMPLE Customer", "Secili obje" };
    private int targetIndex;

    private Transform[] bones = new Transform[0];
    private string[] boneLabels = new string[0];
    private int boneIndex = -1;
    private string boneFilter = "";
    private bool keepLookOnBoneChange = true;

    private Vector3 trayPosition;
    private Vector3 trayEuler;
    private Vector3 trayScale = Vector3.one;

    private Vector3 stackPosition;
    private Vector3 stackEuler;
    private Vector3 stackScale = Vector3.one;

    private Vector3 foodPosition;
    private Vector3 foodEuler;
    private Vector3 foodScale = Vector3.one;

    private Vector3 platePosition;
    private Vector3 plateEuler;
    private Vector3 plateScale = Vector3.one;

    // Uniform by default: these are objects whose proportions are part of the
    // model, not something worth squashing by hand
    private bool trayScaleLocked = true;
    private bool stackScaleLocked = true;
    private bool foodScaleLocked = true;
    private bool plateScaleLocked = true;

    // Read off whichever controller the target is running. A fixed list held the
    // panda's names -- Idle_Holding and friends -- while the rabbits ship
    // IdleWithPlateau and WalkWithPlateau, so posing a customer silently did
    // nothing and there was no way to see the tray in the clip it goes wrong in
    private string[] poses = new string[0];
    private int poseIndex;
    private bool posing;

    private bool live = true;
    private Vector2 scroll;
    private string status;

    [MenuItem("Cooked Fast/Plateau Hand Adjuster")]
    public static void Open()
    {
        PlateauHandWindow window = GetWindow<PlateauHandWindow>(true, "Plateau Ayari");
        window.minSize = new Vector2(360f, 520f);
        window.Reload();
    }

    private void OnDisable()
    {
        StopPose();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawTarget();

        Plateau plateau = FindPlateau();

        if (plateau == null)
        {
            EditorGUILayout.HelpBox(TargetHelp(), MessageType.Warning);

            if (targetIndex == 1 && GUILayout.Button("Ornek musteri ekle", GUILayout.Height(26f)))
            {
                CharacterSetup.AddSampleCustomer();
                Reload();
            }
        }
        else
        {
            DrawPose();
            DrawBone(plateau);
            DrawTransforms();
            DrawSaving(plateau);
        }

        // Drawn whichever character is selected, and even when none resolves:
        // the food is a shared prefab, nothing about it belongs to a character
        DrawFood();

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawTarget()
    {
        int picked = EditorGUILayout.Popup("Hedef", targetIndex, targets);

        if (picked != targetIndex)
        {
            StopPose();
            targetIndex = picked;
            Reload();
        }

        if (targetIndex == 2)
        {
            EditorGUILayout.LabelField("Secili", Selection.activeGameObject == null
                ? "yok"
                : Selection.activeGameObject.name);
        }

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode. Sahnedeki degisiklikler durdurunca silinir --\n" +
                "'Tum tavsanlara yaz' ile prefaba gecir.",
                MessageType.Warning);
        }

        live = EditorGUILayout.ToggleLeft("Canli uygula", live);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Yeniden oku"))
                Reload();

            if (!live && GUILayout.Button("Uygula"))
                ApplyAll();
        }
    }

    private string TargetHelp()
    {
        if (targetIndex == 2)
            return "Hierarchy'den Plateau iceren bir obje sec.\n" +
                   "Plateau'nun kendisi, Food Positions ya da karakterin koku -- hepsi olur.";

        return targets[targetIndex] + " plateau'su bulunamadi";
    }

    private void DrawPose()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Poz", EditorStyles.boldLabel);

        if (poses.Length <= 0)
        {
            EditorGUILayout.HelpBox("Bu karakterin controller'inda klip bulunamadi", MessageType.None);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int picked = EditorGUILayout.Popup(poseIndex, poses);

            if (picked != poseIndex)
            {
                poseIndex = picked;

                if (posing)
                    SamplePose();
            }

            if (GUILayout.Button(posing ? "Pozu birak" : "Pozu goster", GUILayout.Width(100f)))
            {
                if (posing)
                    StopPose();
                else
                    SamplePose();
            }
        }

        if (!posing && !EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Bind pose'da ayar yapma -- kollar asagida. Pozu ac.",
                MessageType.None);
        }
    }

    // A dropdown of every bone in the rig, because a rig with no bone called
    // "hand" defeats any amount of name matching and the person looking at the
    // model already knows which one they want
    private void DrawBone(Plateau plateau)
    {
        EditorGUILayout.Space();

        // Which one is being edited, spelled out. A character can own two trays
        // and editing the invisible one looks exactly like editing nothing
        int count = PlateauAttach.CountPlateaus(FindRoot());

        if (count > 1)
        {
            EditorGUILayout.HelpBox(
                "Bu karakterde " + count + " Plateau var (eski model de tasiyor).\n" +
                "Duzenlenen: " + PathUnder(plateau.transform, FindRoot()),
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Duzenlenen", PathUnder(plateau.transform, FindRoot()));
        }

        EditorGUILayout.LabelField("Bagli kemik", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Su an",
            plateau.transform.parent == null ? "YOK" : plateau.transform.parent.name);

        string typed = EditorGUILayout.TextField("Ara", boneFilter);

        if (typed != boneFilter)
        {
            boneFilter = typed;
            RebuildBones(plateau);
        }

        if (bones.Length <= 0)
        {
            EditorGUILayout.HelpBox("Kemik listesi bos", MessageType.None);
            return;
        }

        keepLookOnBoneChange = EditorGUILayout.ToggleLeft(
            "Tasirken gorunumu koru", keepLookOnBoneChange);

        int picked = EditorGUILayout.Popup("Kemik", boneIndex, boneLabels);

        if (picked != boneIndex && picked >= 0 && picked < bones.Length)
        {
            boneIndex = picked;
            MoveToBone(plateau, bones[picked]);
        }
    }

    private void DrawTransforms()
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tepsi (Plateau)", EditorStyles.boldLabel);
        trayPosition = EditorGUILayout.Vector3Field("Konum", trayPosition);
        trayEuler = EditorGUILayout.Vector3Field("Rotasyon", trayEuler);
        trayScale = ScaleField("Olcek", trayScale, ref trayScaleLocked);

        // The parent of the slots. Moving this moves the whole stack at once,
        // which is the handle wanted when the food is in the right place
        // relative to itself but the wrong place relative to the plate
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Yemek yigini (Positions)", EditorStyles.boldLabel);
        stackPosition = EditorGUILayout.Vector3Field("Konum", stackPosition);
        stackEuler = EditorGUILayout.Vector3Field("Rotasyon", stackEuler);
        stackScale = ScaleField("Olcek", stackScale, ref stackScaleLocked);

        // Cancelling an arm bone's rotation by eye means finding three euler
        // angles that undo a quaternion nobody can see. The maths is one line
        if (GUILayout.Button("Yemegi diklestir (yigini dunyaya hizala)"))
            LevelStack();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Yemek slotu (Food Positions)", EditorStyles.boldLabel);
        foodPosition = EditorGUILayout.Vector3Field("Konum", foodPosition);
        foodEuler = EditorGUILayout.Vector3Field("Rotasyon", foodEuler);
        foodScale = ScaleField("Olcek", foodScale, ref foodScaleLocked);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tabak gorseli (Renderer)", EditorStyles.boldLabel);
        platePosition = EditorGUILayout.Vector3Field("Konum", platePosition);
        plateEuler = EditorGUILayout.Vector3Field("Rotasyon", plateEuler);
        plateScale = ScaleField("Olcek", plateScale, ref plateScaleLocked);

        if (EditorGUI.EndChangeCheck() && live)
            ApplyAll();
    }

    private static readonly GUIContent lockLabel = new GUIContent(
        "XYZ", "Acikken uc eksen birlikte degisir");

    // A plate wants to keep its proportions, and typing the same number into
    // three boxes leaves it stretched between the first edit and the last while
    // live apply is on
    private static Vector3 ScaleField(string label, Vector3 value, ref bool locked)
    {
        Vector3 typed;

        using (new EditorGUILayout.HorizontalScope())
        {
            typed = EditorGUILayout.Vector3Field(label, value);
            locked = GUILayout.Toggle(locked, lockLabel, EditorStyles.miniButton, GUILayout.Width(38f));
        }

        if (!locked || typed == value)
            return typed;

        // Whichever box was actually touched drives the other two. Falling
        // through to z covers the case where two read equal already
        float driver = !Mathf.Approximately(typed.x, value.x) ? typed.x
            : !Mathf.Approximately(typed.y, value.y) ? typed.y
            : typed.z;

        return new Vector3(driver, driver, driver);
    }

    private void DrawSaving(Plateau plateau)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Kaydet", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ayari kaydet"))
                SaveSnapshot();

            using (new EditorGUI.DisabledScope(!HasSnapshot()))
            {
                if (GUILayout.Button("Kayitli ayara don"))
                    RestoreSnapshot();
            }
        }

        // The plate visual lives in the shared prefab, so it is the one thing
        // here that is not per character. Writing it from an instance is the
        // only way to settle its size without opening the prefab by hand
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tabagi prefaba yaz"))
            {
                status = PlateauSetup.WriteVisual(
                    platePosition, Quaternion.Euler(plateEuler), plateScale);
            }

            if (GUILayout.Button("Tabagi prefabdan oku"))
            {
                if (PlateauSetup.ReadVisual(out Vector3 p, out Quaternion r, out Vector3 s))
                {
                    platePosition = p;
                    plateEuler = r.eulerAngles;
                    plateScale = s;

                    ApplyAll();
                    status = "Tabak prefabdan okundu";
                }
                else
                {
                    status = "Plateau prefabi okunamadi";
                }
            }
        }

        // An instance that overrides the plate stops following the shared prefab.
        // Two of those and no amount of editing the prefab makes every tray agree
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tabak override'ini temizle"))
                RevertPlateOverride(plateau);

            if (GUILayout.Button("Tavsanlarda temizle"))
            {
                string report = PlateauAttach.RevertPlateVisualOnCustomers();

                Debug.Log(report);
                status = report;
            }
        }

        // The panda's numbers on a rabbit put the pizza in mid air, so this only
        // ever runs from a rabbit outwards
        using (new EditorGUI.DisabledScope(targetIndex == 0))
        {
            if (GUILayout.Button("Tum tavsanlara yaz", GUILayout.Height(26f)))
                WriteToCustomers(plateau);
        }

        // The way back when a copy went wrong and the trays are somewhere off
        // the character. Independent of the snapshot buttons, which hold
        // whatever was saved last -- this holds a placement known to have worked
        if (GUILayout.Button("Tavsanlari bilinen iyi ayara dondur"))
        {
            if (EditorUtility.DisplayDialog("Bilinen Iyi Ayar",
                    "7 tavsanin tepsi ayari, yemegin ellerinde durdugu son bilinen\n" +
                    "degerlere donecek.\n\nSimdiki ayar gider.",
                    "Dondur", "Vazgec"))
            {
                string report = PlateauAttach.RestoreKnownGoodCustomers();

                Debug.Log(report);
                status = report;
            }
        }

        if (targetIndex == 0)
            EditorGUILayout.HelpBox("Player panda, musteriler tavsan. Ayarlari ayri.", MessageType.None);
    }

    private void RevertPlateOverride(Plateau plateau)
    {
        Transform plate = FindPlateVisual(plateau);

        if (plate == null)
        {
            status = "Tabak mesh dugumu bulunamadi";
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(plate))
        {
            status = "Bu tabak bir prefab kopyasi degil, temizlenecek override yok";
            return;
        }

        PrefabUtility.RevertObjectOverride(plate, InteractionMode.UserAction);

        ReadFromTarget();

        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(plateau.gameObject.scene);

        status = targets[targetIndex] + ": tabak override'i temizlendi, artik prefabi izliyor";
    }

    // ---- the food prefab ---------------------------------------------------

    private const string foodFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";

    private string[] foodPaths = new string[0];
    private string[] foodLabels = new string[0];
    private int foodIndex;

    private Vector3 foodModelPosition;
    private Vector3 foodModelEuler;
    private Vector3 foodModelScale = Vector3.one;
    private bool foodModelScaleLocked = true;

    private float cleanOffset;
    private float dirtyOffset;

    private void DrawFood()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Yemek prefabi", EditorStyles.boldLabel);

        if (foodPaths.Length <= 0)
            RebuildFoods();

        if (foodPaths.Length <= 0)
        {
            EditorGUILayout.HelpBox(foodFolder + " icinde SpawnableFood prefabi yok", MessageType.Warning);
            return;
        }

        int picked = EditorGUILayout.Popup("Yemek", foodIndex, foodLabels);

        if (picked != foodIndex)
        {
            foodIndex = picked;
            ReadFood();
        }

        EditorGUI.BeginChangeCheck();

        foodModelPosition = EditorGUILayout.Vector3Field("Model konum", foodModelPosition);
        foodModelEuler = EditorGUILayout.Vector3Field("Model rotasyon", foodModelEuler);
        foodModelScale = ScaleField("Model olcek", foodModelScale, ref foodModelScaleLocked);

        // How far the next item in the stack sits above this one. Wrong here and
        // two of the same food either float apart or grow through each other
        cleanOffset = EditorGUILayout.FloatField("Yigin araligi", cleanOffset);
        dirtyOffset = EditorGUILayout.FloatField("Kirli araligi", dirtyOffset);

        bool edited = EditorGUI.EndChangeCheck();

        Transform scene = FindSceneFoodModel();

        // The counterpart of the Scene view rotate tool. Everything above can be
        // dragged on the object itself with the gizmo, and without a way back in
        // those drags are overwritten by whatever the fields last held
        using (new EditorGUI.DisabledScope(scene == null))
        {
            if (GUILayout.Button(scene == null
                    ? "Sahnedekinden oku (once bir yemek sec)"
                    : "Sahnedekinden oku: " + scene.parent.name))
                ReadFoodFromScene();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Prefaba yaz"))
                WriteFood();

            if (GUILayout.Button("Prefabdan oku"))
                ReadFood();
        }

        // Play mode clones are the only place a salad is ever seen at its real
        // size on a real tray, so previewing has to reach them too
        if (edited || GUILayout.Button("Sahnedekilere uygula"))
            ApplyFoodLive();
    }

    private void RebuildFoods()
    {
        List<string> paths = new List<string>();
        List<string> labels = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { foodFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null || prefab.GetComponent<SpawnableFood>() == null)
                continue;

            paths.Add(path);
            labels.Add(System.IO.Path.GetFileNameWithoutExtension(path));
        }

        foodPaths = paths.ToArray();
        foodLabels = labels.ToArray();
        foodIndex = Mathf.Clamp(foodIndex, 0, Mathf.Max(0, foodPaths.Length - 1));

        if (foodPaths.Length > 0)
            ReadFood();
    }

    private static Transform FindFoodModel(GameObject root)
    {
        MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);

        return filter == null ? null : filter.transform;
    }

    // The mesh node of whatever food is selected in the scene, reached from the
    // food root, the model node, or anything under either
    private Transform FindSceneFoodModel()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
            return null;

        SpawnableFood food = selected.GetComponentInParent<SpawnableFood>();

        return food == null ? null : FindFoodModel(food.gameObject);
    }

    private void ReadFoodFromScene()
    {
        Transform model = FindSceneFoodModel();

        if (model == null)
        {
            status = "Sahnede secili bir yemek yok";
            return;
        }

        // Follow the selection rather than write a salad's numbers into whatever
        // the dropdown happened to be showing
        SpawnableFood food = model.GetComponentInParent<SpawnableFood>();
        int match = System.Array.IndexOf(foodLabels, food.GetType().Name);

        if (match >= 0)
            foodIndex = match;

        foodModelPosition = model.localPosition;
        foodModelEuler = model.localRotation.eulerAngles;
        foodModelScale = model.localScale;

        status = food.name + " sahneden okundu. 'Prefaba yaz' ile kalici yap.";
    }

    private void ReadFood()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(foodPaths[foodIndex]);

        if (prefab == null)
            return;

        Transform model = FindFoodModel(prefab);

        if (model != null)
        {
            foodModelPosition = model.localPosition;
            foodModelEuler = model.localRotation.eulerAngles;
            foodModelScale = model.localScale;
        }

        SerializedObject so = new SerializedObject(prefab.GetComponent<SpawnableFood>());

        cleanOffset = so.FindProperty("cleanYOffsetOnPlateau").floatValue;
        dirtyOffset = so.FindProperty("dirtyYOffsetOnPlateau").floatValue;

        status = foodLabels[foodIndex] + " okundu";
    }

    // Instances only. The prefab asset is written by the button, so a preview
    // that looked wrong can be walked away from without having changed anything
    private void ApplyFoodLive()
    {
        System.Type wanted = SelectedFoodType();

        if (wanted == null)
        {
            status = "Prefab okunamadi";
            return;
        }

        int touched = 0;

        foreach (SpawnableFood food in Object.FindObjectsByType<SpawnableFood>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (food.GetType() != wanted)
                continue;

            Transform model = FindFoodModel(food.gameObject);

            if (model == null)
                continue;

            Undo.RecordObject(model, "Yemek ayari");

            model.localPosition = foodModelPosition;
            model.localRotation = Quaternion.Euler(foodModelEuler);
            model.localScale = foodModelScale;

            touched++;
        }

        status = foodLabels[foodIndex] + ": sahnede " + touched + " kopya guncellendi";
    }

    private System.Type SelectedFoodType()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(foodPaths[foodIndex]);
        SpawnableFood food = prefab == null ? null : prefab.GetComponent<SpawnableFood>();

        return food == null ? null : food.GetType();
    }

    // Every step here has a way of failing that leaves the asset untouched and
    // says nothing: the prefab open in Prefab Mode, a missing component, a save
    // that returns false. Each one is checked, and the values are read back off
    // disk afterwards so "written" means the file actually says so
    private void WriteFood()
    {
        string path = foodPaths[foodIndex];

        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage != null && stage.assetPath == path)
        {
            status = foodLabels[foodIndex] + " Prefab Mode'da acik.\n" +
                     "Once kapat -- acik sahnenin arkasindan yazmak ikisini celiskiye dusurur.";
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
        {
            status = "Prefab acilamadi: " + path;
            return;
        }

        bool saved;
        string wroteTo;

        try
        {
            Transform model = FindFoodModel(root);

            if (model == null)
            {
                status = foodLabels[foodIndex] + " icinde MeshFilter yok, yazilacak dugum bulunamadi";
                return;
            }

            model.localPosition = foodModelPosition;
            model.localRotation = Quaternion.Euler(foodModelEuler);
            model.localScale = foodModelScale;

            wroteTo = model.name;

            SpawnableFood food = root.GetComponent<SpawnableFood>();

            if (food == null)
            {
                status = foodLabels[foodIndex] + " kokunde SpawnableFood bileseni yok";
                return;
            }

            SerializedObject so = new SerializedObject(food);

            so.FindProperty("cleanYOffsetOnPlateau").floatValue = cleanOffset;
            so.FindProperty("dirtyYOffsetOnPlateau").floatValue = dirtyOffset;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path, out saved);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (!saved)
        {
            status = "KAYDEDILEMEDI: " + path + "\nDosya salt okunur olabilir.";
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);

        Vector3 wanted = foodModelScale;

        ReadFood();

        // Reading back is the only honest confirmation. A mismatch here means
        // something rewrote the asset between the save and the read
        status = (foodModelScale - wanted).sqrMagnitude < .000001f
            ? foodLabels[foodIndex] + " yazildi (" + wroteTo + ")\nolcek " + foodModelScale.ToString("0.0000")
            : "UYUSMUYOR -- yazilan " + wanted.ToString("0.0000") +
              ", dosyada " + foodModelScale.ToString("0.0000");
    }

    // ---- target resolution -------------------------------------------------

    private Transform FindRoot()
    {
        if (targetIndex == 2)
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null)
                return null;

            Plateau found = selected.GetComponentInParent<Plateau>(true)
                            ?? selected.GetComponentInChildren<Plateau>(true);

            return found == null ? null : PlateauAttach.RootOf(found.transform);
        }

        if (targetIndex == 1)
        {
            GameObject sample = GameObject.Find("SAMPLE Customer");

            return sample == null ? null : sample.transform;
        }

        PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        return controllers.Length <= 0 ? null : controllers[0].transform;
    }

    // Enough of the path to tell two trays on one character apart, without the
    // whole chain from the scene root
    private static string PathUnder(Transform transform, Transform root)
    {
        string path = transform.name;

        while (transform.parent != null && transform.parent != root)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private Plateau FindPlateau()
    {
        Transform root = FindRoot();

        return root == null ? null : PlateauAttach.FindPlateau(root);
    }

    private static Transform FindSlot(Plateau plateau)
    {
        FoodPosition[] slots = plateau.GetComponentsInChildren<FoodPosition>(true);

        return slots.Length <= 0 ? null : slots[0].transform;
    }

    // Whatever the slots hang off. Read through the slot rather than by name so
    // it still resolves after the stack has grown extra slots at runtime
    private static Transform FindStack(Plateau plateau)
    {
        Transform slot = FindSlot(plateau);

        return slot == null ? null : slot.parent;
    }

    private static Transform FindPlateVisual(Plateau plateau)
    {
        MeshFilter filter = PlateauSetup.FindTargetFilter(plateau.gameObject);

        return filter == null ? null : filter.transform;
    }

    // ---- reading and writing ----------------------------------------------

    private void Reload()
    {
        Plateau plateau = FindPlateau();

        if (plateau == null)
        {
            status = TargetHelp();
            bones = new Transform[0];
            boneLabels = new string[0];
            boneIndex = -1;
            return;
        }

        RebuildBones(plateau);
        RebuildPoses();
        ReadFromTarget();
        status = "Okundu";
    }

    // Holding clips first: they are the ones the tray is visible in, and the
    // ones the placement has to be right for
    private void RebuildPoses()
    {
        Animator animator = FindAnimator();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            poses = new string[0];
            return;
        }

        List<string> holding = new List<string>();
        List<string> rest = new List<string>();

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
                continue;

            string name = BareName(clip.name);

            if (holding.Contains(name) || rest.Contains(name))
                continue;

            string lower = name.ToLowerInvariant();

            if (lower.Contains("hold") || lower.Contains("plateau"))
                holding.Add(name);
            else
                rest.Add(name);
        }

        holding.AddRange(rest);

        poses = holding.ToArray();
        poseIndex = Mathf.Clamp(poseIndex, 0, Mathf.Max(0, poses.Length - 1));
    }

    private void RebuildBones(Plateau plateau)
    {
        Transform visual = PlateauAttach.FindVisual(FindRoot());

        List<Transform> found = new List<Transform>();
        List<string> labels = new List<string>();

        foreach (Transform candidate in visual.GetComponentsInChildren<Transform>(true))
        {
            if (candidate == plateau.transform || candidate.IsChildOf(plateau.transform))
                continue;

            if (boneFilter.Length > 0 &&
                candidate.name.IndexOf(boneFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            found.Add(candidate);
            labels.Add(candidate.name);
        }

        bones = found.ToArray();
        boneLabels = labels.ToArray();
        boneIndex = found.IndexOf(plateau.transform.parent);
    }

    private bool ReadFromTarget()
    {
        Plateau plateau = FindPlateau();

        if (plateau == null)
            return false;

        trayPosition = plateau.transform.localPosition;
        trayEuler = plateau.transform.localRotation.eulerAngles;
        trayScale = plateau.transform.localScale;

        Transform stack = FindStack(plateau);

        if (stack != null)
        {
            stackPosition = stack.localPosition;
            stackEuler = stack.localRotation.eulerAngles;
            stackScale = stack.localScale;
        }

        Transform slot = FindSlot(plateau);

        if (slot != null)
        {
            foodPosition = slot.localPosition;
            foodEuler = slot.localRotation.eulerAngles;
            foodScale = slot.localScale;
        }

        Transform plate = FindPlateVisual(plateau);

        if (plate != null)
        {
            platePosition = plate.localPosition;
            plateEuler = plate.localRotation.eulerAngles;
            plateScale = plate.localScale;
        }

        return true;
    }

    // FoodPosition.Push hands the food the slot's rotation, not its own, so the
    // food ends up wearing whatever the arm bone was doing. Turning the stack to
    // face world axes makes the food stand up wherever the hand happens to be
    private void LevelStack()
    {
        Plateau plateau = FindPlateau();
        Transform stack = plateau == null ? null : FindStack(plateau);

        if (stack == null || stack.parent == null)
        {
            status = "Yigin dugumu bulunamadi";
            return;
        }

        // world = parent.rotation * local, and the world rotation wanted is none
        Quaternion local = Quaternion.Inverse(stack.parent.rotation);

        stackEuler = local.eulerAngles;

        ApplyAll();

        status = "Yigin dunyaya hizalandi: rotasyon " + stackEuler.ToString("0.0") +
                 "\nYemek hala yatiksa 'Yemek slotu' rotasyonundan ince ayar yap.";
    }

    private void ApplyAll()
    {
        Plateau plateau = FindPlateau();

        if (plateau == null)
        {
            status = TargetHelp();
            return;
        }

        Undo.RecordObject(plateau.transform, "Plateau ayari");

        plateau.transform.localPosition = trayPosition;
        plateau.transform.localRotation = Quaternion.Euler(trayEuler);
        plateau.transform.localScale = trayScale;

        Transform stack = FindStack(plateau);

        if (stack != null)
        {
            Undo.RecordObject(stack, "Plateau ayari");
            stack.localPosition = stackPosition;
            stack.localRotation = Quaternion.Euler(stackEuler);
            stack.localScale = stackScale;
        }

        Transform slot = FindSlot(plateau);

        if (slot != null)
        {
            Undo.RecordObject(slot, "Plateau ayari");
            slot.localPosition = foodPosition;
            slot.localRotation = Quaternion.Euler(foodEuler);
            slot.localScale = foodScale;
        }

        Transform plate = FindPlateVisual(plateau);

        if (plate != null)
        {
            Undo.RecordObject(plate, "Plateau ayari");
            plate.localPosition = platePosition;
            plate.localRotation = Quaternion.Euler(plateEuler);
            plate.localScale = plateScale;
        }

        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(plateau.gameObject.scene);

        status = "Uygulandi";
    }

    // Reparenting keeps the world placement by default, so switching bone to
    // compare two of them does not throw away the offsets already dialled in
    private void MoveToBone(Plateau plateau, Transform bone)
    {
        string previous = plateau.transform.parent == null ? "YOK" : plateau.transform.parent.name;

        Undo.SetTransformParent(plateau.transform, bone, keepLookOnBoneChange, "Plateau kemik degistir");

        ReadFromTarget();

        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(plateau.gameObject.scene);

        status = previous + " -> " + bone.name;
    }

    private void WriteToCustomers(Plateau plateau)
    {
        ReadFromTarget();

        string report = PlateauAttach.CopyPlacementFrom(plateau, FindRoot());

        Debug.Log("Musteri plateau'lari:\n" + report);
        status = report;
    }

    // ---- snapshots ---------------------------------------------------------

    // Stored outside the scene on purpose: the point is to survive whatever
    // wrecks the scene copy, play mode included
    private string SnapshotKey => "CookedFast.Plateau." + targets[targetIndex];

    private bool HasSnapshot()
    {
        return EditorPrefs.HasKey(SnapshotKey);
    }

    private void SaveSnapshot()
    {
        Plateau plateau = FindPlateau();

        if (plateau == null)
        {
            status = TargetHelp();
            return;
        }

        if (!PlateauAttach.ReadPlacement(plateau, FindRoot(), out PlateauAttach.Placement placement))
        {
            status = "Plateau modelin disinda, kaydedilmedi";
            return;
        }

        EditorPrefs.SetString(SnapshotKey, JsonUtility.ToJson(placement));
        status = "Kaydedildi: " + (placement.bonePath.Length <= 0 ? "(model koku)" : placement.bonePath);
    }

    private void RestoreSnapshot()
    {
        Plateau plateau = FindPlateau();

        if (plateau == null || !HasSnapshot())
        {
            status = "Kayit yok";
            return;
        }

        PlateauAttach.Placement placement =
            JsonUtility.FromJson<PlateauAttach.Placement>(EditorPrefs.GetString(SnapshotKey));

        Transform visual = PlateauAttach.FindVisual(FindRoot());
        Transform bone = PlateauAttach.ResolveBone(visual, placement.bonePath);

        if (bone == null)
        {
            status = "Kayitli kemik bulunamadi: " + placement.bonePath;
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(plateau.gameObject, "Plateau geri yukle");
        PlateauAttach.ApplyPlacement(plateau, bone, placement);

        ReadFromTarget();
        RebuildBones(plateau);

        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(plateau.gameObject.scene);

        status = "Geri yuklendi: " + bone.name;
    }

    // ---- pose --------------------------------------------------------------

    private Animator FindAnimator()
    {
        Transform root = FindRoot();

        if (root == null)
            return null;

        foreach (Animator candidate in root.GetComponentsInChildren<Animator>(true))
        {
            if (candidate.gameObject.activeInHierarchy)
                return candidate;
        }

        return null;
    }

    // AnimationMode puts the rig into a real pose and hands it back untouched
    // when it stops, so nothing about the character is written to the scene
    private void SamplePose()
    {
        if (EditorApplication.isPlaying)
        {
            status = "Play Mode'da poz gosterilmez, oyun zaten oynatiyor";
            return;
        }

        Animator animator = FindAnimator();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            status = "Aktif Animator veya controller yok";
            return;
        }

        AnimationClip clip = FindClip(animator, poses[poseIndex]);

        if (clip == null)
        {
            status = poses[poseIndex] + " klibi controller'da yok";
            return;
        }

        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(animator.gameObject, clip, 0f);
        AnimationMode.EndSampling();

        posing = true;
        status = "Poz: " + clip.name;

        SceneView.RepaintAll();
    }

    private void StopPose()
    {
        if (!posing)
            return;

        posing = false;

        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();

        // Animation mode restores what it recorded on the way out. The tray is
        // not animated so it should survive, but writing it again costs nothing
        // and removes the doubt
        ApplyAll();

        SceneView.RepaintAll();
    }

    private static AnimationClip FindClip(Animator animator, string wanted)
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && BareName(clip.name) == wanted)
                return clip;
        }

        return null;
    }

    // Clips ship as "CharacterArmature|Idle_Holding"
    private static string BareName(string name)
    {
        int index = name.LastIndexOf('|');

        return index >= 0 ? name.Substring(index + 1) : name;
    }

    // Dragging the tray with the Scene view gizmo never touches these fields, so
    // without this the window keeps serving the values it last read and every
    // apply quietly undoes the hand placing that was just done by eye
    private void OnInspectorUpdate()
    {
        if (hasFocus)
            return;

        Vector3 previousTray = trayPosition;
        Vector3 previousStack = stackPosition;
        Vector3 previousFood = foodPosition;
        Vector3 previousPlate = platePosition;
        Vector3 previousModel = foodModelEuler;

        MirrorSceneFood();

        if (!ReadFromTarget())
        {
            if (previousModel != foodModelEuler)
                Repaint();

            return;
        }

        if (previousTray != trayPosition || previousStack != stackPosition ||
            previousFood != foodPosition || previousPlate != platePosition ||
            previousModel != foodModelEuler)
            Repaint();
    }

    // Dragging the food's mesh node with the Scene view move or rotate tool
    // never touches these fields, and without this the next write puts the
    // stale values straight back over the drag
    private void MirrorSceneFood()
    {
        Transform model = FindSceneFoodModel();

        if (model == null)
            return;

        SpawnableFood food = model.GetComponentInParent<SpawnableFood>();
        int match = System.Array.IndexOf(foodLabels, food.GetType().Name);

        if (match < 0 || match != foodIndex)
            return;

        foodModelPosition = model.localPosition;
        foodModelEuler = model.localRotation.eulerAngles;
        foodModelScale = model.localScale;
    }
}
