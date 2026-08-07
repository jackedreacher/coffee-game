using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

public class SceneSetup
{
    [MenuItem("Cooked Fast/Setup Lesson 6 (Customer Manager + Spawn Point)")]
    public static void SetupLesson6()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // Load customer prefab
        GameObject customerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Tiny Coffee Shop/Prefabs/Characters/Customer.prefab");

        // =====================
        // 1. CUSTOMER MANAGER
        // =====================
        GameObject customerManagerObj = new GameObject("Customer Manager");
        customerManagerObj.transform.position = Vector3.zero;
        customerManagerObj.AddComponent<CustomerManager>();

        // Set customer prefab reference
        if (customerPrefab != null)
        {
            Customer custComp = customerPrefab.GetComponent<Customer>();
            if (custComp != null)
                SetSerializedFieldObject(customerManagerObj, "CustomerManager", "customerPrefab", custComp);
        }

        // Put under MANAGERS
        GameObject managers = GameObject.Find("--- MANAGERS ---");
        if (managers != null)
            customerManagerObj.transform.SetParent(managers.transform);

        Undo.RegisterCreatedObjectUndo(customerManagerObj, "Create Customer Manager");

        // =====================
        // 2. CUSTOMER SPAWN POINT
        // =====================
        GameObject spawnPoint = new GameObject("CustomerSpawnPoint");
        spawnPoint.transform.position = new Vector3(-6f, 0f, -2f);
        Undo.RegisterCreatedObjectUndo(spawnPoint, "Create Customer Spawn Point");

        // =====================
        // 3. LINK TO CASHIER STATION
        // =====================
        GameObject cashierStation = GameObject.Find("Coffee Cashier Station");
        if (cashierStation != null)
        {
            // Add scripts if not present
            if (cashierStation.GetComponent<FoodServingStation>() == null)
                cashierStation.AddComponent<FoodServingStation>();

            if (cashierStation.GetComponent<FoodServingCustomerManager>() == null)
                cashierStation.AddComponent<FoodServingCustomerManager>();

            SetSerializedFieldObject(cashierStation, "FoodServingCustomerManager", "spawnPoint", spawnPoint.transform);
            SetSerializedFieldInt(cashierStation, "FoodServingCustomerManager", "maxCustomers", 10);
        }

        // Save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Customer Manager + Spawn Point created!");
        EditorUtility.DisplayDialog("Done!", "Customer Manager and Spawn Point created.\n\nSpawn Point position: (-6, 0, -2) - adjust if needed.", "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 13 (Table Set + Table Manager)")]
    public static void SetupLesson13()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // Load assets
        GameObject plateauPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Plateau.prefab");
        GameObject roundTableModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Tiny Coffee Shop/Models/Round_Table.fbx");
        Material paletteMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Tiny Coffee Shop/Materials/Palette.mat");
        GameObject chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Tables and Chairs/Prefabs/Chair2.prefab");

        if (chairPrefab == null)
            Debug.LogWarning("Chair2.prefab not found at Assets/Tables and Chairs/Prefabs/Chair2.prefab");

        // =====================
        // 1. SIMPLE TABLE SET (root) - transform reset
        // =====================
        GameObject tableSet = new GameObject("Simple Table Set");
        tableSet.transform.position = Vector3.zero;
        tableSet.transform.rotation = Quaternion.identity;
        tableSet.transform.localScale = Vector3.one;
        tableSet.AddComponent<TableSet>();

        NavMeshObstacle tableObstacle = tableSet.AddComponent<NavMeshObstacle>();
        tableObstacle.carving = true;
        tableObstacle.size = new Vector3(1.2f, 1f, 1.2f);
        tableObstacle.center = new Vector3(0f, 0.5f, 0f);

        BoxCollider tableCollider = tableSet.AddComponent<BoxCollider>();
        tableCollider.size = new Vector3(1.2f, 1f, 1.2f);
        tableCollider.center = new Vector3(0f, 0.5f, 0f);

        Undo.RegisterCreatedObjectUndo(tableSet, "Create Simple Table Set");

        // =====================
        // 2. ROUND TABLE (child of table set) - local pos zero
        // =====================
        if (roundTableModel != null)
        {
            GameObject roundTable = Object.Instantiate(roundTableModel);
            roundTable.name = "Round Table";
            roundTable.transform.SetParent(tableSet.transform);
            roundTable.transform.localPosition = Vector3.zero;
            roundTable.transform.localRotation = Quaternion.identity;
            roundTable.transform.localScale = Vector3.one;

            if (paletteMat != null)
            {
                Renderer[] renderers = roundTable.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                    r.sharedMaterial = paletteMat;
            }

            // Plateau as child of Round Table, sitting on table surface
            if (plateauPrefab != null)
            {
                GameObject plateau = Object.Instantiate(plateauPrefab);
                plateau.name = "Plateau";
                plateau.transform.SetParent(roundTable.transform);
                plateau.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                plateau.transform.localRotation = Quaternion.identity;
                plateau.transform.localScale = Vector3.one;

                SetSerializedFieldInt(plateau, "Plateau", "maxCapacity", 20);
            }
        }

        // =====================
        // 3. CHAIR 1 (x=0, y=0, z=-0.67)
        // =====================
        GameObject chair1 = CreateChair(chairPrefab, paletteMat, "Chair", new Vector3(0f, 0f, -0.67f), 0f);
        chair1.transform.SetParent(tableSet.transform);

        // =====================
        // 4. CHAIR 2 (x=0, y=0, z=+0.67, rotated 180)
        // =====================
        GameObject chair2 = CreateChair(chairPrefab, paletteMat, "Chair", new Vector3(0f, 0f, 0.67f), 180f);
        chair2.transform.SetParent(tableSet.transform);

        // =====================
        // 5. SAVE AS PREFAB
        // =====================
        string prefabFolder = "Assets/Tiny Coffee Shop/Prefabs/TableStuff";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
            AssetDatabase.CreateFolder("Assets/Tiny Coffee Shop/Prefabs", "TableStuff");

        PrefabUtility.SaveAsPrefabAssetAndConnect(
            chair1, prefabFolder + "/Chair.prefab", InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            tableSet, prefabFolder + "/Simple Table Set.prefab", InteractionMode.AutomatedAction);

        // =====================
        // 6. TABLE MANAGER
        // =====================
        GameObject tableManagerObj = new GameObject("Table Manager");
        tableManagerObj.transform.position = Vector3.zero;
        tableManagerObj.AddComponent<TableManager>();

        GameObject managers = GameObject.Find("--- MANAGERS ---");
        if (managers != null)
            tableManagerObj.transform.SetParent(managers.transform);

        Undo.RegisterCreatedObjectUndo(tableManagerObj, "Create Table Manager");

        // =====================
        // 7. PUT TABLE SET UNDER GAMEPLAY
        // =====================
        GameObject gameplay = GameObject.Find("--- GAMEPLAY ---");
        if (gameplay != null)
            tableSet.transform.SetParent(gameplay.transform, true);

        // Save scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 13: Table Set + Table Manager created!");
        EditorUtility.DisplayDialog("Lesson 13 Done!",
            "Created:\n" +
            "• Simple Table Set at (0,0,0) — move it where you want\n" +
            "• Round Table + Plateau + 2 Chairs (Chair2 prefab)\n" +
            "• Table Manager under MANAGERS\n" +
            "• Prefabs saved to Prefabs/TableStuff/\n\n" +
            "⚡ Move the table set to your desired position.\n" +
            "⚡ Re-bake NavMesh after positioning.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 34 (Duplicate Tables + GUIDs)")]
    public static void SetupLesson34()
    {
        var scene = EditorSceneManager.GetActiveScene();

        GameObject tableSetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Tiny Coffee Shop/Prefabs/TableStuff/Simple Table Set.prefab");

        if (tableSetPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Simple Table Set.prefab not found!\nRun Setup Lesson 13 first.", "OK");
            return;
        }

        GameObject gameplay = GameObject.Find("--- GAMEPLAY ---");

        // Check if there's already a table in the scene
        TableSet existingTable = Object.FindFirstObjectByType<TableSet>();
        if (existingTable == null)
        {
            EditorUtility.DisplayDialog("Error", "No existing table found in scene!\nPlace at least one table first.", "OK");
            return;
        }

        Vector3[] positions = new Vector3[]
        {
            new Vector3(2.5f, 0f, 1.5f),
            new Vector3(-2.5f, 0f, 1.5f),
            new Vector3(0f, 0f, 3.5f)
        };

        int created = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject newTable = (GameObject)PrefabUtility.InstantiatePrefab(tableSetPrefab);
            newTable.name = "Simple Table Set (" + (i + 2) + ")";
            newTable.transform.position = positions[i];

            if (gameplay != null)
                newTable.transform.SetParent(gameplay.transform, true);

            // Generate new GUID
            GuidGenerator guid = newTable.GetComponent<GuidGenerator>();
            if (guid != null)
            {
                SerializedObject so = new SerializedObject(guid);
                SerializedProperty guidProp = so.FindProperty("guid");
                if (guidProp != null)
                {
                    guidProp.stringValue = System.Guid.NewGuid().ToString();
                    so.ApplyModifiedProperties();
                }
            }

            Undo.RegisterCreatedObjectUndo(newTable, "Create Table " + (i + 2));
            created++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 34: " + created + " extra tables created with unique GUIDs!");
        EditorUtility.DisplayDialog("Lesson 34 Done!",
            "Created " + created + " extra tables:\n" +
            "• Table 2 at (2.5, 0, 1.5)\n" +
            "• Table 3 at (-2.5, 0, 1.5)\n" +
            "• Table 4 at (0, 0, 3.5)\n\n" +
            "⚡ Pozisyonları istediğin yere taşı.\n" +
            "⚡ Her tablonun kendine ait GUID'i var.\n" +
            "⚡ NavMesh'i tekrar bake et.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 35 (Cash File on Cashier Station)")]
    public static void SetupLesson35()
    {
        var scene = EditorSceneManager.GetActiveScene();

        GameObject cashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/PinkTea/3D Cartoon Safe Pack/Prefabs/Cash.prefab");

        if (cashPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Cash.prefab not found at\nAssets/PinkTea/3D Cartoon Safe Pack/Prefabs/Cash.prefab", "OK");
            return;
        }

        GameObject cashierStation = GameObject.Find("Coffee Cashier Station");
        if (cashierStation == null)
        {
            EditorUtility.DisplayDialog("Error", "Coffee Cashier Station not found in scene!", "OK");
            return;
        }

        // Check if Cash File already exists
        Transform existingCashFile = cashierStation.transform.Find("Cash File");
        if (existingCashFile != null)
        {
            EditorUtility.DisplayDialog("Warning", "Cash File already exists under Coffee Cashier Station!", "OK");
            return;
        }

        // Create Cash File GameObject
        GameObject cashFileObj = new GameObject("Cash File");
        cashFileObj.transform.SetParent(cashierStation.transform);
        cashFileObj.transform.localPosition = new Vector3(0f, 0f, 1.2f);
        cashFileObj.transform.localRotation = Quaternion.identity;
        cashFileObj.transform.localScale = Vector3.one;

        // Add components
        cashFileObj.AddComponent<GuidGenerator>();
        cashFileObj.AddComponent<CashFile>();

        // Box Collider (trigger)
        BoxCollider col = cashFileObj.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.5f, 1f, 1.5f);
        col.center = new Vector3(0f, 0.5f, 0f);

        // Generate GUID
        GuidGenerator guid = cashFileObj.GetComponent<GuidGenerator>();
        if (guid != null)
        {
            SerializedObject guidSo = new SerializedObject(guid);
            SerializedProperty guidProp = guidSo.FindProperty("guid");
            if (guidProp != null)
            {
                guidProp.stringValue = System.Guid.NewGuid().ToString();
                guidSo.ApplyModifiedProperties();
            }
        }

        // Set CashFile fields
        SetSerializedFieldObject(cashFileObj, "CashFile", "cashPrefab", cashPrefab);
        SetSerializedFieldVector2Int(cashFileObj, "CashFile", "gridSize", new Vector2Int(2, 4));
        SetSerializedFieldVector3(cashFileObj, "CashFile", "gridSpacing", new Vector3(0.75f, 0.15f, 0.4f));

        // Link CashFile to FoodServingStation
        CashFile cashFileComp = cashFileObj.GetComponent<CashFile>();
        if (cashFileComp != null)
            SetSerializedFieldObject(cashierStation, "FoodServingStation", "cashFile", cashFileComp);

        Undo.RegisterCreatedObjectUndo(cashFileObj, "Create Cash File");

        // Save as prefab
        string prefabFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            cashFileObj, prefabFolder + "/Cash Pile.prefab", InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 35: Cash File created on Coffee Cashier Station!");
        EditorUtility.DisplayDialog("Lesson 35 Done!",
            "Created:\n" +
            "• Cash File under Coffee Cashier Station\n" +
            "• BoxCollider (trigger) 1.5x1x1.5\n" +
            "• Grid: 2x4, spacing (0.75, 0.15, 0.4)\n" +
            "• Cash prefab linked\n" +
            "• FoodServingStation.cashFile linked\n" +
            "• Saved as Cash Pile.prefab\n\n" +
            "⚡ Pozisyonu ayarla (localPos şu an 0,0,1.2).\n" +
            "⚡ Play'e bas ve Generate One Cash ile test et.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 36 (Arc Animator Singleton)")]
    public static void SetupLesson36()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // Check if Arc Animator already exists
        ArcAnimator existing = Object.FindFirstObjectByType<ArcAnimator>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Warning", "Arc Animator already exists in scene!", "OK");
            return;
        }

        // Create --- OTHERS --- section if not exists
        GameObject others = GameObject.Find("--- OTHERS ---");
        if (others == null)
        {
            others = new GameObject("--- OTHERS ---");
            others.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(others, "Create OTHERS section");
        }

        // Create Arc Animator
        GameObject arcAnimatorObj = new GameObject("Arc Animator");
        arcAnimatorObj.transform.position = Vector3.zero;
        arcAnimatorObj.AddComponent<ArcAnimator>();
        arcAnimatorObj.transform.SetParent(others.transform);

        Undo.RegisterCreatedObjectUndo(arcAnimatorObj, "Create Arc Animator");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 36: Arc Animator singleton created!");
        EditorUtility.DisplayDialog("Lesson 36 Done!",
            "Created:\n" +
            "• --- OTHERS --- section\n" +
            "• Arc Animator singleton under it\n\n" +
            "Cash collection animation is now ready to use.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 37 (Sijil Save System)")]
    public static void SetupLesson37()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // Check if Sijil already exists
        var existing = Object.FindFirstObjectByType<Tabsil.Sijil.Sijil>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Info", "Sijil already exists in scene!\nNo changes needed.", "OK");
            return;
        }

        // Create --- OTHERS --- section if not exists
        GameObject others = GameObject.Find("--- OTHERS ---");
        if (others == null)
        {
            others = new GameObject("--- OTHERS ---");
            others.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(others, "Create OTHERS section");
        }

        // Create Sijil
        GameObject sijilObj = new GameObject("Sijil");
        sijilObj.transform.position = Vector3.zero;
        sijilObj.AddComponent<Tabsil.Sijil.Sijil>();
        sijilObj.transform.SetParent(others.transform);

        Undo.RegisterCreatedObjectUndo(sijilObj, "Create Sijil");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 37: Sijil save system added to scene!");
        EditorUtility.DisplayDialog("Lesson 37 Done!",
            "Created:\n" +
            "• Sijil save system under --- OTHERS ---\n\n" +
            "CashFile save/load is now ready.\n" +
            "⚡ Tools > Clear Save ile eski kayıtları temizleyebilirsin.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 38 (Locked Element UI Prefab)")]
    public static void SetupLesson38()
    {
        var scene = EditorSceneManager.GetActiveScene();

        Sprite square40 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design Toolbox/Sprites/Tabsil/Square_40.png");
        Sprite square50 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design Toolbox/Sprites/Tabsil/Square_50.png");
        Sprite squareOutline50 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design Toolbox/Sprites/Tabsil/Square_Outline_50.png");
        Sprite cashIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Coffee Shop/Sprites/UI/Cash_icon.png");

        // 1. LOCKED ELEMENT (root)
        GameObject lockedElement = new GameObject("Locked Element");
        lockedElement.transform.position = Vector3.zero;

        // 2. ANIM (LeanTween animation target)
        GameObject anim = new GameObject("Anim");
        anim.transform.SetParent(lockedElement.transform);
        anim.transform.localPosition = Vector3.zero;
        anim.transform.localScale = Vector3.one;

        // 3. CANVAS (World Space, size 2x2, scale 1) under Anim
        GameObject canvasObj = new GameObject("Canvas");
        canvasObj.transform.SetParent(anim.transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.localPosition = new Vector3(0f, 1f, 0f);
        canvasRect.localRotation = Quaternion.Euler(90f, -90f, 0f);
        canvasRect.sizeDelta = new Vector2(2f, 2f);
        canvasRect.localScale = Vector3.one;

        // 4. CONTAINER (stretch to canvas)
        GameObject container = new GameObject("Container");
        container.transform.SetParent(canvasObj.transform);
        Image containerImg = container.AddComponent<Image>();
        if (square50 != null) containerImg.sprite = square50;
        containerImg.type = Image.Type.Simple;
        containerImg.color = new Color(0.35f, 0.35f, 0.35f, 0.9f);

        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;
        containerRect.localPosition = Vector3.zero;
        containerRect.localRotation = Quaternion.identity;

        // 5. FILL IMAGE
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(container.transform);
        Image fillImg = fill.AddComponent<Image>();
        if (square40 != null) fillImg.sprite = square40;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Vertical;
        fillImg.fillOrigin = 0;
        fillImg.fillAmount = 0f;
        fillImg.color = new Color(1f, 0.85f, 0.2f, 1f);

        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // 6. VERTICAL LAYOUT
        GameObject vertLayout = new GameObject("Vertical Layout");
        vertLayout.transform.SetParent(container.transform);

        RectTransform vlRect = vertLayout.AddComponent<RectTransform>();
        vlRect.anchorMin = Vector2.zero;
        vlRect.anchorMax = Vector2.one;
        vlRect.offsetMin = Vector2.zero;
        vlRect.offsetMax = Vector2.zero;

        var vlGroup = vertLayout.AddComponent<VerticalLayoutGroup>();
        vlGroup.childControlWidth = true;
        vlGroup.childControlHeight = true;
        vlGroup.childForceExpandWidth = true;
        vlGroup.childForceExpandHeight = true;

        // Price Text
        GameObject priceTextObj = new GameObject("Price Text");
        priceTextObj.transform.SetParent(vertLayout.transform);
        TextMeshProUGUI priceText = priceTextObj.AddComponent<TextMeshProUGUI>();
        priceText.text = "256";
        priceText.fontSize = 0.6f;
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.enableAutoSizing = false;

        LayoutElement priceLayout = priceTextObj.AddComponent<LayoutElement>();
        priceLayout.preferredHeight = 1.2f;

        // Icon
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(vertLayout.transform);
        Image iconImg = iconObj.AddComponent<Image>();
        if (cashIcon != null) iconImg.sprite = cashIcon;
        iconImg.preserveAspect = true;

        LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.preferredHeight = 0.8f;

        // 7. OUTLINE
        GameObject outline = new GameObject("Outline");
        outline.transform.SetParent(container.transform);
        Image outlineImg = outline.AddComponent<Image>();
        if (squareOutline50 != null) outlineImg.sprite = squareOutline50;
        outlineImg.type = Image.Type.Simple;
        outlineImg.color = Color.white;

        RectTransform outlineRect = outline.GetComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = new Vector2(-0.02f, -0.02f);
        outlineRect.offsetMax = new Vector2(0.02f, 0.02f);

        // 8. UNLOCKED ELEMENTS
        GameObject unlockedElements = new GameObject("Unlocked Elements");
        unlockedElements.transform.SetParent(lockedElement.transform);
        unlockedElements.transform.localPosition = Vector3.zero;

        Undo.RegisterCreatedObjectUndo(lockedElement, "Create Locked Element");

        // 9. PLACE UNDER GAMEPLAY
        GameObject gameplay = GameObject.Find("--- GAMEPLAY ---");
        if (gameplay != null)
            lockedElement.transform.SetParent(gameplay.transform, true);

        // Save as prefab
        string prefabFolder = "Assets/Tiny Coffee Shop/Prefabs/GamePlay";
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            lockedElement, prefabFolder + "/Locked Element.prefab", InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 38: Locked Element UI prefab created!");
        EditorUtility.DisplayDialog("Lesson 38 Done!",
            "Created:\n" +
            "• Locked Element\n" +
            "  └ Anim\n" +
            "    └ Canvas (World Space, 2x2, scale 1)\n" +
            "      └ Container + Fill + VLayout + Outline\n" +
            "  └ Unlocked Elements\n\n" +
            "⚡ Pozisyonu ayarla.\n" +
            "⚡ Unlock edilecek objeyi Unlocked Elements altına koy.\n" +
            "⚡ Font size'ı inspector'dan ayarla (0.5-1 arası).",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 39 (LockedElement + PayAbility)")]
    public static void SetupLesson39()
    {
        var scene = EditorSceneManager.GetActiveScene();

        GameObject cashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/PinkTea/3D Cartoon Safe Pack/Prefabs/Cash.prefab");

        // 1. Add PayAbility to Player
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            if (player.GetComponent<PayAbility>() == null)
                player.AddComponent<PayAbility>();

            if (cashPrefab != null)
                SetSerializedFieldObject(player, "PayAbility", "cashPrefab", cashPrefab);
        }

        // 2. Setup Locked Element in scene (search everywhere, including inactive)
        GameObject lockedElement = null;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == "Locked Element")
            {
                lockedElement = go;
                break;
            }
        }
        if (lockedElement == null)
        {
            lockedElement = new GameObject("Locked Element");
            lockedElement.transform.position = new Vector3(2f, 0f, 2f);
            GameObject gameplay = GameObject.Find("--- GAMEPLAY ---");
            if (gameplay != null)
                lockedElement.transform.SetParent(gameplay.transform, true);
            Undo.RegisterCreatedObjectUndo(lockedElement, "Create Locked Element");
        }

        // Add LockedElement script
        if (lockedElement.GetComponent<LockedElement>() == null)
            lockedElement.AddComponent<LockedElement>();

        // Add BoxCollider (trigger) if not present
        BoxCollider col = lockedElement.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = lockedElement.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 2f);
            col.center = new Vector3(0f, 1f, 0f);
        }

        // Wire up references
        Transform animTransform = lockedElement.transform.Find("Anim");
        if (animTransform != null)
            SetSerializedFieldObject(lockedElement, "LockedElement", "anim", animTransform);

        // Find Price Text and Fill Image in Canvas
        TextMeshProUGUI priceText = lockedElement.GetComponentInChildren<TextMeshProUGUI>(true);
        if (priceText != null)
            SetSerializedFieldObject(lockedElement, "LockedElement", "priceText", priceText);

        Image[] images = lockedElement.GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            if (img.gameObject.name == "Fill")
            {
                SetSerializedFieldObject(lockedElement, "LockedElement", "fillImage", img);
                break;
            }
        }

        // Find Unlocked Elements
        Transform unlockedElements = lockedElement.transform.Find("Unlocked Elements");
        if (unlockedElements != null)
            SetSerializedFieldObject(lockedElement, "LockedElement", "unlockedElements", unlockedElements.gameObject);

        SetSerializedFieldInt(lockedElement, "LockedElement", "initialPrice", 100);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 39: LockedElement + PayAbility setup complete!");
        EditorUtility.DisplayDialog("Lesson 39 Done!",
            "Created:\n" +
            "• LockedElement script on Locked Element\n" +
            "• BoxCollider (trigger 2x2x2) on Locked Element\n" +
            "• PayAbility on Player (+ cash prefab linked)\n" +
            "• Price Text, Fill Image, Anim references wired\n" +
            "• Initial Price: 100\n\n" +
            "⚡ Locked Element pozisyonunu ayarla.\n" +
            "⚡ Unlock edilecek objeyi Unlocked Elements altına koy.\n" +
            "⚡ Tools > Clear Save ile eski kayıtları temizle.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 42 (Progression Manager)")]
    public static void SetupLesson42()
    {
        var scene = EditorSceneManager.GetActiveScene();

        ProgressionManager existing = Object.FindFirstObjectByType<ProgressionManager>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Warning", "Progression Manager already exists in scene!", "OK");
            return;
        }

        GameObject pmObj = new GameObject("Progression Manager");
        pmObj.transform.position = Vector3.zero;

        GameObject managers = GameObject.Find("--- MANAGERS ---");
        if (managers != null)
            pmObj.transform.SetParent(managers.transform);

        ProgressionManager pm = pmObj.AddComponent<ProgressionManager>();
        Undo.RegisterCreatedObjectUndo(pmObj, "Create Progression Manager");

        // Auto-find LockedElement objects by name
        GameObject firstTableLE = null;
        GameObject coffeeStationLE = null;
        GameObject coffeeCashierStationLE = null;

        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == "First Table LE") firstTableLE = go;
            else if (go.name == "Coffee Station LE") coffeeStationLE = go;
            else if (go.name == "Coffee Cashier Station LE") coffeeCashierStationLE = go;
        }

        // Configure progression steps via SerializedObject
        SerializedObject pmSo = new SerializedObject(pm);
        SerializedProperty stepsArr = pmSo.FindProperty("progressionSteps");
        stepsArr.arraySize = 3;

        // Step 0: First Table
        SerializedProperty step0 = stepsArr.GetArrayElementAtIndex(0);
        step0.FindPropertyRelative("name").stringValue = "First Table";
        SerializedProperty elements0 = step0.FindPropertyRelative("lockedElements");
        if (firstTableLE != null && firstTableLE.TryGetComponent(out LockedElement le0))
        {
            elements0.arraySize = 1;
            elements0.GetArrayElementAtIndex(0).objectReferenceValue = le0;
        }

        // Step 1: Coffee Station
        SerializedProperty step1 = stepsArr.GetArrayElementAtIndex(1);
        step1.FindPropertyRelative("name").stringValue = "Coffee Station";
        SerializedProperty elements1 = step1.FindPropertyRelative("lockedElements");
        if (coffeeStationLE != null && coffeeStationLE.TryGetComponent(out LockedElement le1))
        {
            elements1.arraySize = 1;
            elements1.GetArrayElementAtIndex(0).objectReferenceValue = le1;
        }

        // Step 2: Coffee Cashier Station
        SerializedProperty step2 = stepsArr.GetArrayElementAtIndex(2);
        step2.FindPropertyRelative("name").stringValue = "Coffee Cashier Station";
        SerializedProperty elements2 = step2.FindPropertyRelative("lockedElements");
        if (coffeeCashierStationLE != null && coffeeCashierStationLE.TryGetComponent(out LockedElement le2))
        {
            elements2.arraySize = 1;
            elements2.GetArrayElementAtIndex(0).objectReferenceValue = le2;
        }

        pmSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string found = "";
        found += firstTableLE != null ? "✓ First Table LE\n" : "✗ First Table LE (assign manually)\n";
        found += coffeeStationLE != null ? "✓ Coffee Station LE\n" : "✗ Coffee Station LE (assign manually)\n";
        found += coffeeCashierStationLE != null ? "✓ Coffee Cashier Station LE\n" : "✗ Coffee Cashier Station LE (assign manually)\n";

        Debug.Log("✅ Lesson 42: Progression Manager created!");
        EditorUtility.DisplayDialog("Lesson 42 Done!",
            "Created:\n" +
            "• Progression Manager under --- MANAGERS ---\n" +
            "• 3 Progression Steps pre-configured\n\n" +
            "LockedElement auto-find:\n" + found +
            "\n⚡ Eksik LE'leri inspector'dan manuel ata.\n" +
            "⚡ Tools > Clear Save → Play ile test et.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 47 (HR Canvas)")]
    public static void SetupLesson47()
    {
        var scene = EditorSceneManager.GetActiveScene();

        if (GameObject.Find("HR Canvas") != null)
        {
            EditorUtility.DisplayDialog("Warning", "HR Canvas already exists in scene!", "OK");
            return;
        }

        Sprite square50 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design Toolbox/Sprites/Tabsil/Square_50.png");
        Sprite crossIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design Toolbox/Sprites/Heathen Engineering/Icons/Free Flat Button Solid Cross Icon.png");
        GameObject workerUIContainerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tiny Coffee Shop/Prefabs/UI/UI Worker Container.prefab");

        GameObject uiParent = GameObject.Find("--- UI ---");

        // 1. HR CANVAS
        GameObject canvasObj = new GameObject("HR Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f); // portrait, matches Main Canvas
        scaler.matchWidthOrHeight = 0f; // match width so wide content (cards, buttons) never gets cropped

        canvasObj.AddComponent<GraphicRaycaster>();

        if (uiParent != null)
            canvasObj.transform.SetParent(uiParent.transform);

        Undo.RegisterCreatedObjectUndo(canvasObj, "Create HR Canvas");

        // 2. BACKGROUND
        GameObject background = new GameObject("Background");
        background.transform.SetParent(canvasObj.transform);
        Image bgImg = background.AddComponent<Image>();
        bgImg.color = new Color(0.25f, 0.45f, 0.65f, 1f);

        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 3. TITLE
        GameObject title = new GameObject("Title");
        title.transform.SetParent(canvasObj.transform);
        TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = "Human Resources";
        titleText.fontSize = 72f;
        titleText.alignment = TextAlignmentOptions.Center;

        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -60f);
        titleRect.sizeDelta = new Vector2(900f, 150f);

        // 4. BACK BUTTON
        GameObject backButton = new GameObject("Back Button");
        backButton.transform.SetParent(canvasObj.transform);
        Image backImg = backButton.AddComponent<Image>();
        if (square50 != null) backImg.sprite = square50;
        backImg.type = Image.Type.Sliced;
        backImg.color = new Color(0.85f, 0.25f, 0.25f, 1f);
        backButton.AddComponent<Button>();

        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(150f, -142f);
        backRect.sizeDelta = new Vector2(100f, 100f);

        GameObject backIcon = new GameObject("Icon");
        backIcon.transform.SetParent(backButton.transform);
        Image backIconImg = backIcon.AddComponent<Image>();
        if (crossIcon != null) backIconImg.sprite = crossIcon;
        backIconImg.preserveAspect = true;

        RectTransform backIconRect = backIcon.GetComponent<RectTransform>();
        backIconRect.anchorMin = Vector2.zero;
        backIconRect.anchorMax = Vector2.one;
        backIconRect.offsetMin = new Vector2(15f, 15f);
        backIconRect.offsetMax = new Vector2(-15f, -15f);

        // 5. CURRENCY CONTAINER (copy from Main Canvas if present)
        GameObject mainCanvasObj = GameObject.Find("Main Canvas");
        Transform mainCurrencyContainerT = mainCanvasObj != null ? mainCanvasObj.transform.Find("Currency Container") : null;
        if (mainCurrencyContainerT != null)
        {
            GameObject currencyCopy = Object.Instantiate(mainCurrencyContainerT.gameObject, canvasObj.transform);
            currencyCopy.name = "Currency Container";
            RectTransform ccRect = currencyCopy.GetComponent<RectTransform>();
            RectTransform srcRect = mainCurrencyContainerT.GetComponent<RectTransform>();
            ccRect.anchorMin = srcRect.anchorMin;
            ccRect.anchorMax = srcRect.anchorMax;
            ccRect.pivot = srcRect.pivot;
            ccRect.anchoredPosition = srcRect.anchoredPosition;
            ccRect.sizeDelta = srcRect.sizeDelta;
        }

        // 6. WORKER CONTAINER SCROLL (Scroll View)
        GameObject scrollView = new GameObject("Worker Container Scroll");
        scrollView.transform.SetParent(canvasObj.transform);
        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.15f);
        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        RectTransform scrollViewRect = scrollView.GetComponent<RectTransform>();
        scrollViewRect.anchorMin = Vector2.zero;
        scrollViewRect.anchorMax = Vector2.one;
        scrollViewRect.offsetMin = new Vector2(0f, 0f);
        scrollViewRect.offsetMax = new Vector2(0f, -800f);

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform);
        Image viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = Color.white;
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup vlGroup = content.AddComponent<VerticalLayoutGroup>();
        vlGroup.padding = new RectOffset(20, 20, 20, 20);
        vlGroup.spacing = 20f;
        vlGroup.childAlignment = TextAnchor.UpperCenter;
        vlGroup.childControlWidth = true;
        vlGroup.childControlHeight = true;
        vlGroup.childForceExpandWidth = false;
        vlGroup.childForceExpandHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        // Drop one Worker UI Container instance as a preview
        if (workerUIContainerPrefab != null)
        {
            GameObject preview = (GameObject)PrefabUtility.InstantiatePrefab(workerUIContainerPrefab, content.transform);
            preview.name = "Worker UI Container";
        }

        // 7. CANVAS STARTS DISABLED
        canvasObj.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 47: HR Canvas created!");
        EditorUtility.DisplayDialog("Lesson 47 Done!",
            "Oluşturulan yapı:\n" +
            "• HR Canvas (sortOrder=1, disabled)\n" +
            "  ├ Background (mavi panel)\n" +
            "  ├ Title (\"Human Resources\")\n" +
            "  ├ Back Button (kırmızı, cross icon)\n" +
            "  ├ Currency Container (Main Canvas'tan kopya)\n" +
            "  └ Worker Container Scroll\n" +
            "     └ Viewport → Content (VerticalLayoutGroup + ContentSizeFitter)\n" +
            "        └ Worker UI Container (preview, 1 adet)\n\n" +
            "⚡ Progression Canvas sort order'ını kontrol et (10 önerilir, HR'nin üstünde kalmalı).\n" +
            "⚡ Görsel ayarları (renk, boyut, spacing) beğendiğin gibi düzenle.\n" +
            "⚡ Back Button + Currency Container hizası: Y=-142 (ikisi de aynı hizada).",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 54 (Worker Stat Values)")]
    public static void SetupLesson54()
    {
        // The blob math assumes the three maxes are equal, so each worker
        // gets a single tier value applied to speed, capacity and revenue.
        (string fileName, int tier)[] tiers = new (string, int)[]
        {
            ("00_Angelo", 2),
            ("01_Kai", 3),
            ("02_Jawed", 4),
            ("03_Matteo", 5),
            ("04_Ethan", 7),
        };

        string report = "";
        int updated = 0;

        foreach (var t in tiers)
        {
            string assetPath = "Assets/Tiny Coffee Shop/Data/Workers/" + t.fileName + ".asset";
            WorkerDataSO data = AssetDatabase.LoadAssetAtPath<WorkerDataSO>(assetPath);

            if (data == null)
            {
                report += "✗ " + t.fileName + "\n";
                continue;
            }

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("maxSpeed").intValue = t.tier;
            so.FindProperty("maxCapacity").intValue = t.tier;
            so.FindProperty("maxRevenue").intValue = t.tier;
            so.ApplyModifiedProperties();

            report += "✓ " + t.fileName + " → " + t.tier + "/" + t.tier + "/" + t.tier + "\n";
            updated++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log("✅ Lesson 54: " + updated + " worker stat sets written!");
        EditorUtility.DisplayDialog("Lesson 54 Done!",
            "Worker stat değerleri (speed/capacity/revenue):\n\n" + report +
            "\n⚡ Değerleri Inspector'dan istediğin gibi değiştirebilirsin,\n" +
            "   ama üçünü EŞİT tut — blob matematiği bunu varsayıyor.\n" +
            "⚡ Tools > Clear Save → Play ile test et.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 53 (Worker Stats + Upgrade Blobs)")]
    public static void SetupLesson53()
    {
        // 1. Put UIUpgradeBlob on the blob prefab and wire its Image
        string blobPath = "Assets/Tiny Coffee Shop/Prefabs/UI/Worker Upgrade Blob.prefab";
        GameObject blobAsset = AssetDatabase.LoadAssetAtPath<GameObject>(blobPath);
        if (blobAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "Worker Upgrade Blob.prefab not found!", "OK");
            return;
        }

        GameObject blobRoot = PrefabUtility.LoadPrefabContents(blobPath);

        UIUpgradeBlob blobComp = blobRoot.GetComponent<UIUpgradeBlob>();
        if (blobComp == null)
            blobComp = blobRoot.AddComponent<UIUpgradeBlob>();

        Image blobImage = blobRoot.GetComponentInChildren<Image>(true);
        SerializedObject blobSo = new SerializedObject(blobComp);
        AssignRef(blobSo, "blobImage", blobImage);
        blobSo.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(blobRoot, blobPath);
        PrefabUtility.UnloadPrefabContents(blobRoot);

        UIUpgradeBlob blobPrefabComp = AssetDatabase
            .LoadAssetAtPath<GameObject>(blobPath)
            .GetComponent<UIUpgradeBlob>();

        // 2. Put UIWorkerStat on the three stat sections of the container
        string containerPath = "Assets/Tiny Coffee Shop/Prefabs/UI/UI Worker Container.prefab";
        GameObject containerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(containerPath);
        if (containerAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "UI Worker Container.prefab not found!", "OK");
            return;
        }

        GameObject containerRoot = PrefabUtility.LoadPrefabContents(containerPath);

        string[] sectionNames = { "Speed Section", "Capacity Section", "Revenue Section" };
        UIWorkerStat[] statComps = new UIWorkerStat[sectionNames.Length];
        string report = "";

        for (int i = 0; i < sectionNames.Length; i++)
        {
            Transform section = FindDeepChild(containerRoot.transform, sectionNames[i]);

            if (section == null)
            {
                report += "✗ " + sectionNames[i] + "\n";
                continue;
            }

            UIWorkerStat statComp = section.GetComponent<UIWorkerStat>();
            if (statComp == null)
                statComp = section.gameObject.AddComponent<UIWorkerStat>();

            statComps[i] = statComp;

            // Blobs live next to the "Upgrade Label" inside the section's row
            Transform blobsParent = section.childCount > 0 ? section.GetChild(0) : section;

            SerializedObject statSo = new SerializedObject(statComp);
            AssignRef(statSo, "upgradeBlobPrefab", blobPrefabComp);
            AssignRef(statSo, "blobsParent", blobsParent);
            statSo.ApplyModifiedProperties();

            report += "✓ " + sectionNames[i] + " (blobs → " + blobsParent.name + ")\n";
        }

        // 3. Point the container's stats[] at those three sections
        UIWorkerContainer container = containerRoot.GetComponent<UIWorkerContainer>();
        if (container != null)
        {
            SerializedObject containerSo = new SerializedObject(container);
            SerializedProperty statsProp = containerSo.FindProperty("stats");
            statsProp.arraySize = statComps.Length;

            for (int i = 0; i < statComps.Length; i++)
                statsProp.GetArrayElementAtIndex(i).objectReferenceValue = statComps[i];

            containerSo.ApplyModifiedProperties();
        }

        PrefabUtility.SaveAsPrefabAsset(containerRoot, containerPath);
        PrefabUtility.UnloadPrefabContents(containerRoot);
        AssetDatabase.SaveAssets();

        Debug.Log("✅ Lesson 53: Worker stats + upgrade blobs wired!");
        EditorUtility.DisplayDialog("Lesson 53 Done!",
            "• Worker Upgrade Blob prefab → UIUpgradeBlob script + Image bağlandı\n" +
            "• UI Worker Container'ın 3 stat bölümüne UIWorkerStat eklendi:\n" + report +
            "• Container.stats[] → 3 bölüm bağlandı\n\n" +
            "⚡ Blob'lar HENÜZ spawn olmuyor — Initialize() çağrısı 54. derste geliyor.\n" +
            "⚡ Bu derste test edilebilir olan: paralı unlock butonu (para düşmeli).",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 51 (Worker Spawn Point)")]
    public static void SetupLesson51()
    {
        var scene = EditorSceneManager.GetActiveScene();

        WorkerManager workerManager = Object.FindFirstObjectByType<WorkerManager>();
        if (workerManager == null)
        {
            EditorUtility.DisplayDialog("Error", "Worker Manager not found in scene!", "OK");
            return;
        }

        HRManager hrManager = Object.FindFirstObjectByType<HRManager>();
        if (hrManager == null)
        {
            EditorUtility.DisplayDialog("Error", "HR Manager not found!\nRun Setup Lesson 48 first.", "OK");
            return;
        }

        // Spawn point is deliberately NOT a child of Worker Manager,
        // since spawned workers get parented to it at runtime.
        GameObject spawnPoint = GameObject.Find("Worker Spawn Point");
        bool created = false;

        if (spawnPoint == null)
        {
            spawnPoint = new GameObject("Worker Spawn Point");
            spawnPoint.transform.position = new Vector3(-4f, 0f, 4f);

            GameObject others = GameObject.Find("--- OTHERS ---");
            if (others != null)
                spawnPoint.transform.SetParent(others.transform, true);

            Undo.RegisterCreatedObjectUndo(spawnPoint, "Create Worker Spawn Point");
            created = true;
        }

        SetSerializedFieldObject(workerManager.gameObject, "WorkerManager", "workerSpawnPoint", spawnPoint.transform);
        SetSerializedFieldObject(hrManager.gameObject, "HRManager", "workerManager", workerManager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 51: Worker Spawn Point wired!");
        EditorUtility.DisplayDialog("Lesson 51 Done!",
            (created
                ? "• Worker Spawn Point oluşturuldu (-4, 0, 4)\n"
                : "• Mevcut Worker Spawn Point kullanıldı\n") +
            "• Worker Manager → Worker Spawn Point bağlandı\n" +
            "• HR Manager → Worker Manager bağlandı\n\n" +
            "⚡ Spawn Point'i sahnede istediğin yere taşı (NavMesh üstünde olsun).\n" +
            "⚡ Play → HR paneli aç → mavi FREE butonuna bas → worker spawn olmalı.\n" +
            "⚡ Not: Henüz kayıt yok, oyunu kapatınca worker'lar kaybolur (Lesson 52).",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 50 (Wire UI Worker Container)")]
    public static void SetupLesson50()
    {
        string prefabPath = "Assets/Tiny Coffee Shop/Prefabs/UI/UI Worker Container.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "UI Worker Container.prefab not found!", "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        UIWorkerContainer container = root.GetComponent<UIWorkerContainer>();
        if (container == null)
            container = root.AddComponent<UIWorkerContainer>();

        Transform workerIcon = FindDeepChild(root.transform, "Worker Icon");
        Transform nameText = FindDeepChild(root.transform, "Name Text");
        Transform lockedOverlay = FindDeepChild(root.transform, "Locked Overlay");
        Transform unlockButton = FindDeepChild(root.transform, "Unlock Button");
        Transform videoUnlockButton = FindDeepChild(root.transform, "Video Unlock Button");
        Transform upgradeButton = FindDeepChild(root.transform, "Upgrade Button");
        Transform videoUpgradeButton = FindDeepChild(root.transform, "Video Upgrade Button");

        SerializedObject so = new SerializedObject(container);

        AssignRef(so, "profileImage", workerIcon != null ? workerIcon.GetComponent<Image>() : null);
        AssignRef(so, "nameText", nameText != null ? nameText.GetComponent<TextMeshProUGUI>() : null);
        AssignRef(so, "lockedOverlay", lockedOverlay != null ? lockedOverlay.gameObject : null);

        AssignRef(so, "unlockButton", unlockButton != null ? unlockButton.GetComponent<Button>() : null);
        AssignRef(so, "videoUnlockButton", videoUnlockButton != null ? videoUnlockButton.GetComponent<Button>() : null);
        AssignRef(so, "upgradeButton", upgradeButton != null ? upgradeButton.GetComponent<Button>() : null);
        AssignRef(so, "videoUpgradeButton", videoUpgradeButton != null ? videoUpgradeButton.GetComponent<Button>() : null);

        // Price texts live inside their respective buttons
        AssignRef(so, "unlockPriceText", unlockButton != null ? unlockButton.GetComponentInChildren<TextMeshProUGUI>(true) : null);
        AssignRef(so, "upgradePriceText", upgradeButton != null ? upgradeButton.GetComponentInChildren<TextMeshProUGUI>(true) : null);

        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        string report = "";
        report += workerIcon != null ? "✓ Worker Icon\n" : "✗ Worker Icon\n";
        report += nameText != null ? "✓ Name Text\n" : "✗ Name Text\n";
        report += lockedOverlay != null ? "✓ Locked Overlay\n" : "✗ Locked Overlay\n";
        report += unlockButton != null ? "✓ Unlock Button\n" : "✗ Unlock Button\n";
        report += videoUnlockButton != null ? "✓ Video Unlock Button\n" : "✗ Video Unlock Button\n";
        report += upgradeButton != null ? "✓ Upgrade Button\n" : "✗ Upgrade Button\n";
        report += videoUpgradeButton != null ? "✓ Video Upgrade Button\n" : "✗ Video Upgrade Button\n";

        Debug.Log("✅ Lesson 50: UI Worker Container references wired!");
        EditorUtility.DisplayDialog("Lesson 50 Done!",
            "UI Worker Container prefab referansları bağlandı:\n\n" + report +
            "\n⚡ ✗ olanları prefab'ı açıp elle ata.\n" +
            "⚡ Play'e bas: ilk worker FREE, diğerleri fiyatlı görünmeli.\n" +
            "⚡ Yeterli parası olmayan butonlar pasif (gri) olmalı.",
            "OK");
    }

    private static void AssignRef(SerializedObject so, string fieldName, Object value)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }
        return null;
    }

    [MenuItem("Cooked Fast/Setup Lesson 49 (Worker Data + Containers)")]
    public static void SetupLesson49()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // 1. Add UIWorkerContainer to the prefab asset
        string prefabPath = "Assets/Tiny Coffee Shop/Prefabs/UI/UI Worker Container.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
        {
            EditorUtility.DisplayDialog("Error", "UI Worker Container.prefab not found!\nRun Setup Lesson 47 first.", "OK");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabRoot.GetComponent<UIWorkerContainer>() == null)
            prefabRoot.AddComponent<UIWorkerContainer>();
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        // 2. Create WorkerDataSO assets
        string dataFolder = "Assets/Tiny Coffee Shop/Data";
        if (!AssetDatabase.IsValidFolder(dataFolder))
            AssetDatabase.CreateFolder("Assets/Tiny Coffee Shop", "Data");

        string workersFolder = dataFolder + "/Workers";
        if (!AssetDatabase.IsValidFolder(workersFolder))
            AssetDatabase.CreateFolder(dataFolder, "Workers");

        Sprite profilePicture = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Tiny Coffee Shop/Sprites/Worker_Icons/Worker_Icon.png");
        Worker workerPrefabComp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tiny Coffee Shop/Prefabs/Characters/Worker.prefab")?.GetComponent<Worker>();

        (string fileName, string workerName, int price, int level)[] workersToCreate = new (string, string, int, int)[]
        {
            ("00_Angelo", "Angelo", 100, 0),
            ("01_Kai", "Kai", 200, 1),
            ("02_Jawed", "Jawed", 300, 2),
            ("03_Matteo", "Matteo", 400, 3),
            ("04_Ethan", "Ethan", 500, 4),
        };

        System.Collections.Generic.List<WorkerDataSO> createdDatas = new System.Collections.Generic.List<WorkerDataSO>();

        foreach (var w in workersToCreate)
        {
            string assetPath = workersFolder + "/" + w.fileName + ".asset";
            WorkerDataSO data = AssetDatabase.LoadAssetAtPath<WorkerDataSO>(assetPath);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WorkerDataSO>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("name").stringValue = w.workerName;
            so.FindProperty("unlockPrice").intValue = w.price;
            so.FindProperty("initialLevel").intValue = w.level;
            if (profilePicture != null)
                so.FindProperty("profilePicture").objectReferenceValue = profilePicture;
            if (workerPrefabComp != null)
                so.FindProperty("prefab").objectReferenceValue = workerPrefabComp;
            so.ApplyModifiedProperties();

            createdDatas.Add(data);
        }

        AssetDatabase.SaveAssets();

        // 3. Wire HR Manager
        GameObject hrManagerObj = GameObject.Find("HR Manager");
        if (hrManagerObj == null)
        {
            EditorUtility.DisplayDialog("Error", "HR Manager not found!\nRun Setup Lesson 48 first.", "OK");
            return;
        }

        UIWorkerContainer prefabComp = prefabAsset.GetComponent<UIWorkerContainer>();
        SetSerializedFieldObject(hrManagerObj, "HRManager", "uiWorkerContainerPrefab", prefabComp);

        // Content = HR Canvas > Worker Container Scroll > Viewport > Content
        GameObject hrCanvas = GameObject.Find("HR Canvas");
        Transform content = hrCanvas != null
            ? hrCanvas.transform.Find("Worker Container Scroll/Viewport/Content")
            : null;

        if (content != null)
        {
            SetSerializedFieldObject(hrManagerObj, "HRManager", "workerContainersParent", content);

            // Remove any leftover manually-placed preview instances (HRManager spawns its own at runtime)
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child.name == "Worker UI Container" || child.name == "UI Worker Container")
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        SerializedObject hrManagerSo = new SerializedObject(hrManagerObj.GetComponent<HRManager>());
        SerializedProperty workerDatasProp = hrManagerSo.FindProperty("workerDatas");
        workerDatasProp.arraySize = createdDatas.Count;
        for (int i = 0; i < createdDatas.Count; i++)
            workerDatasProp.GetArrayElementAtIndex(i).objectReferenceValue = createdDatas[i];
        hrManagerSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 49: Worker Data + Containers wired!");
        EditorUtility.DisplayDialog("Lesson 49 Done!",
            "Oluşturulan/bağlanan yapı:\n" +
            "• Worker UI Container prefab → UIWorkerContainer script eklendi\n" +
            "• 5 adet WorkerDataSO (Data/Workers/): Angelo, Kai, Jawed, Matteo, Ethan\n" +
            "• HR Manager → prefab, parent (Content), workerDatas[] bağlandı\n" +
            "• Content içindeki eski önizleme kartı temizlendi\n\n" +
            "⚡ Play'e bas, HR Desk'e git, panel içinde 5 worker kartı görünmeli.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 48 (HR Manager + Desk Station)")]
    public static void SetupLesson48()
    {
        var scene = EditorSceneManager.GetActiveScene();

        GameObject hrCanvas = GameObject.Find("HR Canvas");
        if (hrCanvas == null)
        {
            EditorUtility.DisplayDialog("Error", "HR Canvas not found!\nRun Setup Lesson 47 first.", "OK");
            return;
        }

        // HR Desk may be inactive (nested inside a not-yet-unlocked Office Zone), so
        // GameObject.Find (which skips inactive objects) won't find it — search everything.
        GameObject hrDesk = null;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == "HR Desk")
            {
                hrDesk = go;
                break;
            }
        }

        if (hrDesk == null)
        {
            EditorUtility.DisplayDialog("Error", "HR Desk not found!\nRun Setup Lesson 45 first.", "OK");
            return;
        }

        // 1. Canvas Group on HR Canvas
        CanvasGroup cg = hrCanvas.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = hrCanvas.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // HR Canvas GameObject itself should stay active (CanvasGroup handles visibility)
        hrCanvas.SetActive(true);

        // 2. HR Manager GameObject under MANAGERS
        GameObject hrManagerObj = GameObject.Find("HR Manager");
        if (hrManagerObj == null)
        {
            hrManagerObj = new GameObject("HR Manager");
            hrManagerObj.transform.position = Vector3.zero;

            GameObject managers = GameObject.Find("--- MANAGERS ---");
            if (managers != null)
                hrManagerObj.transform.SetParent(managers.transform);

            Undo.RegisterCreatedObjectUndo(hrManagerObj, "Create HR Manager");
        }

        HRManager hrManager = hrManagerObj.GetComponent<HRManager>();
        if (hrManager == null)
            hrManager = hrManagerObj.AddComponent<HRManager>();

        SetSerializedFieldObject(hrManagerObj, "HRManager", "cg", cg);

        // 3. Desk Station on HR Desk
        DeskStation deskStation = hrDesk.GetComponent<DeskStation>();
        if (deskStation == null)
            deskStation = hrDesk.AddComponent<DeskStation>();

        SetSerializedFieldObject(hrDesk, "DeskStation", "hrManager", hrManager);

        // 4. Wire Back Button -> HRManager.Hide
        Transform backButtonT = hrCanvas.transform.Find("Back Button");
        if (backButtonT != null && backButtonT.TryGetComponent(out Button backButton))
        {
            UnityEventTools.AddPersistentListener(backButton.onClick, hrManager.Hide);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 48: HR Manager + Desk Station wired!");
        EditorUtility.DisplayDialog("Lesson 48 Done!",
            "Oluşturulan/bağlanan yapı:\n" +
            "• HR Canvas → Canvas Group eklendi (başlangıçta gizli)\n" +
            "• HR Manager (MANAGERS altında) → Canvas Group referansı bağlandı\n" +
            "• HR Desk → Desk Station script + HR Manager referansı\n" +
            "• Back Button → onClick → HRManager.Hide() bağlandı\n\n" +
            "⚡ Play'e bas, HR Desk'e yaklaş ve dur — panel açılmalı.\n" +
            "⚡ Sol üstteki çarpı butonuna bas — panel kapanmalı.",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 46 (Office Zone Progression Steps)")]
    public static void SetupLesson46()
    {
        var scene = EditorSceneManager.GetActiveScene();

        ProgressionManager pm = Object.FindFirstObjectByType<ProgressionManager>();
        if (pm == null)
        {
            EditorUtility.DisplayDialog("Error", "Progression Manager not found in scene!\nRun Setup Lesson 42 first.", "OK");
            return;
        }

        GameObject officeZoneLE = null;
        GameObject hrLE = null;
        GameObject playerUpgradesLE = null;

        // HR LE / Player Upgrades LE are nested inside Office Zone LE's Unlocked Elements,
        // which starts inactive — GameObject.Find-style active-only search would miss them.
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == "Office Zone LE") officeZoneLE = go;
            else if (go.name == "HR LE") hrLE = go;
            else if (go.name == "Player Upgrades LE") playerUpgradesLE = go;
        }

        SerializedObject pmSo = new SerializedObject(pm);
        SerializedProperty stepsArr = pmSo.FindProperty("progressionSteps");

        int existingCount = stepsArr.arraySize;
        stepsArr.arraySize = existingCount + 3;

        AddProgressionStep(stepsArr, existingCount, "Office Zone", officeZoneLE);
        AddProgressionStep(stepsArr, existingCount + 1, "HR Office", hrLE);
        AddProgressionStep(stepsArr, existingCount + 2, "Player Upgrades Office", playerUpgradesLE);

        pmSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string found = "";
        found += officeZoneLE != null ? "✓ Office Zone LE\n" : "✗ Office Zone LE (assign manually)\n";
        found += hrLE != null ? "✓ HR LE\n" : "✗ HR LE (assign manually)\n";
        found += playerUpgradesLE != null ? "✓ Player Upgrades LE\n" : "✗ Player Upgrades LE (assign manually)\n";

        Debug.Log("✅ Lesson 46: Office Zone progression steps added!");
        EditorUtility.DisplayDialog("Lesson 46 Done!",
            "Progression Manager'a 3 yeni step eklendi:\n" +
            found +
            "\n⚡ Eksik LE'leri inspector'dan manuel ata.\n" +
            "⚡ Office Zone LE'nin Unlocked Callback'ine sağ duvarı SetActive(false) olarak bağla.\n" +
            "⚡ Tools > Clear Save → Play ile test et.",
            "OK");
    }

    private static void AddProgressionStep(SerializedProperty stepsArr, int index, string stepName, GameObject le)
    {
        SerializedProperty step = stepsArr.GetArrayElementAtIndex(index);
        step.FindPropertyRelative("name").stringValue = stepName;
        SerializedProperty elements = step.FindPropertyRelative("lockedElements");

        if (le != null && le.TryGetComponent(out LockedElement leComp))
        {
            elements.arraySize = 1;
            elements.GetArrayElementAtIndex(0).objectReferenceValue = leComp;
        }
        else
        {
            elements.arraySize = 0;
        }
    }

    [MenuItem("Cooked Fast/Setup Lesson 45 (Office Zone Structure)")]
    public static void SetupLesson45()
    {
        var scene = EditorSceneManager.GetActiveScene();

        if (GameObject.Find("Office Zone") != null)
        {
            EditorUtility.DisplayDialog("Warning", "Office Zone already exists in scene!", "OK");
            return;
        }

        GameObject gameplay = GameObject.Find("--- GAMEPLAY ---");

        // 1. Office Zone root
        GameObject officeZone = new GameObject("Office Zone");
        officeZone.transform.position = Vector3.zero;
        if (gameplay != null)
            officeZone.transform.SetParent(gameplay.transform);
        Undo.RegisterCreatedObjectUndo(officeZone, "Create Office Zone");

        // 2. Walls container
        GameObject walls = new GameObject("Walls");
        walls.transform.SetParent(officeZone.transform);
        walls.transform.localPosition = Vector3.zero;

        // 3. Office Ground placeholder
        GameObject officeGround = new GameObject("Office Ground");
        officeGround.transform.SetParent(officeZone.transform);
        officeGround.transform.localPosition = new Vector3(0f, 0.005f, 0f);

        // 4. HR Elements
        GameObject hrElements = new GameObject("HR Elements");
        hrElements.transform.SetParent(officeZone.transform);
        hrElements.transform.localPosition = Vector3.zero;

        // HR Desk trigger placeholder
        GameObject hrDesk = new GameObject("HR Desk");
        hrDesk.transform.SetParent(hrElements.transform);
        hrDesk.transform.localPosition = Vector3.zero;

        BoxCollider hrTrigger = hrDesk.AddComponent<BoxCollider>();
        hrTrigger.isTrigger = true;
        hrTrigger.size = new Vector3(1.5f, 2f, 1.5f);
        hrTrigger.center = new Vector3(0f, 1f, 0f);

        // 5. Player Upgrades Elements
        GameObject playerElements = new GameObject("Player Upgrades Elements");
        playerElements.transform.SetParent(officeZone.transform);
        playerElements.transform.localPosition = Vector3.zero;

        GameObject playerDesk = new GameObject("Player Upgrades Desk");
        playerDesk.transform.SetParent(playerElements.transform);
        playerDesk.transform.localPosition = Vector3.zero;

        BoxCollider playerTrigger = playerDesk.AddComponent<BoxCollider>();
        playerTrigger.isTrigger = true;
        playerTrigger.size = new Vector3(1.5f, 2f, 1.5f);
        playerTrigger.center = new Vector3(0f, 1f, 0f);

        // 6. Disable office zone (starts locked)
        officeZone.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 45: Office Zone structure created!");
        EditorUtility.DisplayDialog("Lesson 45 Done!",
            "Oluşturulan yapı:\n" +
            "• Office Zone (disabled)\n" +
            "  ├ Walls (wall modellerini buraya taşı)\n" +
            "  ├ Office Ground (konumlandır + ölçeklendir)\n" +
            "  ├ HR Elements\n" +
            "  │  └ HR Desk (trigger BoxCollider)\n" +
            "  └ Player Upgrades Elements\n" +
            "     └ Player Upgrades Desk (trigger BoxCollider)\n\n" +
            "⚡ Manuel yapman gerekenler:\n" +
            "1. Sağ duvarı seç → NavMesh Obstacle (carve) ekle\n" +
            "2. NavMesh Surface → Bake\n" +
            "3. Sağ duvarı devre dışı bırak\n" +
            "4. Walls altına ofis duvarlarını ekle (sağ duvarı duplicate)\n" +
            "5. Office Ground'u konumlandır ve ölçeklendir\n" +
            "6. HR/Player masalarına model ekle (Prefabs/Models'dan desk)\n" +
            "7. Worker'ları unpack et, gereksiz component'ları kaldır\n" +
            "8. Manager Animator Controller oluştur (Sit anim)\n" +
            "9. Manager materyali oluştur (kahverengi)",
            "OK");
    }

    [MenuItem("Cooked Fast/Setup Lesson 44 (Progression Canvas)")]
    public static void SetupLesson44()
    {
        var scene = EditorSceneManager.GetActiveScene();

        if (GameObject.Find("Progression Canvas") != null)
        {
            EditorUtility.DisplayDialog("Warning", "Progression Canvas already exists in scene!", "OK");
            return;
        }

        // Create Progression Canvas
        GameObject canvasObj = new GameObject("Progression Canvas");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f); // portrait, matches Main Canvas
        scaler.matchWidthOrHeight = 0f; // match width so wide content (cards, buttons) never gets cropped

        canvasObj.AddComponent<GraphicRaycaster>();

        // Put under UI section if exists
        GameObject uiParent = GameObject.Find("--- UI ---");
        if (uiParent != null)
            canvasObj.transform.SetParent(uiParent.transform);

        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Progression Canvas");

        // Create blocker panel (invisible, blocks input)
        GameObject panel = new GameObject("Blocker");
        panel.transform.SetParent(canvasObj.transform);

        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0f);
        panelImg.raycastTarget = true;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 44: Progression Canvas created!");
        EditorUtility.DisplayDialog("Lesson 44 Done!",
            "Created:\n" +
            "• Progression Canvas (Sort Order 5, Scale With Screen Size)\n" +
            "  └ Blocker (transparent panel, Raycast Target=true)\n\n" +
            "⚡ Manuel yapman gerekenler:\n" +
            "1. Hierarchy'de Player Follow Camera'yı duplicate et\n" +
            "2. Yeni kameranın adını 'Progression Camera' yap\n" +
            "3. Tracking Target alanını temizle (None)\n" +
            "4. Progression Camera'yı devre dışı bırak (uncheck)\n" +
            "5. Main Camera → Cinemachine Brain → Default Blend: In/Out 0.5s\n" +
            "6. Progression Manager inspector'da:\n" +
            "   - Progression Camera → Progression Camera\n" +
            "   - Progression Canvas → Progression Canvas\n\n" +
            "⚡ Tools > Clear Save → Play → unlock et → kamera geçişini test et.",
            "OK");
    }

    private static GameObject CreateChair(GameObject chairPrefab, Material paletteMat, string name, Vector3 localPos, float yRotation)
    {
        // Chair parent (empty): holds script, collider, obstacle
        GameObject chair = new GameObject(name);
        chair.transform.localPosition = localPos;
        chair.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        chair.transform.localScale = Vector3.one;

        chair.AddComponent<Chair>();

        NavMeshObstacle obstacle = chair.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.size = new Vector3(0.7f, 1f, 0.7f);
        obstacle.center = new Vector3(0f, 0.5f, 0f);

        BoxCollider collider = chair.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.7f, 1f, 0.7f);
        collider.center = new Vector3(0f, 0.5f, 0f);

        // Chair render (visual model)
        if (chairPrefab != null)
        {
            GameObject render = Object.Instantiate(chairPrefab);
            render.name = "Chair Render";
            render.transform.SetParent(chair.transform);
            // x=0, y=0, z=0 relative to chair parent
            render.transform.localPosition = Vector3.zero;
            render.transform.localRotation = Quaternion.identity;

            // Remove mesh colliders (we use box collider on parent)
            MeshCollider[] meshColliders = render.GetComponentsInChildren<MeshCollider>();
            foreach (var mc in meshColliders)
                Object.DestroyImmediate(mc);
        }
        else
        {
            GameObject render = GameObject.CreatePrimitive(PrimitiveType.Cube);
            render.name = "Chair Render (PLACEHOLDER)";
            render.transform.SetParent(chair.transform);
            render.transform.localPosition = Vector3.zero;
            render.transform.localRotation = Quaternion.identity;
            render.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            Object.DestroyImmediate(render.GetComponent<BoxCollider>());
        }

        return chair;
    }

    // =====================
    // LESSON 56 - Worker base stats
    // =====================
    [MenuItem("Cooked Fast/Setup Lesson 56 (Worker Base Stats)")]
    public static void SetupLesson56()
    {
        // 1. Create the base stats asset
        string dataFolder = "Assets/Tiny Coffee Shop/Data";
        if (!AssetDatabase.IsValidFolder(dataFolder))
            AssetDatabase.CreateFolder("Assets/Tiny Coffee Shop", "Data");

        string baseStatsFolder = dataFolder + "/Base Stats";
        if (!AssetDatabase.IsValidFolder(baseStatsFolder))
            AssetDatabase.CreateFolder(dataFolder, "Base Stats");

        string assetPath = baseStatsFolder + "/Worker Base Stats.asset";
        BaseCharacterStatsSO baseStats = AssetDatabase.LoadAssetAtPath<BaseCharacterStatsSO>(assetPath);

        if (baseStats == null)
        {
            baseStats = ScriptableObject.CreateInstance<BaseCharacterStatsSO>();
            AssetDatabase.CreateAsset(baseStats, assetPath);

            // Only fill the values on creation, so re-running never overwrites
            // whatever the designer tuned in the Inspector
            SerializedObject statsSO = new SerializedObject(baseStats);
            statsSO.FindProperty("speed").floatValue = 2f;      // NavMeshAgent default
            statsSO.FindProperty("capacity").intValue = 7;
            statsSO.FindProperty("revenue").floatValue = 1f;
            statsSO.ApplyModifiedProperties();
        }

        AssetDatabase.SaveAssets();

        // 2. Add CharacterStats to the Worker prefab and reference the asset
        string workerPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/Characters/Worker.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(workerPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Error", "Worker.prefab not found at\n" + workerPrefabPath, "OK");
            return;
        }

        GameObject workerRoot = PrefabUtility.LoadPrefabContents(workerPrefabPath);

        CharacterStats characterStats = workerRoot.GetComponent<CharacterStats>();
        if (characterStats == null)
            characterStats = workerRoot.AddComponent<CharacterStats>();

        SerializedObject cs = new SerializedObject(characterStats);
        cs.FindProperty("baseStats").objectReferenceValue = baseStats;
        cs.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(workerRoot, workerPrefabPath);
        PrefabUtility.UnloadPrefabContents(workerRoot);

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Lesson 56",
            "Worker Base Stats created (speed 2, capacity 7, revenue 1)\n" +
            "CharacterStats added to Worker.prefab and wired.",
            "OK");
    }

    // =====================
    // LESSON 57 - Player base stats
    // =====================
    // FoodServingStation now refuses to serve anything without a CharacterStats
    // component, so the player needs one too or they can no longer serve
    [MenuItem("Cooked Fast/Setup Lesson 57 (Player Base Stats)")]
    public static void SetupLesson57()
    {
        var scene = EditorSceneManager.GetActiveScene();

        string baseStatsFolder = "Assets/Tiny Coffee Shop/Data/Base Stats";
        if (!AssetDatabase.IsValidFolder(baseStatsFolder))
        {
            EditorUtility.DisplayDialog("Error", "Base Stats folder not found!\nRun Setup Lesson 56 first.", "OK");
            return;
        }

        string assetPath = baseStatsFolder + "/Player Base Stats.asset";
        BaseCharacterStatsSO baseStats = AssetDatabase.LoadAssetAtPath<BaseCharacterStatsSO>(assetPath);

        if (baseStats == null)
        {
            baseStats = ScriptableObject.CreateInstance<BaseCharacterStatsSO>();
            AssetDatabase.CreateAsset(baseStats, assetPath);

            SerializedObject statsSO = new SerializedObject(baseStats);
            statsSO.FindProperty("speed").floatValue = 2f;
            statsSO.FindProperty("capacity").intValue = 7;
            statsSO.FindProperty("revenue").floatValue = 1f;
            statsSO.ApplyModifiedProperties();
        }

        AssetDatabase.SaveAssets();

        GameObject player = null;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == "Player" && go.GetComponent<PlayerDetector>() != null)
            {
                player = go;
                break;
            }
        }

        if (player == null)
        {
            EditorUtility.DisplayDialog("Error", "Player (with PlayerDetector) not found in the scene!", "OK");
            return;
        }

        CharacterStats characterStats = player.GetComponent<CharacterStats>();
        if (characterStats == null)
            characterStats = player.AddComponent<CharacterStats>();

        SerializedObject cs = new SerializedObject(characterStats);
        cs.FindProperty("baseStats").objectReferenceValue = baseStats;
        cs.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog("Lesson 57",
            "Player Base Stats created and CharacterStats added to the scene Player.",
            "OK");
    }

    // =====================
    // LESSON 59 - Player Upgrades Canvas (UI only, no logic yet)
    // =====================
    [MenuItem("Cooked Fast/Setup Lesson 59 (Player Upgrades Canvas)")]
    public static void SetupLesson59()
    {
        var scene = EditorSceneManager.GetActiveScene();

        if (GameObject.Find("Player Upgrades Canvas") != null)
        {
            EditorUtility.DisplayDialog("Warning", "Player Upgrades Canvas already exists in scene!", "OK");
            return;
        }

        Sprite square50 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design Toolbox/Sprites/Tabsil/Square_50.png");
        Sprite crossIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Design Toolbox/Sprites/Heathen Engineering/Icons/Free Flat Button Solid Cross Icon.png");

        string containerPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/UI/UI Player Upgrade Container.prefab";
        GameObject containerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(containerPrefabPath);

        // The course resizes the container prefab to 260x450 so three of them
        // fit side by side on a phone
        if (containerPrefab != null)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(containerPrefabPath);
            RectTransform prefabRect = prefabRoot.GetComponent<RectTransform>();
            if (prefabRect != null)
                prefabRect.sizeDelta = new Vector2(260f, 450f);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, containerPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        GameObject uiParent = GameObject.Find("--- UI ---");

        // 1. CANVAS
        GameObject canvasObj = new GameObject("Player Upgrades Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Hidden by default; Lesson 60 wires the show/hide logic to this group.
        // The GameObject stays active so the CanvasGroup is the single switch
        CanvasGroup cg = canvasObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        if (uiParent != null)
            canvasObj.transform.SetParent(uiParent.transform);

        Undo.RegisterCreatedObjectUndo(canvasObj, "Create Player Upgrades Canvas");

        // 2. PANEL - full screen dark overlay
        GameObject panel = new GameObject("PANEL");
        panel.transform.SetParent(canvasObj.transform);
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.6f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 3. CONTAINER - bottom sheet. Image stays enabled but fully
        // transparent: it is only there to be a raycast target
        GameObject container = new GameObject("Container");
        container.transform.SetParent(panel.transform);
        Image containerImg = container.AddComponent<Image>();
        containerImg.color = new Color(0f, 0f, 0f, 0f);

        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(0f, 800f);

        VerticalLayoutGroup containerLayout = container.AddComponent<VerticalLayoutGroup>();
        containerLayout.spacing = 0f;
        containerLayout.childAlignment = TextAnchor.UpperCenter;
        containerLayout.childControlWidth = true;
        containerLayout.childControlHeight = true;
        containerLayout.childForceExpandWidth = true;
        containerLayout.childForceExpandHeight = false;

        // 4. TOP RIBBON
        GameObject topRibbon = new GameObject("Top Ribbon");
        topRibbon.transform.SetParent(container.transform);
        Image ribbonImg = topRibbon.AddComponent<Image>();
        if (square50 != null)
        {
            ribbonImg.sprite = square50;
            ribbonImg.type = Image.Type.Sliced;
        }
        ribbonImg.color = new Color(0.72f, 0.28f, 0.28f, 1f);

        LayoutElement ribbonLayout = topRibbon.AddComponent<LayoutElement>();
        ribbonLayout.preferredHeight = 140f;
        ribbonLayout.flexibleHeight = 0f;

        GameObject titleText = new GameObject("Title Text");
        titleText.transform.SetParent(topRibbon.transform);
        TextMeshProUGUI title = titleText.AddComponent<TextMeshProUGUI>();
        title.text = "PLAYER";
        title.fontSize = 64f;
        title.alignment = TextAlignmentOptions.Center;

        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(160f, 0f);
        titleRect.offsetMax = new Vector2(-160f, 0f);

        // 5. CLOSE BUTTON - no callback yet, that comes with the logic lesson
        GameObject closeButton = new GameObject("Close Button");
        closeButton.transform.SetParent(topRibbon.transform);
        Image closeImg = closeButton.AddComponent<Image>();
        if (square50 != null)
        {
            closeImg.sprite = square50;
            closeImg.type = Image.Type.Sliced;
        }
        closeImg.color = new Color(0.85f, 0.25f, 0.25f, 1f);
        closeButton.AddComponent<Button>();

        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-20f, 0f);
        closeRect.sizeDelta = new Vector2(100f, 100f);

        GameObject closeIcon = new GameObject("Icon");
        closeIcon.transform.SetParent(closeButton.transform);
        Image closeIconImg = closeIcon.AddComponent<Image>();
        if (crossIcon != null) closeIconImg.sprite = crossIcon;
        closeIconImg.preserveAspect = true;

        RectTransform closeIconRect = closeIcon.GetComponent<RectTransform>();
        closeIconRect.anchorMin = Vector2.zero;
        closeIconRect.anchorMax = Vector2.one;
        closeIconRect.offsetMin = new Vector2(22f, 22f);
        closeIconRect.offsetMax = new Vector2(-22f, -22f);

        // 6. UPGRADE BUTTONS CONTAINER - the three stat cards get spawned in
        // here at runtime, exactly like the worker containers
        GameObject upgradeButtonsContainer = new GameObject("Upgrade Buttons Container");
        upgradeButtonsContainer.transform.SetParent(container.transform);
        upgradeButtonsContainer.AddComponent<RectTransform>();

        LayoutElement buttonsLayout = upgradeButtonsContainer.AddComponent<LayoutElement>();
        buttonsLayout.flexibleHeight = 1f;

        HorizontalLayoutGroup hlGroup = upgradeButtonsContainer.AddComponent<HorizontalLayoutGroup>();
        hlGroup.spacing = 50f;
        hlGroup.childAlignment = TextAnchor.MiddleCenter;
        // Cards keep their own 260x450 size: expanding them looks wrong on tablets
        hlGroup.childControlWidth = false;
        hlGroup.childControlHeight = false;
        hlGroup.childForceExpandWidth = false;
        hlGroup.childForceExpandHeight = false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 59: Player Upgrades Canvas created!");
        EditorUtility.DisplayDialog("Lesson 59 Done!",
            "Player Upgrades Canvas (CanvasGroup hidden)\n" +
            "  └ PANEL (dark overlay)\n" +
            "     └ Container (bottom sheet, transparent raycast target)\n" +
            "        ├ Top Ribbon (Title Text + Close Button)\n" +
            "        └ Upgrade Buttons Container (Horizontal Layout, spacing 50)\n\n" +
            "UI Player Upgrade Container prefab resized to 260x450.\n" +
            "Cards are spawned at runtime in Lesson 60 - none placed now.",
            "OK");
    }

    // =====================
    // LESSON 60 - Upgrade Desk Station + container spawning
    // =====================
    [MenuItem("Cooked Fast/Setup Lesson 60 (Upgrade Desk Station)")]
    public static void SetupLesson60()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // 1. Script on the container prefab, with its title and icon wired
        string containerPrefabPath = "Assets/Tiny Coffee Shop/Prefabs/UI/UI Player Upgrade Container.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(containerPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Error", "UI Player Upgrade Container.prefab not found!", "OK");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(containerPrefabPath);

        UIPlayerUpgradeContainer containerComp = prefabRoot.GetComponent<UIPlayerUpgradeContainer>();
        if (containerComp == null)
            containerComp = prefabRoot.AddComponent<UIPlayerUpgradeContainer>();

        Transform titleSection = FindDeepChild(prefabRoot.transform, "Upgrade Title Section");
        Transform iconSection = FindDeepChild(prefabRoot.transform, "Icon Section");

        SerializedObject containerSO = new SerializedObject(containerComp);

        if (titleSection != null)
        {
            TextMeshProUGUI titleTmp = titleSection.GetComponentInChildren<TextMeshProUGUI>(true);
            if (titleTmp != null)
                containerSO.FindProperty("titleText").objectReferenceValue = titleTmp;
        }

        if (iconSection != null)
        {
            Image iconImg = iconSection.GetComponentInChildren<Image>(true);
            if (iconImg != null)
                containerSO.FindProperty("iconImage").objectReferenceValue = iconImg;
        }

        containerSO.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, containerPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        GameObject containerPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(containerPrefabPath);

        // 2. Player base stats: the course makes the player a bit faster than
        // the workers and more generous on revenue. Icons stay for the user
        BaseCharacterStatsSO playerBaseStats = AssetDatabase.LoadAssetAtPath<BaseCharacterStatsSO>(
            "Assets/Tiny Coffee Shop/Data/Base Stats/Player Base Stats.asset");

        if (playerBaseStats == null)
        {
            EditorUtility.DisplayDialog("Error", "Player Base Stats.asset not found!\nRun Setup Lesson 57 first.", "OK");
            return;
        }

        SerializedObject statsSO = new SerializedObject(playerBaseStats);
        statsSO.FindProperty("speed").floatValue = 3f;
        statsSO.FindProperty("capacity").intValue = 7;
        statsSO.FindProperty("revenue").floatValue = 1.5f;
        statsSO.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        // 3. Canvas sort order 2, so it wins over the HR canvas if both ever open
        GameObject canvasObj = GameObject.Find("Player Upgrades Canvas");
        if (canvasObj == null)
        {
            EditorUtility.DisplayDialog("Error", "Player Upgrades Canvas not found!\nRun Setup Lesson 59 first.", "OK");
            return;
        }

        canvasObj.GetComponent<Canvas>().sortingOrder = 2;
        CanvasGroup panelGroup = canvasObj.GetComponent<CanvasGroup>();

        Transform buttonsParent = FindDeepChild(canvasObj.transform, "Upgrade Buttons Container");
        Transform closeButtonT = FindDeepChild(canvasObj.transform, "Close Button");

        // 4. The station itself, on the desk that already has the trigger collider
        GameObject desk = null;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == "Player Upgrades Desk")
            {
                desk = go;
                break;
            }
        }

        if (desk == null)
        {
            EditorUtility.DisplayDialog("Error", "Player Upgrades Desk not found in the scene!", "OK");
            return;
        }

        UpgradeDeskStation station = desk.GetComponent<UpgradeDeskStation>();
        if (station == null)
            station = desk.AddComponent<UpgradeDeskStation>();

        BoxCollider deskCollider = desk.GetComponent<BoxCollider>();
        if (deskCollider == null)
        {
            deskCollider = desk.AddComponent<BoxCollider>();
            deskCollider.size = new Vector3(2f, 2f, 2f);
        }
        deskCollider.isTrigger = true;

        SerializedObject stationSO = new SerializedObject(station);
        stationSO.FindProperty("upgradePanel").objectReferenceValue = panelGroup;
        stationSO.FindProperty("upgradeContainerPrefab").objectReferenceValue =
            containerPrefabAsset != null ? containerPrefabAsset.GetComponent<UIPlayerUpgradeContainer>() : null;
        if (buttonsParent != null)
            stationSO.FindProperty("upgradeContainersParent").objectReferenceValue = buttonsParent;
        stationSO.FindProperty("playerBaseStats").objectReferenceValue = playerBaseStats;
        stationSO.ApplyModifiedProperties();

        // 5. Close button finally gets its callback
        if (closeButtonT != null && closeButtonT.TryGetComponent(out Button closeButton))
        {
            for (int i = closeButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(closeButton.onClick, i);

            UnityEventTools.AddPersistentListener(closeButton.onClick, station.Hide);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("✅ Lesson 60: Upgrade Desk Station wired!");
        EditorUtility.DisplayDialog("Lesson 60 Done!",
            "UpgradeDeskStation added to Player Upgrades Desk and wired to:\n" +
            "  • Player Upgrades Canvas CanvasGroup\n" +
            "  • UI Player Upgrade Container prefab\n" +
            "  • Upgrade Buttons Container\n" +
            "  • Player Base Stats (speed 3, capacity 7, revenue 1.5)\n\n" +
            "Canvas sort order set to 2, Close Button calls Hide().\n\n" +
            "STILL TO DO BY HAND: assign Speed/Capacity/Revenue icons on the\n" +
            "Player Base Stats asset - the cards spawn without art otherwise.",
            "OK");
    }

    // =====================
    // HELPERS
    // =====================
    private static void SetSerializedFieldObject(GameObject obj, string componentType, string fieldName, Object value)
    {
        foreach (var comp in obj.GetComponents<Component>())
        {
            if (comp.GetType().Name == componentType)
            {
                SerializedObject so = new SerializedObject(comp);
                SerializedProperty prop = so.FindProperty(fieldName);
                if (prop != null)
                {
                    prop.objectReferenceValue = value;
                    so.ApplyModifiedProperties();
                }
                return;
            }
        }
    }

    private static void SetSerializedFieldVector2Int(GameObject obj, string componentType, string fieldName, Vector2Int value)
    {
        foreach (var comp in obj.GetComponents<Component>())
        {
            if (comp.GetType().Name == componentType)
            {
                SerializedObject so = new SerializedObject(comp);
                SerializedProperty prop = so.FindProperty(fieldName);
                if (prop != null)
                {
                    prop.vector2IntValue = value;
                    so.ApplyModifiedProperties();
                }
                return;
            }
        }
    }

    private static void SetSerializedFieldVector3(GameObject obj, string componentType, string fieldName, Vector3 value)
    {
        foreach (var comp in obj.GetComponents<Component>())
        {
            if (comp.GetType().Name == componentType)
            {
                SerializedObject so = new SerializedObject(comp);
                SerializedProperty prop = so.FindProperty(fieldName);
                if (prop != null)
                {
                    prop.vector3Value = value;
                    so.ApplyModifiedProperties();
                }
                return;
            }
        }
    }

    private static void SetSerializedFieldInt(GameObject obj, string componentType, string fieldName, int value)
    {
        foreach (var comp in obj.GetComponents<Component>())
        {
            if (comp.GetType().Name == componentType)
            {
                SerializedObject so = new SerializedObject(comp);
                SerializedProperty prop = so.FindProperty(fieldName);
                if (prop != null)
                {
                    prop.intValue = value;
                    so.ApplyModifiedProperties();
                }
                return;
            }
        }
    }
}
