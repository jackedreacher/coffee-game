using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Switches the game from "walk into a trigger" to "tap a thing".
//
// Two halves. The player half turns the joystick off and click-to-move on, and
// takes PlayerDetector off the player so triggers stop driving them. The station
// half gives everything the player can use an Interactable and a stand point.
//
// What it deliberately does NOT do is delete the trigger colliders. The workers
// still walk into them and PlayerDetector still runs on the workers -- pulling
// them out would silently stop the staff working, which is a worse bug than the
// one being fixed
public static class InteractionSetup
{
    private const string standPointName = "Stand Point";
    private const string clickBoxName = "Click Box";

    // Far enough that arriving is not a game of millimetres, close enough that
    // two stations side by side are still two stations
    private const float defaultReach = 1.6f;

    [MenuItem("Cooked Fast/Etkilesim: 1 - Tiklamaya Gecir", priority = 200)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz.\n\n" +
                "Sahneye eklenen her sey Play durunca silinir.\n" +
                "Once Play'i durdur, sonra tekrar calistir.",
                "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.Append(SetupPlayer());
        report.AppendLine();
        report.Append(SetupStations());

        report.AppendLine();
        report.AppendLine("Nasil oynanir");
        report.AppendLine("  Bos zemine tikla -> oraya yurur");
        report.AppendLine("  Istasyona tikla  -> durma noktasina yurur, varinca is yapar");
        report.AppendLine("  Musteriye tikla  -> yanina yurur, servis alanindaysa verir");
        report.AppendLine();
        report.AppendLine("Ayarlar");
        report.AppendLine("  Durma noktasi : her istasyon > " + standPointName + " (surukle)");
        report.AppendLine("  Ne kadar yakin: ayni obje > Interactable > Reach");
        report.AppendLine("  Console adi   : ayni yer > Label");
        report.AppendLine();
        report.AppendLine("Interactable secince Scene'de yesil top = durma noktasi,");
        report.AppendLine("  etrafindaki halka = Reach. Ikisi de elle surukelenebilir.");

        Debug.Log("[Etkilesim]\n" + report);
        EditorUtility.DisplayDialog("Etkilesim Kurulumu", report.ToString(), "Tamam");
    }

    // Setup leaves an existing pop target alone, which is right for a target
    // somebody dragged in by hand and wrong for the ones it wrote itself in an
    // earlier version -- and from the field there is no telling the two apart.
    // So the overwrite is its own command rather than a rule inside the first
    [MenuItem("Cooked Fast/Etkilesim: 2 - Pop Hedeflerini Yenile", priority = 201)]
    public static void RefreshPopTargets()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Play Mode",
                "Play Mode'da calistirilamaz -- yazilan her sey Play durunca silinir.",
                "Tamam");
            return;
        }

        Interactable[] all = UnityEngine.Object.FindObjectsByType<Interactable>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        StringBuilder report = new StringBuilder();

        report.AppendLine("Pop hedefleri yeniden hesaplandi: " + all.Length + " istasyon");
        report.AppendLine();

        int changed = 0;

        foreach (Interactable interactable in all)
        {
            SerializedObject so = new SerializedObject(interactable);
            SerializedProperty pop = so.FindProperty("popTarget");

            Transform stored = pop.objectReferenceValue as Transform;
            Transform wanted = Interactable.ResolvePopTarget(
                interactable.gameObject, out string why);

            if (stored == wanted)
                continue;

            pop.objectReferenceValue = wanted;
            so.ApplyModifiedProperties();

            changed++;

            report.AppendLine("  " + interactable.name + ": " +
                              (stored == null ? "bos" : stored.name) + " -> " +
                              (wanted == null ? "YOK" : wanted.name));

            report.AppendLine("      " + why);

            EditorSceneManager.MarkSceneDirty(interactable.gameObject.scene);
        }

        if (changed <= 0)
            report.AppendLine("  hicbiri degismedi -- hepsi zaten dogru hedefte");

        report.AppendLine();
        report.AppendLine("Peynir tezgahinda hedef Wheel olmali: uzerindeki dilimler");
        report.AppendLine("  onun cocugu, dolayisiyla tekerlekle birlikte zipllarlar.");

        Debug.Log("[Etkilesim]\n" + report);
        EditorUtility.DisplayDialog("Pop Hedefleri", report.ToString(), "Tamam");
    }

    // ---- the player --------------------------------------------------------

    private static string SetupPlayer()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("Oyuncu");

        TapToServe tap = UnityEngine.Object.FindFirstObjectByType<TapToServe>(
            FindObjectsInactive.Include);

        if (tap == null)
        {
            report.AppendLine("  TapToServe bulunamadi -- oyuncu sahnede mi?");
            return report.ToString();
        }

        GameObject player = tap.gameObject;

        report.AppendLine("  " + player.name);

        JoystickPlayerController joystick = player.GetComponent<JoystickPlayerController>();

        // Read BEFORE it is switched off. This is the number that has been tuned
        // by hand; a freshly added controller serializes its own at zero, hands
        // that to the agent, and the player accepts every tap and never moves
        float speed = 0f;

        if (joystick != null)
        {
            speed = new SerializedObject(joystick).FindProperty("moveSpeed").floatValue;

            report.AppendLine("  joystick hizi: " + speed.ToString("0.0") + " (devralinacak)");

            report.Append(HideJoystickUI(joystick));
        }

        if (joystick != null && joystick.enabled)
        {
            Undo.RecordObject(joystick, "Disable joystick");
            joystick.enabled = false;

            report.AppendLine("  joystick: KAPATILDI");
        }
        else
        {
            report.AppendLine("  joystick: " + (joystick == null ? "yok" : "zaten kapali"));
        }

        ClickToMovePlayerController click = player.GetComponent<ClickToMovePlayerController>();

        if (click == null)
        {
            click = Undo.AddComponent<ClickToMovePlayerController>(player);
            report.AppendLine("  ClickToMove: eklendi");
        }
        else
        {
            report.AppendLine("  ClickToMove: zaten vardi");
        }

        Undo.RecordObject(click, "Enable click to move");
        click.enabled = true;

        // Pointed at TapToServe so it stops raycasting on its own. Two handlers
        // answering the same tap is how a station tap turns into a walk past it
        SerializedObject so = new SerializedObject(click);

        so.FindProperty("tapToServe").objectReferenceValue = tap;

        SerializedProperty moveSpeed = so.FindProperty("moveSpeed");

        if (moveSpeed.floatValue <= .01f)
        {
            moveSpeed.floatValue = speed > .01f ? speed : 5f;

            report.AppendLine("  ClickToMove > Move Speed: " +
                              moveSpeed.floatValue.ToString("0.0") +
                              (speed > .01f ? " (joystick'ten alindi)" : " (varsayilan)"));
        }
        else
        {
            report.AppendLine("  ClickToMove > Move Speed: " +
                              moveSpeed.floatValue.ToString("0.0") + " (zaten ayarli)");
        }

        so.ApplyModifiedProperties();

        report.AppendLine("  ClickToMove > Tap To Serve: bagli (tiklamayi TapToServe yonetir)");

        report.Append(CheckAgent(player));
        report.Append(ReplaceCharacterController(player));

        // The player's own trigger interaction. The component stays in the
        // project because the workers run on it -- it is only the player who
        // stops using it
        PlayerDetector detector = player.GetComponent<PlayerDetector>();

        if (detector != null && detector.enabled)
        {
            Undo.RecordObject(detector, "Disable player detector");
            detector.enabled = false;

            report.AppendLine("  PlayerDetector: KAPATILDI (tetikleyiciyle is yapmayi birakti)");
        }
        else
        {
            report.AppendLine("  PlayerDetector: " + (detector == null ? "yok" : "zaten kapali"));
        }

        report.AppendLine("  not: istasyonlarin trigger'lari DURUYOR -- calisanlar onlari kullaniyor");

        EditorSceneManager.MarkSceneDirty(player.scene);

        return report.ToString();
    }

    // The on-screen stick is a separate UI object -- switching the controller off
    // stops it steering but leaves it drawn, which reads as "the change did not
    // take". Its own GameObject is turned off, not destroyed, so going back to
    // the joystick is one checkbox
    private static string HideJoystickUI(JoystickPlayerController joystick)
    {
        UnityEngine.Object ui = new SerializedObject(joystick)
            .FindProperty("joystick").objectReferenceValue;

        MonoBehaviour behaviour = ui as MonoBehaviour;

        if (behaviour == null)
            return "  joystick UI: bulunamadi, ekranda kalabilir -- elle kapat\n";

        if (!behaviour.gameObject.activeSelf)
            return "  joystick UI: zaten kapali (" + behaviour.gameObject.name + ")\n";

        Undo.RecordObject(behaviour.gameObject, "Hide joystick UI");
        behaviour.gameObject.SetActive(false);

        return "  joystick UI: GIZLENDI (" + behaviour.gameObject.name + ")\n";
    }

    // A CharacterController and a NavMeshAgent on one object fight over the
    // transform: the controller re-applies its own tracked position every
    // physics step and quietly undoes what the agent just did. The player
    // accepts destinations, reports them, and stands still.
    //
    // It cannot simply be switched off, though -- it is also the player's
    // collider, and without one the serving zone triggers stop firing and
    // customers can no longer be handed anything. So it is swapped for the same
    // shape as a plain capsule, plus the kinematic Rigidbody those triggers need
    // in order to notice it at all
    private static string ReplaceCharacterController(GameObject player)
    {
        CharacterController controller = player.GetComponent<CharacterController>();

        if (controller == null)
            return "  CharacterController: yok\n";

        if (!controller.enabled)
            return "  CharacterController: zaten kapali\n";

        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();

        if (capsule == null)
            capsule = Undo.AddComponent<CapsuleCollider>(player);

        Undo.RecordObject(capsule, "Match character controller");

        capsule.center = controller.center;
        capsule.radius = controller.radius;
        capsule.height = controller.height;
        capsule.isTrigger = false;

        Rigidbody body = player.GetComponent<Rigidbody>();

        if (body == null)
            body = Undo.AddComponent<Rigidbody>(player);

        Undo.RecordObject(body, "Match character controller");

        // Kinematic: the agent moves the player, physics must not
        body.isKinematic = true;
        body.useGravity = false;

        Undo.RecordObject(controller, "Disable character controller");
        controller.enabled = false;

        return "  CharacterController: KAPATILDI -- agent ile transform icin cekisiyordu\n" +
               "    yerine ayni olculerde CapsuleCollider + kinematic Rigidbody kondu,\n" +
               "    servis alani tetikleyicileri calismaya devam etsin diye\n";
    }

    // Both of these are silent at runtime and both of them stop the player dead
    private static string CheckAgent(GameObject player)
    {
        UnityEngine.AI.NavMeshAgent agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (agent == null)
            return "  NavMeshAgent: YOK -- ClickToMove eklendiyse gelmis olmali\n";

        string report = "  NavMeshAgent: var\n";

        // Reported and left alone in the first version, which was no use: the
        // report said "KAPALI, ac" and the thing it was describing was the exact
        // reason the player would not move
        if (!agent.enabled)
        {
            Undo.RecordObject(agent, "Enable agent");
            agent.enabled = true;

            report += "    KAPALIYDI -> ACILDI (yurumemesinin sebebi buydu)\n";
        }

        if (!UnityEngine.AI.NavMesh.SamplePosition(
                player.transform.position, out _, 2f, UnityEngine.AI.NavMesh.AllAreas))
            report += "    <-- oyuncunun altinda NavMesh YOK. Window > AI > Navigation > Bake\n";

        return report;
    }

    // ---- the stations ------------------------------------------------------

    // Everything the player can walk up to and use. Gathered by component type
    // rather than by name: a station renamed in the hierarchy still works, and a
    // station added later is picked up by re-running this
    private static GameObject[] FindStations()
    {
        HashSet<GameObject> found = new HashSet<GameObject>();

        Collect<FoodSpawnerStation>(found);
        Collect<CookingStation>(found);
        Collect<FoodDropZone>(found);
        Collect<HoldingShelf>(found);
        Collect<FridgeDoor>(found);
        Collect<FryerStation>(found);
        Collect<Trash>(found);
        Collect<TableSet>(found);

        // Not because the counter is tapped -- the customer is -- but because
        // the stand point on it is where the player is sent to serve them
        Collect<FoodServingCustomerManager>(found);

        GameObject[] array = new GameObject[found.Count];

        found.CopyTo(array);

        return array;
    }

    private static void Collect<T>(HashSet<GameObject> into) where T : Component
    {
        foreach (T candidate in UnityEngine.Object.FindObjectsByType<T>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            into.Add(candidate.gameObject);
        }
    }

    private static string SetupStations()
    {
        GameObject[] stations = FindStations();

        StringBuilder report = new StringBuilder();

        report.AppendLine("Istasyonlar: " + stations.Length);

        if (stations.Length <= 0)
        {
            report.AppendLine("  sahnede hicbir istasyon bulunamadi");
            return report.ToString();
        }

        foreach (GameObject station in stations)
        {
            Interactable interactable = station.GetComponent<Interactable>();
            bool fresh = interactable == null;

            if (fresh)
                interactable = Undo.AddComponent<Interactable>(station);

            SerializedObject so = new SerializedObject(interactable);

            Transform stand = EnsureStandPoint(station, out bool madePoint);

            so.FindProperty("standPoint").objectReferenceValue = stand;

            if (fresh)
                so.FindProperty("reach").floatValue = defaultReach;

            // Written into the field rather than left to the runtime fallback.
            // Which piece of a station is "the bit the food sits on" is scene
            // knowledge -- a pan for the oven, a basket for the fryer, a plate
            // for a counter -- and it belongs where it can be seen and changed
            SerializedProperty pop = so.FindProperty("popTarget");

            string popNote = ApplyPop(station, pop);

            so.ApplyModifiedProperties();

            report.AppendLine("  " + station.name +
                              (fresh ? "  [Interactable eklendi]" : "  [zaten vardi]") +
                              (madePoint ? "  [" + standPointName + " eklendi]" : ""));

            report.AppendLine("  " + popNote);

            report.Append(EnsureClickBox(station));

            EditorSceneManager.MarkSceneDirty(station.scene);
        }

        report.AppendLine();
        report.AppendLine("YENI EKLENEN durma noktalari objenin ustunde duruyor.");
        report.AppendLine("  Her birini oyuncunun DURABILECEGI yere surukle --");
        report.AppendLine("  tezgahin icine degil, onunde durulacak zemine.");

        return report.ToString();
    }

    // Which piece bounces is decided by Interactable, not here.
    //
    // It used to be decided in both places, with a copy of the rules on each
    // side, and the copies drifted: the editor wrote one target into the scene
    // and the runtime fallback would have chosen a different one, so what a
    // station did on a tap depended on which of the two had run last
    private static string ApplyPop(GameObject station, SerializedProperty pop)
    {
        Transform stored = pop.objectReferenceValue as Transform;
        Transform wanted = Interactable.ResolvePopTarget(station);

        if (stored != null && stored == wanted)
            return "  pop -> " + stored.name;

        // A hand-picked target is left alone -- but only if it can actually do
        // the job. An invisible or navmesh-carving one is not a choice anybody
        // made on purpose, it is an older version of this command's leftovers
        if (stored != null && !Interactable.WouldNeverShow(station, stored))
            return "  pop -> " + stored.name + " (elle atanmis, korundu)" +
                   (wanted == null ? "" : "  [onerilen: " + wanted.name +
                                          " -- gecmek icin Etkilesim: 2]");

        pop.objectReferenceValue = wanted;

        string was = stored == null ? "bostu" : stored.name + " ise yaramiyordu";

        return wanted == null
            ? "  pop -> YOK, tiklayinca tepki vermez  (" + was + ")"
            : "  pop -> " + wanted.name + "  (" + was + ")";
    }

    // Placed at the station's own feet rather than guessed at. Which side of a
    // counter is walkable is not something bounds can answer -- it depends on
    // where the walls and the other counters are -- so it is left somewhere
    // obvious and obviously wrong, and the report says to move it
    private static Transform EnsureStandPoint(GameObject station, out bool made)
    {
        Transform existing = station.transform.Find(standPointName);

        made = existing == null;

        if (existing != null)
            return existing;

        GameObject point = new GameObject(standPointName);

        Undo.RegisterCreatedObjectUndo(point, "Add stand point");
        Undo.SetTransformParent(point.transform, station.transform, "Add stand point");

        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;
        point.transform.localPosition = Vector3.zero;

        if (TryBounds(station, out Bounds bounds))
            point.transform.position = new Vector3(bounds.center.x, 0f, bounds.center.z);
        else
            point.transform.position = station.transform.position.With(y: 0f);

        return point.transform;
    }

    // The tap is a raycast, so something has to be hit. Its own box rather than
    // resizing whatever is there: an existing solid collider is usually keeping
    // the player out of the counter, and growing it opens a hole in the kitchen
    private static string EnsureClickBox(GameObject station)
    {
        Collider[] colliders = station.GetComponentsInChildren<Collider>(true);

        if (colliders.Length > 0)
            return "";

        GameObject box = new GameObject(clickBoxName);

        Undo.RegisterCreatedObjectUndo(box, "Add click box");
        Undo.SetTransformParent(box.transform, station.transform, "Add click box");

        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = Vector3.one;

        BoxCollider collider = box.AddComponent<BoxCollider>();

        collider.isTrigger = true;

        if (!TryBounds(station, out Bounds bounds))
        {
            box.transform.localPosition = Vector3.zero;
            collider.size = Vector3.one;

            return "    " + clickBoxName + ": eklendi 1x1x1 -- elle ayarla\n";
        }

        box.transform.position = bounds.center;

        collider.center = Vector3.zero;
        collider.size = Divide(bounds.size, box.transform.lossyScale);

        return "    " + clickBoxName + ": eklendi (collider yoktu, tiklanamazdi)\n";
    }

    private static bool TryBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;

        bool any = false;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return any;
    }

    private static Vector3 Divide(Vector3 wanted, Vector3 parent)
    {
        return new Vector3(
            Mathf.Abs(parent.x) < .0001f ? wanted.x : wanted.x / parent.x,
            Mathf.Abs(parent.y) < .0001f ? wanted.y : wanted.y / parent.y,
            Mathf.Abs(parent.z) < .0001f ? wanted.z : wanted.z / parent.z);
    }
}
