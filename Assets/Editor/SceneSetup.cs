using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;

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
