using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

// Imports the animated character pack and builds one Animator Controller per
// character, using the state names the existing PlayerAnimator and
// CustomerAnimator already call by hand
public static class CharacterSetup
{
    // Scans several folders so moving the pack around does not break the menu.
    // Missing or empty ones are skipped
    private static readonly string[] characterFolders =
    {
        "Assets/Tiny Coffee Shop/FBX-1",
        "Assets/Tiny Coffee Shop/FBX-2",
    };

    private const string controllersFolder = "Assets/Tiny Coffee Shop/Animations/Characters";

    // The pack ships at roughly twice the size the game uses
    private const float characterScale = .5f;

    // Left side is the state name the game code plays, right side is the clip
    // shipped in the pack
    private static readonly (string state, string clip)[] stateMap =
    {
        ("Idle", "Idle"),
        ("Walk", "Walk"),
        ("IdleWithPlateau", "Idle_Holding"),
        ("WalkWithPlateau", "Walk_Holding"),
        ("Sit", "Sitting_Idle"),
    };

    // Anything that should keep playing rather than freeze on its last frame
    private static readonly HashSet<string> loopingClips = new HashSet<string>
    {
        "Idle", "Walk", "Run",
        "Idle_Holding", "Walk_Holding", "Run_Holding",
        "Chop_Loop", "Pan_Loop", "Assembly_Loop",
        "Jump_Idle", "Sitting_Idle", "Sitting_Eating",
    };

#if COOKED_FAST_SETUP
    [MenuItem("Cooked Fast/Setup New Characters")]
#endif
    public static void SetupNewCharacters()
    {
        List<string> searchFolders = new List<string>();

        foreach (string folder in characterFolders)
        {
            if (AssetDatabase.IsValidFolder(folder))
                searchFolders.Add(folder);
        }

        string[] guids = searchFolders.Count > 0
            ? AssetDatabase.FindAssets("t:Model", searchFolders.ToArray())
            : new string[0];

        if (guids.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata",
                "Karakter klasorlerinde model bulunamadi:\n" + string.Join("\n", characterFolders),
                "Tamam");
            return;
        }

        if (!AssetDatabase.IsValidFolder(controllersFolder))
            CreateFolderRecursive(controllersFolder);

        string report = "";

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            report += ImportCharacter(path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Karakter kurulumu:\n" + report);
        EditorUtility.DisplayDialog("Karakterler", report, "Tamam");
    }

    // Swaps only the visible model on the Player. Every gameplay component
    // stays where it is, so movement, carrying and serving keep working
#if COOKED_FAST_SETUP
    [MenuItem("Cooked Fast/Make Panda The Player")]
#endif
    public static void MakePandaThePlayer()
    {
        SwapPlayerModel("Panda");
    }

    private static void SwapPlayerModel(string characterName)
    {
        PlayerController[] controllers =
            Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (controllers.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata", "Sahnede PlayerController yok", "Tamam");
            return;
        }

        GameObject playerRoot = controllers[0].gameObject;

        if (!playerRoot.TryGetComponent(out PlayerAnimator playerAnimator))
        {
            EditorUtility.DisplayDialog("Hata", "Player'da PlayerAnimator yok", "Tamam");
            return;
        }

        GameObject model = FindCharacterModel(characterName);

        if (model == null)
        {
            EditorUtility.DisplayDialog("Hata", characterName + " modeli bulunamadi", "Tamam");
            return;
        }

        SerializedObject animatorSo = new SerializedObject(playerAnimator);
        SerializedProperty animatorProperty = animatorSo.FindProperty("animator");
        Animator oldAnimator = animatorProperty.objectReferenceValue as Animator;

        string report = "";
        Transform oldVisual = oldAnimator != null ? oldAnimator.transform : null;

        Vector3 localPosition = Vector3.zero;
        Quaternion localRotation = Quaternion.identity;

        if (oldVisual != null && oldVisual != playerRoot.transform)
        {
            localPosition = oldVisual.localPosition;
            localRotation = oldVisual.localRotation;
        }

        // Rescue the tray FIRST. It usually lives inside whichever body is
        // currently on the player, and the swap below removes that body
        List<Plateau> plateaus = new List<Plateau>();

        foreach (Plateau plateau in playerRoot.GetComponentsInChildren<Plateau>(true))
        {
            if (plateau.transform.parent == playerRoot.transform)
                continue;

            Undo.SetTransformParent(plateau.transform, playerRoot.transform, "Rescue plateau");
            plateaus.Add(plateau);
        }

        if (plateaus.Count > 0)
            report += "- " + plateaus.Count + " plateau gecici olarak koke alindi\n";

        // Running the command twice should replace the model, not stack another
        // one on top of the last
        string visualName = characterName + " Visual";

        foreach (Transform child in playerRoot.transform)
        {
            if (child.name == visualName)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                report += "- Onceki " + visualName + " kaldirildi\n";
                break;
            }
        }

        GameObject newVisual = (GameObject)PrefabUtility.InstantiatePrefab(model, playerRoot.transform);
        Undo.RegisterCreatedObjectUndo(newVisual, "Swap player model");

        newVisual.name = visualName;
        newVisual.transform.localPosition = localPosition;
        newVisual.transform.localRotation = localRotation;
        newVisual.transform.localScale = Vector3.one * characterScale;

        Animator newAnimator = newVisual.GetComponentInChildren<Animator>();

        if (newAnimator == null)
            newAnimator = newVisual.AddComponent<Animator>();

        string controllerPath = FindControllerFor(characterName);

        if (controllerPath != null)
        {
            newAnimator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            report += "- Controller: " + System.IO.Path.GetFileName(controllerPath) + "\n";
        }
        else
        {
            report += "- UYARI: " + characterName + " controller'i yok, once Setup New Characters calistir\n";
        }

        animatorProperty.objectReferenceValue = newAnimator;
        animatorSo.ApplyModifiedProperties();
        report += "- PlayerAnimator yeni modele baglandi\n";

        // Put the tray back inside the new body, keeping the world placement it
        // had, so a hand-tuned position survives the swap. It goes on the hand
        // bone rather than the body root: parented to the root it hangs rigidly
        // in front of the character while the arms animate straight through it
        foreach (Plateau plateau in plateaus)
        {
            Transform hand = PlateauAttach.FindHand(newVisual.transform, plateau.transform);
            Transform parent = hand != null ? hand : newVisual.transform;

            Undo.SetTransformParent(plateau.transform, parent, "Reattach plateau");
            report += "- Plateau geri kondu: " + parent.name + "\n";
        }

        if (oldVisual != null && oldVisual != playerRoot.transform && oldVisual != newVisual.transform)
        {
            Undo.RecordObject(oldVisual.gameObject, "Hide old model");
            oldVisual.gameObject.SetActive(false);
            report += "- Eski model kapatildi: " + oldVisual.name + " (silinmedi)\n";
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = newVisual;

        Debug.Log("Player modeli:\n" + report);
        EditorUtility.DisplayDialog("Player Modeli", report, "Tamam");
    }

    // Builds one Customer prefab per rabbit and hands the set to CustomerManager,
    // so every customer that walks in looks different
#if COOKED_FAST_SETUP
    [MenuItem("Cooked Fast/Make Rabbits The Customers")]
#endif
    public static void MakeRabbitsTheCustomers()
    {
        const string sourcePath = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customer.prefab";
        const string outputFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
        {
            EditorUtility.DisplayDialog("Hata", "Customer.prefab bulunamadi", "Tamam");
            return;
        }

        if (!AssetDatabase.IsValidFolder(outputFolder))
            CreateFolderRecursive(outputFolder);

        List<string> rabbits = new List<string>();

        foreach (string folder in characterFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));

                if (name.StartsWith("Rabbit") && !rabbits.Contains(name))
                    rabbits.Add(name);
            }
        }

        if (rabbits.Count <= 0)
        {
            EditorUtility.DisplayDialog("Hata", "Rabbit ile baslayan model yok", "Tamam");
            return;
        }

        List<Customer> variants = new List<Customer>();
        string report = "";

        foreach (string rabbit in rabbits)
        {
            string variantPath = outputFolder + "/Customer_" + rabbit + ".prefab";

            if (BuildCustomerVariant(sourcePath, variantPath, rabbit))
            {
                variants.Add(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath).GetComponent<Customer>());
                report += "- " + rabbit + "\n";
            }
            else
            {
                report += "- " + rabbit + " ATLANDI\n";
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report += AssignVariantsToManager(variants);

        Debug.Log("Musteriler:\n" + report);
        EditorUtility.DisplayDialog("Musteriler", report, "Tamam");
    }

    private const string customersFolder = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers";
    private const string sampleName = "SAMPLE Customer";

    // Drops one customer into the scene so the tray can be nudged by eye
    [MenuItem("Cooked Fast/Add Sample Customer")]
    public static void AddSampleCustomer()
    {
        GameObject prefab = FindSpawningCustomerPrefab();

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Once Make Rabbits The Customers calistir", "Tamam");
            return;
        }

        GameObject existing = GameObject.Find(sampleName);

        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = sampleName;
        instance.transform.position = FindSampleSpot();

        Undo.RegisterCreatedObjectUndo(instance, "Add sample customer");

        Plateau plateau = instance.GetComponentInChildren<Plateau>(true);

        if (plateau == null)
        {
            EditorUtility.DisplayDialog("Hata", prefab.name + " icinde Plateau yok", "Tamam");
            return;
        }

        // A customer's tray is switched off until they are handed something, and
        // it now hangs several bones deep inside the rig. Both together are why
        // it could not be found in the Hierarchy
        plateau.gameObject.SetActive(true);

        AddSampleFood(plateau);

        // Select the tray itself rather than the customer, so the transform
        // fields in the Inspector are already the ones being tuned
        Selection.activeGameObject = plateau.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string path = PathOf(plateau.transform, instance.transform);

        Debug.Log("Ornek musteri plateau yolu:\n" + path);

        EditorUtility.DisplayDialog("Ornek Musteri",
            prefab.name + " sahneye eklendi ve Plateau secildi.\n\n" +
            "Yol:\n" + path + "\n\n" +
            "Tutma pozunda ayarlamak icin:\n" +
            "Cooked Fast > Plateau Hand Adjuster\n\n" +
            "Bitince Apply Sample Plateau To All Customers.",
            "Tamam");
    }

    // The variant CustomerManager actually spawns, not whatever the folder scan
    // happens to return first. Tuning a rabbit the game never shows is wasted work
    private static GameObject FindSpawningCustomerPrefab()
    {
        CustomerManager manager =
            Object.FindFirstObjectByType<CustomerManager>(FindObjectsInactive.Include);

        if (manager != null)
        {
            SerializedProperty list = new SerializedObject(manager).FindProperty("customerPrefabs");

            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                Object entry = list.GetArrayElementAtIndex(i).objectReferenceValue;

                if (entry is Customer customer)
                    return customer.gameObject;
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { customersFolder });

        return guids.Length <= 0
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // Something on the tray, otherwise there is no way to see whether the food
    // sits on the plate or floats above it
    private static void AddSampleFood(Plateau plateau)
    {
        FoodPosition foodPosition = plateau.GetComponentInChildren<FoodPosition>(true);

        if (foodPosition == null)
            return;

        GameObject foodPrefab = FindSceneFood();

        if (foodPrefab == null)
            return;

        GameObject food = (GameObject)PrefabUtility.InstantiatePrefab(foodPrefab, foodPosition.transform);

        food.transform.localPosition = Vector3.zero;
        food.transform.localRotation = Quaternion.identity;
    }

    // Whatever the stations in this scene actually hand out. The kitchen runs on
    // pizza, and a folder scan kept returning the coffee shop's Cup
    private static GameObject FindSceneFood()
    {
        FoodSpawnerStation[] stations = Object.FindObjectsByType<FoodSpawnerStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        GameObject first = null;

        foreach (FoodSpawnerStation station in stations)
        {
            SpawnableFood food = station.SpawnableFoodPrefab;

            if (food == null)
                continue;

            if (first == null)
                first = food.gameObject;

            if (food.name.ToLowerInvariant().Contains("pizza"))
                return food.gameObject;
        }

        return first;
    }

    private static string PathOf(Transform transform, Transform root)
    {
        string path = transform.name;

        while (transform.parent != null && transform != root)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private static Vector3 FindSampleSpot()
    {
        FoodServingCustomerManager manager =
            Object.FindFirstObjectByType<FoodServingCustomerManager>(FindObjectsInactive.Include);

        if (manager != null)
        {
            SerializedObject so = new SerializedObject(manager);
            Transform queueStart = so.FindProperty("queueStartPoint").objectReferenceValue as Transform;

            if (queueStart != null)
                return queueStart.position;
        }

        return Vector3.zero;
    }

    // Copies the tray placement off the sample and into every variant prefab.
    // Superseded by Copy Selected Customer Plateau To Others, which reads the
    // same numbers off whatever is selected instead of only off the sample
#if COOKED_FAST_SETUP
    [MenuItem("Cooked Fast/Apply Sample Plateau To All Customers")]
#endif
    public static void ApplySamplePlateauToAllCustomers()
    {
        GameObject sample = GameObject.Find(sampleName);

        if (sample == null)
        {
            EditorUtility.DisplayDialog("Hata",
                "Sahnede '" + sampleName + "' yok.\nOnce Add Sample Customer calistir.", "Tamam");
            return;
        }

        Plateau samplePlateau = sample.GetComponentInChildren<Plateau>(true);

        if (samplePlateau == null)
        {
            EditorUtility.DisplayDialog("Hata", "Ornekte Plateau bulunamadi", "Tamam");
            return;
        }

        // Hands off to the copy path rather than repeating it. That one reads the
        // bone the sample's tray actually hangs off; this used to run FindHand on
        // each variant and re-guess, which is how hand placing kept getting
        // thrown away on characters that were already correct
        string report = PlateauAttach.CopyPlacementFrom(samplePlateau, sample.transform);

        Debug.Log("Plateau ayari:\n" + report);
        EditorUtility.DisplayDialog("Plateau Ayari", report, "Tamam");
    }

    private static bool BuildCustomerVariant(string sourcePath, string variantPath, string characterName)
    {
        GameObject model = FindCharacterModel(characterName);

        if (model == null)
            return false;

        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);

        try
        {
            if (!root.TryGetComponent(out CustomerAnimator customerAnimator))
                return false;

            SerializedObject animatorSo = new SerializedObject(customerAnimator);
            SerializedProperty animatorProperty = animatorSo.FindProperty("animator");
            Animator oldAnimator = animatorProperty.objectReferenceValue as Animator;
            Transform oldVisual = oldAnimator != null ? oldAnimator.transform : null;

            Vector3 localPosition = oldVisual != null ? oldVisual.localPosition : Vector3.zero;
            Quaternion localRotation = oldVisual != null ? oldVisual.localRotation : Quaternion.identity;

            GameObject newVisual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            newVisual.name = characterName + " Visual";
            newVisual.transform.localPosition = localPosition;
            newVisual.transform.localRotation = localRotation;
            newVisual.transform.localScale = Vector3.one * characterScale;

            Animator newAnimator = newVisual.GetComponentInChildren<Animator>();

            if (newAnimator == null)
                newAnimator = newVisual.AddComponent<Animator>();

            string controllerPath = FindControllerFor(characterName);

            if (controllerPath != null)
                newAnimator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            animatorProperty.objectReferenceValue = newAnimator;
            animatorSo.ApplyModifiedProperties();

            // Move the tray inside the new body and KEEP its local transform.
            // World-preserving would leave it hanging at the old character's
            // height, so a half-size rabbit ends up with food above its head
            foreach (Plateau plateau in root.GetComponentsInChildren<Plateau>(true))
            {
                Vector3 plateauPosition = plateau.transform.localPosition;
                Quaternion plateauRotation = plateau.transform.localRotation;

                plateau.transform.SetParent(newVisual.transform, false);
                plateau.transform.localPosition = plateauPosition;
                plateau.transform.localRotation = plateauRotation;
                plateau.transform.localScale = Vector3.one;

                // Only now, with the tray sitting at the right height on the new
                // body, hang it off the hand so the carry animation moves it.
                // World-preserving this time, since the placement is already right
                Transform hand = PlateauAttach.FindHand(newVisual.transform, plateau.transform);

                if (hand != null)
                    plateau.transform.SetParent(hand, true);
            }

            if (oldVisual != null && oldVisual != root.transform)
                oldVisual.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, variantPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string AssignVariantsToManager(List<Customer> variants)
    {
        CustomerManager manager =
            Object.FindFirstObjectByType<CustomerManager>(FindObjectsInactive.Include);

        if (manager == null)
            return "- UYARI: Sahnede CustomerManager yok, prefablari elle bagla\n";

        SerializedObject managerSo = new SerializedObject(manager);
        SerializedProperty listProperty = managerSo.FindProperty("customerPrefabs");
        listProperty.arraySize = variants.Count;

        for (int i = 0; i < variants.Count; i++)
            listProperty.GetArrayElementAtIndex(i).objectReferenceValue = variants[i];

        managerSo.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return "- CustomerManager'a " + variants.Count + " varyant baglandi\n";
    }

    private static GameObject FindCharacterModel(string characterName)
    {
        foreach (string folder in characterFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                continue;

            foreach (string guid in AssetDatabase.FindAssets("t:Model " + characterName, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (System.IO.Path.GetFileNameWithoutExtension(path) == characterName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        return null;
    }

    // Dragging an FBX into a scene wires the avatar but leaves the controller
    // empty, so the character just stands in its bind pose
    [MenuItem("Cooked Fast/Assign Character Controllers In Scene")]
    public static void AssignControllersInScene()
    {
        Animator[] animators =
            Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        string report = "";

        foreach (Animator animator in animators)
        {
            if (animator.runtimeAnimatorController != null)
                continue;

            // The scene object keeps the model's name, give or take a suffix
            string match = FindControllerFor(animator.name);

            if (match == null)
            {
                report += "- " + animator.name + ": eslesen controller yok\n";
                continue;
            }

            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(match);

            Undo.RecordObject(animator, "Assign controller");
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);

            report += "- " + animator.name + " -> " + System.IO.Path.GetFileName(match) + "\n";
        }

        if (report.Length <= 0)
            report = "Bos controller'i olan Animator yok";

        Debug.Log("Controller atama:\n" + report);
        EditorUtility.DisplayDialog("Controller Atama", report, "Tamam");
    }

    private static string FindControllerFor(string objectName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { controllersFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string controllerName = System.IO.Path.GetFileNameWithoutExtension(path);

            if (objectName.StartsWith(controllerName))
                return path;
        }

        return null;
    }

    private static string ImportCharacter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

        if (importer == null)
            return "- " + path + " okunamadi\n";

        string characterName = System.IO.Path.GetFileNameWithoutExtension(path);

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;

        // The embedded takes do not loop by default, so an idle plays once and
        // freezes. Copy the auto-detected ranges and flip loopTime where needed
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

        for (int i = 0; i < clips.Length; i++)
        {
            bool shouldLoop = loopingClips.Contains(BareName(clips[i].name));
            clips[i].loopTime = shouldLoop;
            clips[i].loopPose = shouldLoop;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        return BuildController(path, characterName, clips.Length);
    }

    private static string BuildController(string modelPath, string characterName, int clipCount)
    {
        Dictionary<string, AnimationClip> clipsByName = new Dictionary<string, AnimationClip>();

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            // Unity keeps a hidden __preview__ copy of every clip next to the
            // real one; taking it would give an empty animation
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                clipsByName[BareName(clip.name)] = clip;
        }

        string controllerPath = controllersFolder + "/" + characterName + ".controller";

        // Overwriting in place can leave the previous state machine behind as an
        // orphaned sub-asset, which the Animator window then trips over
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("moveSpeed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        string missing = "";

        for (int i = 0; i < stateMap.Length; i++)
        {
            // Explicit positions: states stacked on top of each other make the
            // Animator window throw while drawing its graph
            AnimatorState state = stateMachine.AddState(
                stateMap[i].state,
                new Vector3(300f, 60f + i * 70f, 0f));

            if (clipsByName.TryGetValue(stateMap[i].clip, out AnimationClip clip))
                state.motion = clip;
            else
                missing += " " + stateMap[i].clip;

            // The code drives everything with Animator.Play, so the default
            // state only decides what shows before the first call
            if (i == 0)
                stateMachine.defaultState = state;
        }

        EditorUtility.SetDirty(controller);

        string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(modelPath));
        string line = "- " + characterName + " (" + folder + "): " + clipCount + " klip, controller hazir";

        return missing.Length > 0
            ? line + " (EKSIK KLIP:" + missing + ")\n"
            : line + "\n";
    }

    // The pack names its takes "CharacterArmature|Idle". Everything here works
    // off the part after the bar, which is what the pack's docs call the clip
    private static string BareName(string name)
    {
        int separator = name.LastIndexOf('|');

        return separator >= 0 ? name.Substring(separator + 1) : name;
    }

    private static void CreateFolderRecursive(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
