#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

#if COOP_ONLINE
using Unity.Netcode;
using Unity.Netcode.Components;
#endif

// Builds the connection prototype: one empty room, one floor, two capsules and
// the online screens.
//
// Its own scene rather than the kitchen, on purpose. The question this answers
// is "do two machines on two internet connections agree with each other", and
// asking it inside the kitchen means the first thing that goes wrong could be
// Relay, ownership, the NavMesh, the animator, the plateau or the tap router.
// Six suspects, one symptom, and no way to tell them apart.
public static class CoopTestSetup
{
    public const string prefabPath = "Assets/Resources/Online/Coop Player.prefab";
    public const string scenePath = "Assets/Scenes/Coop Test.unity";

    [MenuItem("Cooked Fast/Online/3 - Test Sahnesini Kur", priority = 242)]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Online",
                "Play modundayken calismaz. Once durdur.", "Tamam");

            return;
        }

        // Asked before anything is created, because the next line closes
        // whatever scene is open. The kitchen has months of hand placed work in
        // it and a silent close is not a risk worth taking for a test room
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string report = "";

        Prefab(ref report);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        Room(ref report);

        GameObject canvas = CoopCanvas.Build("Coop Menu", 900);

        CoopPanels.Build(canvas.transform, false);

        report += "- Ekranlar: secim, oda kodu, kod girisi, bekleme, hata, oyun\n";

        if (!System.IO.Directory.Exists("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        EditorSceneManager.SaveScene(scene, scenePath);

        report += "\nSahne: " + scenePath + "\n";

        report += "\nNASIL TEST EDILIR\n" +
                  "1. Window > Multiplayer > Multiplayer Play Mode\n" +
                  "2. Player 2'yi acik hale getir ve pencerenin yuklenmesini bekle\n" +
                  "3. Ana editorde Play'e bas\n" +
                  "4. Birinde ODA KUR, kod ciksin\n" +
                  "5. Digerinde KODLA KATIL, kodu yapistir\n" +
                  "6. Zemine tikla: kapsul iki ekranda da ayni yere gitmeli\n\n" +
                  "Yesil kapsul her ekranda o ekranin oyuncusudur.\n" +
                  "Tiklama once hosta gidiyor, hareketi host yapiyor --\n" +
                  "kapsul aninda kimildarsa baglanti degil, kod yanlis demektir.";

        Debug.Log("Online test sahnesi\n" + report);
        EditorUtility.DisplayDialog("Online test sahnesi", report, "Tamam");
    }

    // ---- the capsule --------------------------------------------------------

    // Public, because the main menu command needs it too: connecting from
    // the menu spawns this same prefab, and a prefab without the badge on it
    // is a second player whose character never arrives. Rebuilt rather than
    // patched, so both commands leave exactly the same asset behind
    public static void Prefab(ref string report)
    {
#if !COOP_ONLINE
        report += "- ATLANDI: kapsul prefabi kurulamadi, paketler yok\n" +
                  "  Cooked Fast > Online > 1 - Paketleri Kur\n" +
                  "  Paketler gelince bu komutu tekrar calistir\n";
#else
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Online"))
            AssetDatabase.CreateFolder("Assets/Resources", "Online");

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);

        capsule.name = "Coop Player";

        // Half a capsule above the floor. A primitive is built around its own
        // middle, so a prefab saved at zero spawns both players buried to the
        // waist -- and the first thought on seeing that is that the ground is
        // wrong, not the pivot
        capsule.transform.position = new Vector3(0f, 1f, 0f);

        // Taken off rather than left on. The tap that moves the capsule is a
        // raycast at the floor, and a collider on the capsule catches taps
        // aimed past it -- so standing between the camera and where you wanted
        // to go would stop you going there
        Object.DestroyImmediate(capsule.GetComponent<Collider>());

        capsule.AddComponent<NetworkObject>();
        capsule.AddComponent<NetworkTransform>();
        capsule.AddComponent<CoopCapsule>();

        // What the other player IS, as opposed to where they are. It lives on
        // the player object because that is the one thing Netcode creates and
        // destroys exactly once per player, with no code of ours deciding when
        capsule.AddComponent<CoopPlayerBadge>();

        PrefabUtility.SaveAsPrefabAsset(capsule, prefabPath);

        Object.DestroyImmediate(capsule);

        report += "- Kapsul prefabi: " + prefabPath + "\n" +
                  "  Resources altinda, cunku atanacak bir sahne yok:\n" +
                  "  NetworkManager da calisirken kuruluyor\n";
#endif
    }

    // ---- the room -----------------------------------------------------------

    private static void Room(ref string report)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);

        floor.name = "Floor";
        floor.transform.localScale = new Vector3(3f, 1f, 3f);

        // The flag that says "capsules are welcome here".
        //
        // Without it the same player prefab would put two grey capsules in the
        // middle of the kitchen the moment co-op connected from the main menu.
        // The capsule is scaffolding: it should only be visible in the room
        // that was built for it
        floor.AddComponent<CoopTestRoom>();

        GameObject camera = new GameObject("Main Camera");

        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 14f, -11f);
        camera.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

        Camera eye = camera.AddComponent<Camera>();

        eye.clearFlags = CameraClearFlags.SolidColor;
        eye.backgroundColor = new Color(.16f, .17f, .22f);

        camera.AddComponent<AudioListener>();

        GameObject sun = new GameObject("Directional Light");

        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light light = sun.AddComponent<Light>();

        light.type = LightType.Directional;
        light.intensity = 1.1f;

        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject events = new GameObject("EventSystem");

            events.AddComponent<EventSystem>();

            // The Input System module, not the old StandaloneInputModule.
            //
            // This project is set to "Input System Package (New)" only, and the
            // old module reads UnityEngine.Input -- which throws on the first
            // frame it ticks. Not recognisable as a UI bug when it happens: the
            // exception comes out of EventSystem.Update and mentions nothing
            // that was built here
            events.AddComponent<InputSystemUIInputModule>();
        }

        report += "- Oda: zemin, kamera, isik, EventSystem\n";
    }
}
#endif
