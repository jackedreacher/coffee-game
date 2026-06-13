using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEditor;
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

        // 2. Setup Locked Element in scene (search everywhere)
        GameObject lockedElement = null;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
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
