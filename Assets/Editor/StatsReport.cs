using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;

// Prints what the player is actually running on, next to what the stats asset
// says it should be. Editing an asset and seeing nothing change has one of a
// handful of causes and all of them are visible here
public static class StatsReport
{
    [MenuItem("Cooked Fast/Report Player Stats")]
    public static void Report()
    {
        StringBuilder text = new StringBuilder();

        if (!EditorApplication.isPlaying)
            text.AppendLine("(Play Mode kapali -- calisma zamani degerleri bos gorunur)\n");

        PlayerController[] controllers = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (controllers.Length <= 0)
        {
            Debug.LogError("[StatsReport] Sahnede PlayerController yok");
            return;
        }

        // Both controllers sit on the player with one of them switched off, and
        // which one is live decides whether a NavMesh Obstacle means anything to
        // the player at all
        PlayerController live = null;

        text.AppendLine("CONTROLLERS");

        foreach (PlayerController candidate in controllers)
        {
            bool on = candidate.enabled && candidate.gameObject.activeInHierarchy;

            text.AppendLine("  " + candidate.GetType().Name +
                            "  acik " + on +
                            "  moveSpeed " + ReadFloat(candidate, "moveSpeed").ToString("0.000"));

            if (on && live == null)
                live = candidate;
        }

        if (live == null)
            live = controllers[0];

        text.AppendLine("  -> aktif: " + live.GetType().Name +
                        (live is ClickToMovePlayerController
                            ? "  (NavMeshAgent -- obstacle Carve ile ise yarar)"
                            : "  (CharacterController -- SADECE fizik collider durdurur)"));

        Transform player = live.transform;
        text.AppendLine();

        CharacterStats stats = player.GetComponentInChildren<CharacterStats>(true);

        if (stats == null)
        {
            text.AppendLine("CHARACTER STATS: bilesen yok -- asset oyuna hic baglanmiyor");
        }
        else
        {
            BaseCharacterStatsSO asset = new SerializedObject(stats)
                .FindProperty("baseStats").objectReferenceValue as BaseCharacterStatsSO;

            text.AppendLine("ASSET: " + (asset == null ? "BOS" : AssetDatabase.GetAssetPath(asset)));

            if (asset != null)
            {
                text.AppendLine("  taban   speed " + asset.Speed.ToString("0.00") +
                                "  capacity " + asset.Capacity +
                                "  revenue " + asset.Revenue.ToString("0.00"));
                text.AppendLine("  tavan   speed " + asset.MaxSpeed.ToString("0.00") +
                                "  capacity " + asset.MaxCapacity +
                                "  revenue " + asset.MaxRevenue.ToString("0.00"));
            }

            text.AppendLine();
            text.AppendLine("CALISMA ZAMANI (CharacterStats)");
            text.AppendLine("  speed " + stats.Speed.ToString("0.000") +
                            "  capacity " + stats.Capacity +
                            "  revenue " + stats.Revenue.ToString("0.000"));
        }

        text.AppendLine();
        DescribeDesk(text);

        text.AppendLine();
        DescribePlateau(text, player);

        Debug.Log("[StatsReport]\n" + text);
    }

    // The desk is the only thing that pushes levels onto the player, and it
    // lives inside a LockedElement. Switched off, it never loads and never
    // applies -- the single most likely reason an asset edit does nothing
    private static void DescribeDesk(StringBuilder text)
    {
        UpgradeDeskStation[] desks = Object.FindObjectsByType<UpgradeDeskStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        text.AppendLine("UPGRADE DESK sayisi: " + desks.Length);

        foreach (UpgradeDeskStation desk in desks)
        {
            text.AppendLine("  " + desk.name +
                            "  aktif " + desk.gameObject.activeInHierarchy +
                            " (kendi: " + desk.gameObject.activeSelf + ")");

            FieldInfo field = typeof(UpgradeDeskStation)
                .GetField("statLevels", BindingFlags.NonPublic | BindingFlags.Instance);

            int[] levels = field == null ? null : field.GetValue(desk) as int[];

            text.AppendLine("    seviyeler: " +
                            (levels == null ? "yuklenmedi" : string.Join(", ", levels)));
        }

        if (desks.Length <= 0)
            text.AppendLine("  Yok -- PlayerStatsHandler.Start taban degerleri kendi uygular");
    }

    private static void DescribePlateau(StringBuilder text, Transform player)
    {
        PlayerStatsHandler handler = player.GetComponentInChildren<PlayerStatsHandler>(true);

        text.AppendLine("PLAYER STATS HANDLER: " + (handler == null ? "YOK" : "var"));

        Plateau plateau = handler == null
            ? player.GetComponentInChildren<Plateau>(true)
            : new SerializedObject(handler).FindProperty("plateau").objectReferenceValue as Plateau;

        if (plateau == null)
        {
            text.AppendLine("PLATEAU: bagli degil -- kapasite hic guncellenmiyor");
            return;
        }

        text.AppendLine("PLATEAU: " + plateau.name +
                        "  aktif " + plateau.gameObject.activeInHierarchy);
        text.AppendLine("  maxCapacity (gercekte kullanilan): " +
                        ReadInt(plateau, "maxCapacity"));
        text.AppendLine("  slot sayisi: " + plateau.GetComponentsInChildren<FoodPosition>(true).Length);
        text.AppendLine("  dolu: " + plateau.IsFull + "  bos: " + plateau.IsEmpty);
    }

    // Walks every link between standing on a station and holding its food. A
    // station that hands out nothing has broken exactly one of these and they
    // all look fine in the Inspector
    [MenuItem("Cooked Fast/Report Food Stations")]
    public static void ReportStations()
    {
        StringBuilder text = new StringBuilder();

        if (!EditorApplication.isPlaying)
            text.AppendLine("(Play Mode kapali -- tabaktaki yemek sayilari bos gorunur)\n");

        GameObject player = DescribePlayerSide(text);

        FoodSpawnerStation[] stations = Object.FindObjectsByType<FoodSpawnerStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (FoodSpawnerStation station in stations)
        {
            text.AppendLine();
            DescribeStation(text, station, player);
        }

        // An object that looks like a station and is not one reads as "my zone
        // does nothing" rather than "my zone was never wired up"
        text.AppendLine();
        DescribeUnwiredZones(text, stations);

        text.AppendLine();
        DescribeFoodInstances(text);

        Debug.Log("[Yemek Istasyonlari]\n" + text);
    }

    // Editing a food prefab and seeing nothing change has two causes and this
    // separates them: the object being looked at is not an instance of that
    // prefab at all, or it is a copy made before the edit and never told
    private static void DescribeFoodInstances(StringBuilder text)
    {
        text.AppendLine("YEMEK PREFABLARI");

        foreach (string guid in AssetDatabase.FindAssets(
                     "t:Prefab", new[] { "Assets/Tiny Coffee Shop/Prefabs/GamePlay" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null || prefab.GetComponent<SpawnableFood>() == null)
                continue;

            MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>(true);

            text.AppendLine("  " + prefab.name + " (" +
                            prefab.GetComponent<SpawnableFood>().GetType().Name + ")");
            text.AppendLine("    dosyadaki model olcek: " +
                            (filter == null ? "mesh yok" : filter.transform.localScale.ToString("0.0000")) +
                            "  mesh " + (filter == null || filter.sharedMesh == null
                                ? "YOK"
                                : filter.sharedMesh.name));
        }

        text.AppendLine();
        text.AppendLine("SAHNEDEKI YEMEKLER");

        int listed = 0;

        foreach (SpawnableFood food in Object.FindObjectsByType<SpawnableFood>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            MeshFilter filter = food.GetComponentInChildren<MeshFilter>(true);
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(food.gameObject);

            text.AppendLine("  " + food.name + " (" + food.GetType().Name + ")" +
                            "  kaynak: " + (source == null
                                ? "PREFAB BAGI YOK -- prefabi degistirmek bunu etkilemez"
                                : AssetDatabase.GetAssetPath(source)));
            text.AppendLine("    model olcek: " +
                            (filter == null ? "mesh yok" : filter.transform.localScale.ToString("0.0000")));

            if (++listed >= 12)
            {
                text.AppendLine("  ... (ilk 12 tanesi)");
                break;
            }
        }

        if (listed <= 0)
            text.AppendLine("  yok");

        // Objects that carry a food mesh without being food at all: scene
        // decoration that looks like the thing being edited
        text.AppendLine();
        text.AppendLine("YEMEK GIBI GORUNUP SpawnableFood OLMAYANLAR");

        int strays = 0;

        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (filter.GetComponentInParent<SpawnableFood>() != null)
                continue;

            string lower = filter.name.ToLowerInvariant();

            if (!lower.Contains("salad") && !lower.Contains("pizza") && !lower.Contains("cup"))
                continue;

            text.AppendLine("  " + filter.name + "  bu bir SpawnableFood degil, dekor");
            strays++;
        }

        if (strays <= 0)
            text.AppendLine("  yok");
    }

    private static void DescribeUnwiredZones(StringBuilder text, FoodSpawnerStation[] stations)
    {
        StringBuilder found = new StringBuilder();

        foreach (Transform candidate in Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string lower = candidate.name.ToLowerInvariant();

            if (!lower.Contains("zone") && !lower.Contains("station") && !lower.Contains("salad"))
                continue;

            if (candidate.GetComponent<FoodSpawnerStation>() != null)
                continue;

            if (candidate.GetComponent<FoodDropZone>() != null)
                continue;

            found.AppendLine("  " + candidate.name +
                             "  aktif " + candidate.gameObject.activeInHierarchy);
        }

        text.AppendLine("ISTASYON OLMAYAN 'zone/station/salad' OBJELERI");
        text.AppendLine(found.Length <= 0
            ? "  yok"
            : found + "  Bunlarda FoodSpawnerStation yok -- oyuncu ustunden gecer, bir sey olmaz");
    }

    private static GameObject DescribePlayerSide(StringBuilder text)
    {
        PlayerDetector[] detectors = Object.FindObjectsByType<PlayerDetector>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (detectors.Length <= 0)
        {
            text.AppendLine("PLAYER: PlayerDetector yok -- hicbir istasyon tetiklenmez");
            return null;
        }

        GameObject player = detectors[0].gameObject;

        text.AppendLine("PLAYER: " + player.name + "  layer " + LayerMask.LayerToName(player.layer));

        // Trigger callbacks need a moving body on one side. A NavMeshAgent on
        // its own is not one, which is why this is worth printing
        bool hasBody = player.GetComponent<Rigidbody>() != null ||
                       player.GetComponent<CharacterController>() != null;

        text.AppendLine("  Rigidbody/CharacterController: " + hasBody +
                        (hasBody ? "" : "  <-- trigger olaylari calismaz"));

        Collider[] colliders = player.GetComponents<Collider>();

        text.AppendLine("  collider sayisi: " + colliders.Length);

        if (player.TryGetComponent(out HoldFoodAbility hold))
        {
            Plateau plateau = new SerializedObject(hold).FindProperty("plateau").objectReferenceValue as Plateau;

            if (plateau == null)
            {
                text.AppendLine("  HoldFoodAbility.plateau BOS");
            }
            else
            {
                SpawnableFood held = Application.isPlaying ? plateau.Peek() : null;

                text.AppendLine("  tasidigi: " + (held == null ? "bos" : held.GetType().Name) +
                                "  dolu " + plateau.IsFull);
            }
        }
        else
        {
            text.AppendLine("  HoldFoodAbility yok");
        }

        return player;
    }

    private static void DescribeStation(StringBuilder text, FoodSpawnerStation station, GameObject player)
    {
        SerializedObject so = new SerializedObject(station);

        Object food = so.FindProperty("spawnableFoodPrefab").objectReferenceValue;
        Plateau plateau = so.FindProperty("plateau").objectReferenceValue as Plateau;
        Object workerPoint = so.FindProperty("workerTargetPoint").objectReferenceValue;

        text.AppendLine("ISTASYON: " + station.name +
                        "  aktif " + station.gameObject.activeInHierarchy +
                        "  layer " + LayerMask.LayerToName(station.gameObject.layer));

        text.AppendLine("  yemek prefabi: " + (food == null ? "BOS <--" : food.name +
                        " (" + food.GetType().Name + ")"));
        text.AppendLine("  spawnDelay: " + so.FindProperty("spawnDelay").floatValue.ToString("0.00"));
        text.AppendLine("  workerTargetPoint: " + (workerPoint == null ? "BOS" : "var"));

        // The detector reads the station off the collider it touched, so a
        // trigger on a child object finds nothing however correct it looks
        Collider trigger = null;

        foreach (Collider candidate in station.GetComponents<Collider>())
        {
            if (candidate.isTrigger && candidate.enabled)
                trigger = candidate;
        }

        if (trigger == null)
        {
            bool onChild = false;

            foreach (Collider candidate in station.GetComponentsInChildren<Collider>(true))
            {
                if (candidate.isTrigger && candidate.gameObject != station.gameObject)
                    onChild = true;
            }

            text.AppendLine("  trigger: YOK <-- " + (onChild
                ? "cocukta var ama FoodSpawnerStation ile ayni objede olmali"
                : "hic trigger collider yok"));
        }
        else
        {
            text.AppendLine("  trigger: var, dunya boyutu " + trigger.bounds.size.ToString("0.00"));
        }

        if (player != null && Physics.GetIgnoreLayerCollision(player.layer, station.gameObject.layer))
            text.AppendLine("  <-- Physics matrisinde player layer'i ile carpismasi KAPALI");

        if (plateau == null)
        {
            text.AppendLine("  plateau: BOS <-- Pop() hep null doner, hicbir sey verilmez");
            return;
        }

        text.AppendLine("  plateau: " + plateau.name +
                        "  aktif " + plateau.gameObject.activeInHierarchy +
                        (plateau.gameObject.activeInHierarchy ? "" : "  <-- kapali, dolmaz"));

        if (Application.isPlaying)
            text.AppendLine("  uzerindeki yemek: " + plateau.GetFoodCount() +
                            "  bos " + plateau.IsEmpty +
                            (plateau.IsEmpty ? "  <-- verecek yemegi yok" : ""));
    }

    private static float ReadFloat(Object target, string field)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(field);

        return property == null ? 0f : property.floatValue;
    }

    private static int ReadInt(Object target, string field)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(field);

        return property == null ? 0 : property.intValue;
    }
}
