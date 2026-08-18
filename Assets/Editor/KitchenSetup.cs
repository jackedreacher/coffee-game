using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

// One-shot wiring for the hand-serving kitchen scene. Everything here is
// something you would otherwise click through in the Inspector
public static class KitchenSetup
{
    private const string playerBaseStatsPath =
        "Assets/Tiny Coffee Shop/Data/Base Stats/Player Base Stats.asset";

    // How much food a source station keeps on its tray before it stops making
    // more. Bump this if the stations run dry too fast
    private const int spawnerCapacity = 3;

#if COOKED_FAST_SETUP
    [MenuItem("Cooked Fast/Arac/Setup Kitchen Scene")]
#endif
    public static void SetupKitchenScene()
    {
        string report = "";

        report += SetupPlayerBaseStats();
        report += SetupCustomerQueue();
        report += SetupDropZones();
        report += SetupSpawnerStations();
        report += SetupCustomerCollider();
        report += DisableAutoServing();
        report += DisableStatUpgrades();
        report += SetupPlayer();
        report += SetupCamera();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("Kitchen setup:\n" + report);
        EditorUtility.DisplayDialog("Kitchen Setup", report, "Tamam");
    }

    // Tapping a customer can only walk the player over if something drives
    // movement by destination. The joystick controller cannot, so this swaps
    // the player onto the NavMeshAgent based controller
    [MenuItem("Cooked Fast/Arac/Switch Player To Click To Move")]
    public static void SwitchPlayerToClickToMove()
    {
        PlayerController[] controllers = FindAll<PlayerController>();

        if (controllers.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata", "Sahnede PlayerController yok", "Tamam");
            return;
        }

        GameObject playerObject = controllers[0].gameObject;
        string report = BakeNavMesh();

        float moveSpeed = new SerializedObject(controllers[0]).FindProperty("moveSpeed").floatValue;

        NavMeshAgent agent = playerObject.GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            agent = Undo.AddComponent<NavMeshAgent>(playerObject);
            report += "- NavMeshAgent eklendi\n";
        }

        Undo.RecordObject(agent, "Configure agent");
        agent.enabled = true;
        agent.speed = moveSpeed > 0 ? moveSpeed : 3f;
        agent.radius = .3f;
        agent.height = 1.8f;
        agent.stoppingDistance = .1f;
        agent.angularSpeed = 0f;
        agent.acceleration = 60f;
        EditorUtility.SetDirty(agent);

        ClickToMovePlayerController clickToMove = playerObject.GetComponent<ClickToMovePlayerController>();

        if (clickToMove == null)
        {
            clickToMove = Undo.AddComponent<ClickToMovePlayerController>(playerObject);
            report += "- ClickToMovePlayerController eklendi\n";
        }

        SerializedObject clickSo = new SerializedObject(clickToMove);
        clickSo.FindProperty("moveSpeed").floatValue = moveSpeed > 0 ? moveSpeed : 3f;
        clickSo.FindProperty("gameCamera").objectReferenceValue = Camera.main;
        clickSo.FindProperty("tapToServe").objectReferenceValue = playerObject.GetComponent<TapToServe>();
        clickSo.ApplyModifiedProperties();

        // Two PlayerControllers would both run the base Update
        foreach (PlayerController other in playerObject.GetComponents<PlayerController>())
        {
            if (other == clickToMove || !other.enabled)
                continue;

            Undo.RecordObject(other, "Disable old controller");
            other.enabled = false;
            EditorUtility.SetDirty(other);
            report += "- Kapatildi: " + other.GetType().Name + "\n";
        }

        // Deliberately left ENABLED. It is the player's only collider, and
        // without it PlayerDetector stops picking food up and TapToServe never
        // learns it is standing in a serving zone. It only moves when something
        // calls Move(), which the click-to-move controller never does

        // isOnNavMesh is meaningless in edit mode, the agent only binds in play.
        // Sampling the mesh around the player is the check that actually means
        // something here
        Vector3 playerPosition = playerObject.transform.position;

        if (NavMesh.SamplePosition(playerPosition, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            float offset = Vector3.Distance(playerPosition, navHit.position);

            if (offset > .1f)
            {
                Undo.RecordObject(playerObject.transform, "Snap player to navmesh");
                playerObject.transform.position = navHit.position;
                report += "- Player NavMesh'e oturtuldu (" + offset.ToString("0.00") + " birim kaydi)\n";
            }
            else
            {
                report += "- Player NavMesh uzerinde\n";
            }
        }
        else
        {
            report += "- UYARI: Player'in 2 birim cevresinde NavMesh yok.\n" +
                      "  Zemin objesi bake'e dahil mi kontrol et (NavMeshSurface > Collect Objects)\n";
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Click to move:\n" + report);
        EditorUtility.DisplayDialog("Click To Move", report, "Tamam");
    }

    // Swapping the player model can destroy the tray, and a replacement one
    // does not reconnect itself: three separate scripts hold a reference to it
    [MenuItem("Cooked Fast/Arac/Repair Player Plateau Links")]
    public static void RepairPlayerPlateauLinks()
    {
        PlayerController[] controllers = FindAll<PlayerController>();

        if (controllers.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata", "Sahnede PlayerController yok", "Tamam");
            return;
        }

        GameObject playerRoot = controllers[0].gameObject;
        Plateau[] plateaus = playerRoot.GetComponentsInChildren<Plateau>(true);

        if (plateaus.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata",
                "Player altinda Plateau yok.\n" +
                "Customer prefabindaki Plateau'yu kopyalayip Player'in altina koy, sonra tekrar calistir.",
                "Tamam");
            return;
        }

        Plateau plateau = plateaus[0];
        string report = "- Bulunan plateau: " + plateau.name + "\n";

        if (plateaus.Length > 1)
            report += "- UYARI: " + plateaus.Length + " plateau var, ilki kullanildi\n";

        report += Relink(playerRoot.GetComponent<HoldFoodAbility>(), "plateau", plateau);
        report += Relink(playerRoot.GetComponent<PlayerStatsHandler>(), "plateau", plateau);
        report += Relink(playerRoot.GetComponent<PlayerAnimator>(), "plateau", plateau.gameObject);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Plateau baglantilari:\n" + report);
        EditorUtility.DisplayDialog("Plateau", report, "Tamam");
    }

    private static string Relink(Component target, string fieldName, Object value)
    {
        if (target == null)
            return "";

        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(fieldName);

        if (property == null)
            return "- " + target.GetType().Name + ": " + fieldName + " alani yok\n";

        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();

        return "- " + target.GetType().Name + "." + fieldName + " baglandi\n";
    }

    // Puts the joystick back. Tapping a customer still works, the player just
    // has to walk over there themselves
    [MenuItem("Cooked Fast/Arac/Switch Player To Joystick")]
    public static void SwitchPlayerToJoystick()
    {
        PlayerController[] controllers = FindAll<PlayerController>();

        if (controllers.Length <= 0)
        {
            EditorUtility.DisplayDialog("Hata", "Sahnede PlayerController yok", "Tamam");
            return;
        }

        GameObject playerObject = controllers[0].gameObject;
        string report = "";

        foreach (PlayerController controller in playerObject.GetComponents<PlayerController>())
        {
            bool isJoystick = controller is JoystickPlayerController;

            if (controller.enabled == isJoystick)
                continue;

            Undo.RecordObject(controller, "Switch controller");
            controller.enabled = isJoystick;
            EditorUtility.SetDirty(controller);

            report += (isJoystick ? "- Acildi: " : "- Kapatildi: ") + controller.GetType().Name + "\n";
        }

        if (playerObject.TryGetComponent(out CharacterController characterController) &&
            !characterController.enabled)
        {
            Undo.RecordObject(characterController, "Enable character controller");
            characterController.enabled = true;
            EditorUtility.SetDirty(characterController);
            report += "- CharacterController acildi\n";
        }

        // Left in place but told to stay put, so tap-to-serve can still steer
        // it if you switch back later
        if (playerObject.TryGetComponent(out NavMeshAgent agent))
        {
            Undo.RecordObject(agent, "Stop agent");
            agent.enabled = false;
            EditorUtility.SetDirty(agent);
            report += "- NavMeshAgent kapatildi\n";
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Joystick:\n" + report);
        EditorUtility.DisplayDialog("Joystick", report, "Tamam");
    }

    // Unity 6 moved baking out of the Navigation window and onto the
    // NavMeshSurface component, which is easy to miss
    [MenuItem("Cooked Fast/Arac/Bake NavMesh")]
    public static void BakeNavMeshMenu()
    {
        string report = BakeNavMesh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Bake NavMesh", report, "Tamam");
    }

    private static string BakeNavMesh()
    {
        Unity.AI.Navigation.NavMeshSurface[] surfaces =
            FindAll<Unity.AI.Navigation.NavMeshSurface>();

        if (surfaces.Length <= 0)
            return "- UYARI: Sahnede NavMeshSurface yok\n";

        foreach (Unity.AI.Navigation.NavMeshSurface surface in surfaces)
        {
            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
        }

        AssetDatabase.SaveAssets();

        return "- NavMesh bake edildi (" + surfaces.Length + " surface)\n";
    }

    private static string SetupPlayerBaseStats()
    {
        BaseCharacterStatsSO stats =
            AssetDatabase.LoadAssetAtPath<BaseCharacterStatsSO>(playerBaseStatsPath);

        if (stats == null)
            return "- Player Base Stats BULUNAMADI (" + playerBaseStatsPath + ")\n";

        SerializedObject so = new SerializedObject(stats);
        so.FindProperty("capacity").intValue = 1;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(stats);

        return "- Player Base Stats capacity = 1\n";
    }

    private static string SetupCustomerQueue()
    {
        FoodServingCustomerManager[] managers = FindAll<FoodServingCustomerManager>();

        if (managers.Length <= 0)
            return "- FoodServingCustomerManager bulunamadi\n";

        string report = "";

        foreach (FoodServingCustomerManager manager in managers)
        {
            SerializedObject so = new SerializedObject(manager);

            so.FindProperty("customersPerRow").intValue = 3;
            so.FindProperty("maxCustomers").intValue = 3;

            // Sideways spread only; the row-to-row offset is left alone so a
            // hand-tuned queue direction survives
            SerializedProperty side = so.FindProperty("sideSpacing");
            if (side.vector3Value == Vector3.zero)
                side.vector3Value = new Vector3(0f, 0f, 1f);

            so.ApplyModifiedProperties();

            report += "- " + manager.name + ": 3 yan yana, tek sira\n";
        }

        return report;
    }

    private static string SetupDropZones()
    {
        FoodDropZone[] zones = FindAll<FoodDropZone>();

        if (zones.Length <= 0)
            return "- FoodDropZone bulunamadi\n";

        int changed = 0;

        foreach (FoodDropZone zone in zones)
        {
            SerializedObject zoneSo = new SerializedObject(zone);
            Plateau plateau = zoneSo.FindProperty("plateau").objectReferenceValue as Plateau;

            if (plateau == null)
                continue;

            SetMaxCapacity(plateau, 1);
            changed++;
        }

        return "- " + changed + " drop zone plateau max capacity = 1\n";
    }

    private static string SetupSpawnerStations()
    {
        FoodSpawnerStation[] stations = FindAll<FoodSpawnerStation>();

        if (stations.Length <= 0)
            return "- FoodSpawnerStation bulunamadi\n";

        int changed = 0;

        foreach (FoodSpawnerStation station in stations)
        {
            SerializedObject so = new SerializedObject(station);
            Plateau plateau = so.FindProperty("plateau").objectReferenceValue as Plateau;

            if (plateau == null)
                continue;

            SetMaxCapacity(plateau, spawnerCapacity);
            changed++;
        }

        return "- " + changed + " kaynak istasyonu max capacity = " + spawnerCapacity + "\n";
    }

    // Customers ship without a collider, so a tap ray goes straight through
    // them and lands on the floor behind. A trigger capsule gives the tap
    // something to actually hit without affecting physics or navigation
    private static string SetupCustomerCollider()
    {
        string path = "Assets/Tiny Coffee Shop/Prefabs/Characters/Customer.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
            return "- Customer prefab bulunamadi (" + path + ")\n";

        CapsuleCollider collider = root.GetComponent<CapsuleCollider>();
        bool added = collider == null;

        if (added)
            collider = root.AddComponent<CapsuleCollider>();

        collider.isTrigger = true;
        collider.radius = .45f;
        collider.height = 1.8f;
        collider.center = new Vector3(0f, .9f, 0f);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        return added
            ? "- Customer prefab'ina tiklanabilir collider eklendi\n"
            : "- Customer collider guncellendi\n";
    }

    // Hand serving only: the counter must not quietly take the food and serve
    // it by itself while the player walks past
    private static string DisableAutoServing()
    {
        string report = "";

        foreach (FoodServingStation station in FindAll<FoodServingStation>())
        {
            if (!station.enabled)
                continue;

            Undo.RecordObject(station, "Disable auto serving");
            station.enabled = false;
            EditorUtility.SetDirty(station);

            report += "- Otomatik servis kapatildi: " + station.name + "\n";
        }

        int disabledColliders = 0;

        foreach (FoodDropZone zone in FindAll<FoodDropZone>())
        {
            foreach (Collider collider in zone.GetComponents<Collider>())
            {
                if (!collider.enabled)
                    continue;

                Undo.RecordObject(collider, "Disable drop zone");
                collider.enabled = false;
                EditorUtility.SetDirty(collider);
                disabledColliders++;
            }
        }

        report += "- " + disabledColliders + " drop zone collider kapatildi\n";

        return report;
    }

    // The upgrade desk restores a saved capacity level and pushes it onto the
    // player's tray, which quietly undoes the one-item-in-hand rule. This scene
    // has no upgrades, so the whole desk goes to sleep
    private static string DisableStatUpgrades()
    {
        UpgradeDeskStation[] desks = FindAll<UpgradeDeskStation>();

        if (desks.Length <= 0)
            return "- Upgrade desk yok, gerek kalmadi\n";

        string report = "";

        foreach (UpgradeDeskStation desk in desks)
        {
            if (!desk.gameObject.activeSelf)
                continue;

            Undo.RecordObject(desk.gameObject, "Disable upgrade desk");
            desk.gameObject.SetActive(false);
            EditorUtility.SetDirty(desk.gameObject);

            report += "- Upgrade desk kapatildi: " + desk.name + "\n";
        }

        return report.Length > 0 ? report : "- Upgrade desk zaten kapali\n";
    }

    private static string SetupPlayer()
    {
        PlayerController[] controllers = FindAll<PlayerController>();

        if (controllers.Length <= 0)
            return "- Player (PlayerController) bulunamadi\n";

        PlayerController player = controllers[0];
        GameObject playerObject = player.gameObject;
        string report = "- Player: " + playerObject.name + "\n";

        // The player's own tray, in case PlayerStatsHandler is not in this scene
        if (playerObject.TryGetComponent(out HoldFoodAbility holdFood))
        {
            SerializedObject holdSo = new SerializedObject(holdFood);
            Plateau plateau = holdSo.FindProperty("plateau").objectReferenceValue as Plateau;

            if (plateau != null)
            {
                SetMaxCapacity(plateau, 1);
                report += "- Player plateau max capacity = 1\n";
            }
        }
        else
        {
            report += "- UYARI: Player'da HoldFoodAbility yok\n";
        }

        TapToServe tapToServe = playerObject.GetComponent<TapToServe>();

        if (tapToServe == null)
        {
            tapToServe = Undo.AddComponent<TapToServe>(playerObject);
            report += "- TapToServe eklendi\n";
        }

        FoodServingCustomerManager[] managers = FindAll<FoodServingCustomerManager>();
        CashFile cashFile = FindFirst<CashFile>();
        Transform exitPoint = FindCustomerExitPoint();

        SerializedObject tapSo = new SerializedObject(tapToServe);

        SerializedProperty managersProperty = tapSo.FindProperty("customerManagers");
        managersProperty.arraySize = managers.Length;
        for (int i = 0; i < managers.Length; i++)
            managersProperty.GetArrayElementAtIndex(i).objectReferenceValue = managers[i];

        RectTransform[] ignored = FindFullScreenInputCatchers();
        SerializedProperty ignoredProperty = tapSo.FindProperty("ignoredUI");
        ignoredProperty.arraySize = ignored.Length;
        for (int i = 0; i < ignored.Length; i++)
            ignoredProperty.GetArrayElementAtIndex(i).objectReferenceValue = ignored[i];

        report += "- Yok sayilan tam ekran UI: " + ignored.Length + "\n";

        tapSo.FindProperty("cashFile").objectReferenceValue = cashFile;
        tapSo.FindProperty("customerExitPoint").objectReferenceValue = exitPoint;
        tapSo.FindProperty("gameCamera").objectReferenceValue = Camera.main;
        tapSo.ApplyModifiedProperties();

        report += "- TapToServe: " + managers.Length + " kuyruk baglandi\n";
        report += cashFile != null ? "- Cash file baglandi\n" : "- UYARI: CashFile yok, para uretilmez\n";
        report += exitPoint != null
            ? "- Cikis noktasi: " + exitPoint.name + "\n"
            : "- UYARI: Cikis noktasi yok, musteriler yerinde silinir\n";

        if (playerObject.TryGetComponent(out ClickToMovePlayerController clickToMove))
        {
            SerializedObject clickSo = new SerializedObject(clickToMove);
            clickSo.FindProperty("tapToServe").objectReferenceValue = tapToServe;

            if (clickSo.FindProperty("gameCamera").objectReferenceValue == null)
                clickSo.FindProperty("gameCamera").objectReferenceValue = Camera.main;

            clickSo.ApplyModifiedProperties();
            report += "- ClickToMove: musteriye tiklamak yurume sayilmayacak\n";
        }

        return report;
    }

    private static string SetupCamera()
    {
        Camera camera = Camera.main;

        if (camera == null)
            return "- Main Camera bulunamadi\n";

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();

        if (data == null)
            return "- Kamerada URP camera data yok\n";

        Undo.RecordObject(data, "Kitchen camera AA");
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        EditorUtility.SetDirty(data);

        return "- Kamera: Post Processing + SMAA (High)\n";
    }

    // The joystick zone stretches across the whole screen so a drag can start
    // anywhere. It is not a button, so a tap landing on it must still reach
    // the game world
    private static RectTransform[] FindFullScreenInputCatchers()
    {
        List<RectTransform> found = new List<RectTransform>();

        foreach (MobileJoystick joystick in FindAll<MobileJoystick>())
        {
            RectTransform rect = joystick.transform as RectTransform;

            if (rect != null && !found.Contains(rect))
                found.Add(rect);
        }

        foreach (Transform candidate in FindAll<Transform>())
        {
            if (!candidate.name.ToLower().Contains("joystick"))
                continue;

            RectTransform rect = candidate as RectTransform;

            if (rect != null && !found.Contains(rect))
                found.Add(rect);
        }

        return found.ToArray();
    }

    // Borrowed from whichever pickup station already has one, so the customers
    // leave the way they always did. Falls back to a name match
    private static Transform FindCustomerExitPoint()
    {
        PickupStation pickup = FindFirst<PickupStation>();

        if (pickup != null)
        {
            SerializedObject so = new SerializedObject(pickup);
            Transform point = so.FindProperty("customerExitPoint").objectReferenceValue as Transform;

            if (point != null)
                return point;
        }

        foreach (Transform candidate in FindAll<Transform>())
        {
            if (candidate.name.Replace(" ", "").ToLower().Contains("customerexit"))
                return candidate;
        }

        return null;
    }

    private static void SetMaxCapacity(Plateau plateau, int capacity)
    {
        SerializedObject so = new SerializedObject(plateau);
        so.FindProperty("maxCapacity").intValue = capacity;
        so.ApplyModifiedProperties();
    }

    // Inactive objects included on purpose: stations often start disabled
    private static T[] FindAll<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private static T FindFirst<T>() where T : Object
    {
        T[] found = FindAll<T>();
        return found.Length > 0 ? found[0] : null;
    }
}
