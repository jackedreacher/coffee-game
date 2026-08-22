#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts a green tick over every cooking station, shown when what is in it is
// done.
//
// Built the same way the burn warning is: a world SpriteRenderer with
// FaceCamera, not a world space canvas. That decision was made twice already
// and both times the canvas came out invisible -- a sprite in the world is a
// quad with a texture on it, and it shows up the moment it exists.
//
// The two indicators are a pair and read as one: the tick says come and get it,
// the exclamation says come and get it NOW. The station hides the tick whenever
// the warning is up, so they never argue on the same pan
public static class ReadyTickSetup
{
    private const string rootName = "Ready Tick";

    private const string iconPath =
        "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_256/Icon_PictoIcon_Check.Png";

    private static readonly Color green = new Color(.24f, .78f, .35f);

    // Placed by hand in the Inspector and copied back here.
    //
    // This used to be arithmetic -- a lift off the machine, a fit to the sprite
    // and a push along the camera axis -- and arithmetic is the right shape for
    // a rule. It was never a rule. Where a mark reads well over a particular
    // oven in a particular kitchen is a thing you settle by looking at it, and
    // once it is settled the honest way to keep it is to write the numbers
    // down. Nudge the Ready Tick in the scene, copy its Transform back into
    // these three lines, and re-run.
    //
    // LOCAL to the machine, so a station rotated to face the room takes its
    // tick round with it. The report says which ones are turned
    private static readonly Vector3 place = new Vector3(-.523f, 1.121f, -.103f);

    // Edit mode only, in practice. FaceCamera writes the camera's whole
    // rotation over this every frame the game is running, so what this really
    // buys is a tick that is readable in the Scene view instead of edge on --
    // which is what made it worth setting by hand in the first place
    private static readonly Vector3 turn = new Vector3(53.551f, 97.282f, 4.651f);

    private const float size = .12595f;

    [MenuItem("Cooked Fast/Istasyon/Ocak: Hazir Tikini Kur", priority = 503)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Hazir Tiki",
                "Play modundayken calismaz. Once durdur.", "Tamam");
            return;
        }

        // Imported as a Sprite first. A PNG left at the default texture type
        // answers null to LoadAssetAtPath<Sprite> and says nothing about why
        FoodIconBaker.MakeSprite(iconPath);

        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

        if (icon == null)
        {
            Show("Tik ikonu bulunamadi:\n" + iconPath);
            return;
        }

        CookingStation[] ovens = Object.FindObjectsByType<CookingStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        // The fryer is not a CookingStation and never was.
        //
        // They are two separate MonoBehaviours on purpose -- the oven burns per
        // piece because it holds several, the fryer has one portion or none --
        // and this command only ever walked the first list. That is the whole
        // of why the fries never got a tick: not a wiring mistake in the scene,
        // a machine this command had never heard of
        FryerStation[] fryers = Object.FindObjectsByType<FryerStation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (ovens.Length <= 0 && fryers.Length <= 0)
        {
            Show("Sahnede hic pisirme istasyonu yok (CookingStation / FryerStation).");
            return;
        }

        string report = "";

        foreach (CookingStation oven in ovens)
        {
            if (oven == null)
                continue;

            report += Attach(oven, oven.transform, icon);
        }

        foreach (FryerStation fryer in fryers)
        {
            if (fryer == null)
                continue;

            report += Attach(fryer, fryer.transform, icon);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        report += "\n" + ovens.Length + " ocak, " + fryers.Length + " kizartma makinesi.\n";
        report += "\nHepsine ayni yerel Transform yazildi:\n" +
                  "  Position " + place.ToString("0.000") + "\n" +
                  "  Rotation " + turn.ToString("0.000") + "\n" +
                  "  Scale    " + size.ToString("0.00000") + "\n";

        report += "\nPisen urun hazir oldugunda cikar, alininca kaybolur.\n" +
                  "Yanma uyarisi ciktiginda gizlenir -- ikisi ayni tavayi anlatir\n" +
                  "ve acelesi olan uyaridir.\n\n" +
                  "Baska bir yer istersen sahnede bir Ready Tick'i surukle,\n" +
                  "Transform'unu ReadyTickSetup'in basindaki uc satira yaz ve\n" +
                  "komutu tekrar calistir -- hepsi ona uyar.";

        Show(report);
    }

    // One tick, on whichever kind of machine this is. Both of them keep it in a
    // field called readyRoot, so the wiring is the same line either way
    private static string Attach(Component station, Transform host, Sprite icon)
    {
        GameObject tick = Build(host, icon);

        SerializedObject so = new SerializedObject(station);

        SerializedProperty field = so.FindProperty("readyRoot");

        if (field == null)
            return "- " + station.name + ": readyRoot alani yok, atlandi\n";

        field.objectReferenceValue = tick;
        so.ApplyModifiedProperties();

        string line = "- " + station.name + ": tik kuruldu";

        // The one thing that can make the same numbers land somewhere else.
        //
        // The Transform below is local, so a machine turned to face the room
        // takes its tick round with it -- which is usually what is wanted and is
        // occasionally a tick pointing into a wall. Said out loud rather than
        // guessed at, because the values came off ONE station and nothing here
        // knows which one
        Vector3 turned = host.eulerAngles;

        if (Mathf.Abs(Mathf.DeltaAngle(turned.y, 0f)) > 1f)
            line += "  (makine " + turned.y.ToString("0") + " derece donuk -- " +
                    "tik de onunla donuk)";

        // Fryers may sit on either wall and can be rotated after setup. Their
        // timer/tick placement is world-bounds based, not the oven's one local
        // offset, so rebuilding the tick must finish by applying that rule.
        if (station is FryerStation fryer)
            line += "\n" + FriesSetup.AlignIndicatorsFor(fryer).TrimEnd();

        return line + "\n";
    }

    private static GameObject Build(Transform host, Sprite icon)
    {
        // Rebuilt rather than patched: this object is generated, so a half
        // updated one is a shape nobody authored
        Transform existing = host.Find(rootName);

        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject root = new GameObject(rootName);

        Undo.RegisterCreatedObjectUndo(root, "Create ready tick");
        Undo.SetTransformParent(root.transform, host, "Create ready tick");

        // The three numbers off the Inspector, written straight in. No fitting
        // to the sprite's own size any more -- that answered "how big is this
        // picture" when the question was "how big should this mark be here"
        root.transform.localPosition = place;
        root.transform.localRotation = Quaternion.Euler(turn);
        root.transform.localScale = Vector3.one * size;

        SpriteRenderer mark = root.AddComponent<SpriteRenderer>();

        mark.sprite = icon;
        mark.color = green;

        // In front of the kitchen it floats over. The oven is opaque geometry
        // and a sprite at the same depth would flicker against it
        mark.sortingOrder = 100;

        // A white edge, one order behind the mark.
        //
        // The hobs are dark and the wall behind them is not, so the same green
        // tick had two completely different amounts of contrast depending on
        // which machine it was standing on. The edge takes the background out
        // of it: white against the dark hob, and the green still reads against
        // the white. The mark's own colour is not touched -- the edge is a
        // separate set of renderers underneath it
        SpriteOutline.Build(root, icon, mark.sortingOrder - 1);

        // The kitchen camera is isometric, so a sprite left facing world
        // forward is seen edge on from one side.
        //
        // This overwrites the rotation above every frame the game is running --
        // deliberately. The written angle is what makes the mark readable in the
        // Scene view; this is what keeps it square to the screen in play
        root.AddComponent<FaceCamera>();

        // Off until the station turns it on. A tick showing on an empty pan is
        // worse than no tick: it is a tick that means nothing
        root.SetActive(false);

        return root;
    }

    private static void Show(string report)
    {
        Debug.Log("Hazir Tiki\n" + report);
        EditorUtility.DisplayDialog("Hazir Tiki", report, "Tamam");
    }
}
#endif
