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
