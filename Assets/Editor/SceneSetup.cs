using UnityEngine;
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
