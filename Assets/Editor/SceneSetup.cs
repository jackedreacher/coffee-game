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

        // Try to find chair model - search common import paths
        GameObject chairModel = FindChairModel();

        // =====================
        // 1. SIMPLE TABLE SET (root)
        // =====================
        GameObject tableSet = new GameObject("Simple Table Set");
        tableSet.transform.position = new Vector3(3f, 0f, 2f);
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
        // 2. ROUND TABLE (child of table set)
        // =====================
        if (roundTableModel != null)
        {
            GameObject roundTable = (GameObject)PrefabUtility.InstantiatePrefab(roundTableModel);
            roundTable.name = "Round Table";
            roundTable.transform.SetParent(tableSet.transform);
            roundTable.transform.localPosition = Vector3.zero;
            roundTable.transform.localRotation = Quaternion.identity;

            if (paletteMat != null)
            {
                Renderer[] renderers = roundTable.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                    r.sharedMaterial = paletteMat;
            }

            // Plateau as child of Round Table
            if (plateauPrefab != null)
            {
                GameObject plateau = (GameObject)PrefabUtility.InstantiatePrefab(plateauPrefab);
                plateau.name = "Plateau";
                plateau.transform.SetParent(roundTable.transform);
                plateau.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                plateau.transform.localRotation = Quaternion.identity;

                SetSerializedFieldInt(plateau, "Plateau", "maxCapacity", 20);
            }
        }

        // =====================
        // 3. CHAIR 1 (z = -0.67)
        // =====================
        GameObject chair1 = CreateChair(chairModel, paletteMat, "Chair", new Vector3(0f, 0f, -0.67f), 0f);
        chair1.transform.SetParent(tableSet.transform);

        // =====================
        // 4. CHAIR 2 (z = +0.67, rotated 180)
        // =====================
        GameObject chair2 = CreateChair(chairModel, paletteMat, "Chair", new Vector3(0f, 0f, 0.67f), 180f);
        chair2.transform.SetParent(tableSet.transform);

        // =====================
        // 5. SAVE AS PREFAB
        // =====================
        string prefabFolder = "Assets/Tiny Coffee Shop/Prefabs/TableStuff";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Tiny Coffee Shop/Prefabs", "TableStuff");
        }

        // Save Chair prefab (from chair1)
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            chair1, prefabFolder + "/Chair.prefab", InteractionMode.AutomatedAction);

        // Save Table Set prefab
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

        string chairStatus = chairModel != null
            ? "Chair model found and used."
            : "⚠️ Chair model NOT found! Placeholder cubes used.\nImport 'Lounge Chair' from Furniture Free asset, then re-run or replace manually.";

        Debug.Log("✅ Lesson 13: Table Set + Table Manager created!");
        EditorUtility.DisplayDialog("Lesson 13 Done!",
            "Created:\n" +
            "• Simple Table Set (Round Table + Plateau + 2 Chairs)\n" +
            "• Chair prefab (NavMeshObstacle + BoxCollider)\n" +
            "• Table Manager\n" +
            "• Prefabs saved to Prefabs/TableStuff/\n\n" +
            chairStatus + "\n\n" +
            "⚡ Adjust table position as needed.\n" +
            "⚡ Re-bake NavMesh after positioning.",
            "OK");
    }

    private static GameObject CreateChair(GameObject chairModel, Material paletteMat, string name, Vector3 localPos, float yRotation)
    {
        GameObject chair = new GameObject(name);
        chair.transform.localPosition = localPos;
        chair.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        chair.AddComponent<Chair>();

        NavMeshObstacle obstacle = chair.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.size = new Vector3(0.7f, 1f, 0.7f);
        obstacle.center = new Vector3(0f, 0.5f, 0f);

        BoxCollider collider = chair.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.7f, 1f, 0.7f);
        collider.center = new Vector3(0f, 0.5f, 0f);

        GameObject render;
        if (chairModel != null)
        {
            render = (GameObject)PrefabUtility.InstantiatePrefab(chairModel);
            render.name = "Chair Render";

            if (paletteMat != null)
            {
                Renderer[] renderers = render.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                    r.sharedMaterial = paletteMat;
            }

            // Remove mesh collider if present (use box collider instead)
            MeshCollider[] meshColliders = render.GetComponentsInChildren<MeshCollider>();
            foreach (var mc in meshColliders)
                Object.DestroyImmediate(mc);
        }
        else
        {
            render = GameObject.CreatePrimitive(PrimitiveType.Cube);
            render.name = "Chair Render (PLACEHOLDER)";
            render.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            Object.DestroyImmediate(render.GetComponent<BoxCollider>());
        }

        render.transform.SetParent(chair.transform);
        render.transform.localPosition = Vector3.zero;
        render.transform.localRotation = Quaternion.identity;

        return chair;
    }

    private static GameObject FindChairModel()
    {
        // Search common paths where Furniture Free asset might be imported
        string[] possiblePaths = new string[]
        {
            "Assets/Furniture Free/Prefabs/Lounge Chair.prefab",
            "Assets/Furniture Free/Models/Lounge_Chair_001.fbx",
            "Assets/Furniture Free/Prefabs/lounge_chair.prefab",
            "Assets/Tiny Coffee Shop/Models/Lounge_Chair.fbx",
            "Assets/Tiny Coffee Shop/Models/Lounge_Chair_001.fbx",
        };

        foreach (string path in possiblePaths)
        {
            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (obj != null) return obj;
        }

        // Fallback: search by name in entire project
        string[] guids = AssetDatabase.FindAssets("lounge chair t:GameObject");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return null;
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
