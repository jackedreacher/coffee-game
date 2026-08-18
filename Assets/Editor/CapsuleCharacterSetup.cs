#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Moves the cast over to the capsule animals, and the worker onto the waiter
// animations.
//
// The whole thing rests on one fact that took some digging to establish: the
// capsule animals, the waiter clips and the old Hypercasual character are ALL
// imported as Humanoid. Unity retargets a humanoid clip through an intermediate
// muscle space rather than through bone names, so any clip can drive any of
// them and nothing has to be re-rigged. Where that stops being true is the
// existing RABBITS -- Rabbit_Bald.fbx and its siblings are Generic, and their
// thirty baked clips cannot come along. That is not a bug to fix, it is the
// price of the swap, and it is why the customer controller below is built out
// of the Hypercasual clips instead.
//
// The other useful accident: all fifteen animals copy one avatar, sourced from
// DGN_Bear_Outline. One rig, fifteen skins. Changing species is changing a mesh
// and nothing else, which is what makes a queue of fifteen different animals
// cost the same as a queue of one
public static class CapsuleCharacterSetup
{
    // ---- where everything lives ---------------------------------------------

    private const string animalFolder =
        "Assets/DGN_15_CapsuleAnimals/Models/Characters/Outlined_Characters/";

    // Every other animal copies its avatar from this one, so this file is the
    // rig for all fifteen. Assigning any other model's avatar would assign
    // nothing -- with avatarSetup: 2 the importer does not create one
    private const string avatarSource = animalFolder + "DGN_Bear_Outline.fbx";

    private const string humanClips =
        "Assets/Tiny Coffee Shop/Animations/Hypercasual Character v2.fbx";

    private const string sitClip =
        "Assets/Tiny Coffee Shop/Animations/Male@Fast Run@Sitting.fbx";

    private const string waiterFolder =
        "Assets/VFXPACK_FIRE_WALLCOEUR/Waiter_Anims/Art/Animations/";

    // The pack's own two clips, and they are worth more than either of the
    // other libraries for the states they cover.
    //
    // Everything else here is a clip authored on a human and then bent onto a
    // body with legs a third the length. Retargeting matches muscle ANGLES, so
    // a human's hip swing arrives on these stumps as a much wider stance than
    // anybody drew -- which is the splayed walk, and it is not a bug in the
    // mapping. It is what asking one body to move like a differently shaped one
    // looks like.
    //
    // These two were animated on THIS skeleton. Nothing is being converted, so
    // there is nothing to come out wrong
    private const string ownWalk =
        "Assets/DGN_15_CapsuleAnimals/Animations/Character@Test_Walking.fbx";

    private const string ownIdle =
        "Assets/DGN_15_CapsuleAnimals/Animations/Character@Test_LookingAround.fbx";

    private const string outputFolder = "Assets/Tiny Coffee Shop/Animations/Capsule";

    private const string customerController = outputFolder + "/Capsule Customer.controller";
    private const string workerController = outputFolder + "/Capsule Worker.controller";

    // The five names CustomerAnimator plays by string. They are not a
    // convention, they are an interface -- animator.Play("Walk") fails silently
    // against a state called anything else, and a customer that stands still
    // while gliding across the floor is what that failure looks like
    private static readonly string[] states =
        { "Walk", "Idle", "WalkWithPlateau", "IdleWithPlateau", "Sit" };

    // Everywhere a clip might be worth trying, named once so the browser window
    // and the controller builder are looking at the same shelf. A folder or a
    // single file -- the reader handles both, because "the waiter pack" is a
    // directory and "the sitting animation" is one FBX with one clip in it
    internal static readonly string[][] Libraries =
    {
        new[] { "Paketin kendi klipleri", "Assets/DGN_15_CapsuleAnimals/Animations" },
        new[] { "Waiter", waiterFolder },
        new[] { "Hypercasual (eski karakter)", humanClips },
        new[] { "Oturma", sitClip },
    };

    // ---- 1: look at it before believing it ----------------------------------

    // The one thing that cannot be read off disk.
    //
    // Humanoid retargeting always SUCCEEDS -- it never errors, it just produces
    // a pose. Whether that pose is any good is a different question, and these
    // clips were authored on a realistic waiter while these characters have a
    // head the size of their torso and arms that stop at the elbow. The arms
    // may well pass through the body. Fifteen minutes of prefab surgery is the
    // wrong way to find that out, so this is the cheap way: one animal, one
    // clip, in an empty scene
    [MenuItem("Cooked Fast/Karakter/1 - Retargeti Dene", priority = 700)]
    public static void Try()
    {
        Sample("Rabbit", waiterFolder + "Waiter_Tray_Walk_Forward.fbx", "Tepsili yuruyus", true);
    }

    [MenuItem("Cooked Fast/Karakter/1b - Retargeti Dene (bos elli)", priority = 701)]
    public static void TryEmpty()
    {
        Sample("Rabbit", ownWalk, "Bos elli yuruyus (paketin kendi klibi)");
    }

    internal const string testName = "RETARGET TEST";

    // The fifteen, in the order the pack ships them
    internal static readonly string[] Animals =
    {
        "Bear", "Beaver", "Bull", "Cat", "Cow", "Deer", "Dog", "Fox",
        "Koala", "Mouse", "Panda", "Pig", "Rabbit", "Ram", "Squirrel",
    };

    private static void Sample(string animal, string clipPath, string label, bool tray = false)
    {
        AnimationClip clip = FirstClip(clipPath, clipPath == humanClips ? "Walk" : null);

        if (clip == null)
        {
            Show("Klip bulunamadi:\n" + clipPath);
            return;
        }

        TraySetup setup = TraySetup.Default;

        setup.on = tray;

        if (Preview(animal, clip, setup, out string trouble))
            Show(label + " -- " + animal + " uzerinde.\n\n" +
                 "Klip: " + clip.name + "\n\n" +
                 "SCENE penceresinde bak. Bitince:\n" +
                 "Cooked Fast > Karakter > 1c - Test Objesini Sil");
        else
            Show(trouble);
    }

    // One animal, one clip, put down somewhere you can see it.
    //
    // Split out from the menu command so the browser window can drive it too --
    // clicking a name in a list and picking a menu item are the same request,
    // and there is no reason for them to be two pieces of code that drift
    internal const string plateauPrefab =
        "Assets/Tiny Coffee Shop/Prefabs/GamePlay/Plateau.prefab";

    internal const string trayName = "TEST PLATEAU";

    // An empty between the hand bone and the tray.
    //
    // The tray used to hang straight off the bone, which meant one transform
    // was answering two different questions: where this hand holds things, and
    // what is currently being held. Splitting them buys three things.
    //
    // The placement stops belonging to the tray. Swap the tray for a plate or a
    // cup and it goes on at zero -- the socket already knows where a hand's
    // grip is, and that is a fact about the CHARACTER, not about the prop.
    //
    // The scale compensation lives in one place. These bones are scaled a long
    // way from one, so something has to divide by that; done on the socket, the
    // prop underneath sits at its own authored scale and every number in its
    // inspector is a number you can read.
    //
    // And it can be dragged. A socket is a selectable object in the hierarchy
    // with a normal move and rotate gizmo, so the placement can be done by
    // grabbing it while the animation plays -- which is a far better way to
    // answer "is it in the paw" than typing Vector3 fields at it
    internal const string socketName = "PLATEAU SOKET";

    // Where the tray goes, as numbers somebody can change.
    //
    // The first attempt computed all of this -- right hand by convention, scale
    // by dividing the prefab's own size by the bone's -- and every part of that
    // was a guess dressed as arithmetic. Which hand a carry animation uses is a
    // property of the ANIMATION and this code cannot read it; how big a tray
    // should look in a paw is a thing you settle by looking at it.
    //
    // So it is a struct with defaults and a panel that edits them, the same
    // bargain the ready tick ended up with: the computer holds the numbers, the
    // person who can see the result chooses them
    internal struct TraySetup
    {
        public bool on;
        public bool right;
        public Vector3 place;
        public Vector3 turn;

        // 0 means work it out from the character's own height, which is a
        // better starting guess than any fixed number -- a mouse and a bear are
        // not the same size and neither should their trays be
        public float size;

        public static TraySetup Default => new TraySetup { on = true, right = true };
    }

    // One signature, deliberately.
    //
    // There were three of these for a moment -- one taking nothing, one taking
    // a bool, one taking the struct -- and "default" is a legal argument for
    // both the bool and the struct, so the compiler could not tell which was
    // meant. Convenience overloads that differ only in how little you pass are
    // exactly where that happens, and the callers here are two
    internal static bool Preview(string animal, AnimationClip clip, TraySetup tray, out string trouble)
    {
        trouble = "";

        // Refused in play mode, because an object created there is gone the
        // moment play stops -- and the person watching it vanish reasonably
        // concludes the command did not work
        if (EditorApplication.isPlaying)
        {
            trouble = "Play modundayken calismaz -- olusan obje Play bitince kaybolur.\n" +
                      "Once durdur, komutu calistir, sonra Play'e bas.";
            return false;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model(animal));

        if (model == null)
        {
            trouble = "Model bulunamadi:\n" + Model(animal);
            return false;
        }

        if (clip == null)
        {
            trouble = "Klip yok.";
            return false;
        }

        Avatar avatar = LoadAvatar();

        if (avatar == null)
        {
            trouble = "Avatar bulunamadi:\n" + avatarSource +
                      "\n\nBu dosya on besinin de rig'i, olmadan hicbiri oynatilamaz.";
            return false;
        }

        Clear();

        GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(model);

        copy.name = testName + " - " + animal;

        // Beside the kitchen, not in it.
        //
        // This used to sit at the world origin, and the world origin in the
        // Kitchen scene is the middle of the floor -- so the test model was
        // created standing inside whoever happened to be walking through it,
        // which reads as the command having done something very strange rather
        // than having put an object down carelessly. Measured off what is
        // actually in the scene, so it stays clear whatever the room is
        copy.transform.position = Clearing();

        Undo.RegisterCreatedObjectUndo(copy, "Retarget test");

        Directory.CreateDirectory(outputFolder);
        AssetDatabase.Refresh();

        // Deleted first, or Unity quietly makes "Retarget Test 1", then 2, and
        // the folder fills up with the same asset while the scene keeps
        // pointing at the oldest one
        AssetDatabase.DeleteAsset(outputFolder + "/Retarget Test.controller");

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(
                outputFolder + "/Retarget Test.controller");

        AnimatorState test = controller.layers[0].stateMachine.AddState("Test");

        test.motion = clip;

        // Same as the real controllers, or the test would be showing something
        // the game will never look like
        test.iKOnFeet = true;

        Animator animator = copy.GetComponent<Animator>();

        if (animator == null)
            animator = copy.AddComponent<Animator>();

        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;

        // Off, always. Position comes from the NavMeshAgent everywhere else in
        // this game, and a clip that also moves the root fights it
        animator.applyRootMotion = false;

        if (tray.on)
            HandTray(animator, copy, tray);

        Selection.activeGameObject = copy;

        // Framed, so it is looked at rather than looked for. The whole point of
        // this command is a thing you SEE, and it has just been put somewhere
        // deliberately out of the way
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return true;
    }

    [MenuItem("Cooked Fast/Karakter/1c - Test Objesini Sil", priority = 702)]
    public static void ClearTest()
    {
        int gone = Clear();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Show(gone <= 0
            ? "Sahnede test objesi yok."
            : gone + " test objesi silindi.");
    }

    // What Unity ACTUALLY mapped, asked of Unity rather than guessed at.
    //
    // The meta file says "human: []" and "skeleton: []", which means nobody has
    // ever opened Configure on this rig -- the humanoid mapping is being
    // regenerated from bone names on every import. Auto mapping is a heuristic
    // and it is at its worst on stylised bodies: it reads a name it does not
    // recognise and simply leaves that bone out, silently. A missing Toes is
    // the classic one, and a foot with no toe below it is a foot the retarget
    // has nothing to orient against.
    //
    // GetBoneTransform is the only honest way to see the result. It answers
    // from the avatar that got built, not from what anyone intended
    [MenuItem("Cooked Fast/Karakter/1d - Avatar Eslemesini Denetle", priority = 703)]
    public static void Audit()
    {
        Avatar avatar = LoadAvatar();

        if (avatar == null)
        {
            Show("Avatar bulunamadi:\n" + avatarSource);
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(avatarSource);

        if (model == null)
        {
            Show("Model bulunamadi:\n" + avatarSource);
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("DGN_Bear_Outline -- on besinin de rig'i");
        report.AppendLine("  gecerli : " + avatar.isValid);
        report.AppendLine("  humanoid: " + avatar.isHuman);
        report.AppendLine();

        GameObject probe = Object.Instantiate(model);

        try
        {
            Animator animator = probe.GetComponent<Animator>();

            if (animator == null)
                animator = probe.AddComponent<Animator>();

            animator.avatar = avatar;

            HumanBodyBones[] wanted =
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                HumanBodyBones.Neck, HumanBodyBones.Head,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
                HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
            };

            int missing = 0;

            foreach (HumanBodyBones bone in wanted)
            {
                Transform found = animator.GetBoneTransform(bone);

                if (found == null)
                    missing++;

                report.AppendLine("  " + bone.ToString().PadRight(14) +
                                  (found == null ? "YOK" : found.name));
            }

            report.AppendLine();

            report.AppendLine(missing <= 0
                ? "Butun temel kemikler eslenmis."
                : missing + " kemik eslenmemis -- retarget onlari tahmin ediyor.");
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }

        report.AppendLine();
        report.AppendLine("ELDE DUZELTMEK ICIN:");
        report.AppendLine("  " + avatarSource);
        report.AppendLine("  > Inspector > Rig > Configure...");
        report.AppendLine("  Eksik kemikleri elle bagla, sonra Pose > Enforce T-Pose,");
        report.AppendLine("  Apply.");
        report.AppendLine();
        report.AppendLine("BU DOSYA ON BESININ DE RIG'I. Diger 14 hayvan avatarini");
        report.AppendLine("buradan kopyaliyor, yani burayi bir kere duzeltmek");
        report.AppendLine("hepsini birden duzeltir.");

        Show(report.ToString());
    }

    // Every run cleans up after the last one, so pressing the command twice
    // leaves one model rather than a crowd
    internal static int Clear()
    {
        int gone = 0;

        foreach (GameObject found in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (found == null || !found.name.StartsWith(testName))
                continue;

            Undo.DestroyObjectImmediate(found);
            gone++;
        }

        return gone;
    }

    // Somewhere with nothing in it, found by measuring what IS in the scene:
    // one clear model's width past the far edge of everything, at floor level.
    // A fixed offset would have been a guess about a room this has never seen
    private static Vector3 Clearing()
    {
        Bounds room = default;
        bool any = false;

        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!any)
            {
                room = renderer.bounds;
                any = true;
                continue;
            }

            room.Encapsulate(renderer.bounds);
        }

        if (!any)
            return Vector3.zero;

        return new Vector3(room.max.x + 2f, room.min.y, room.center.z);
    }

    // ---- 2: the two controllers ---------------------------------------------

    [MenuItem("Cooked Fast/Karakter/2 - Animator Controllerlarini Uret", priority = 706)]
    public static void BuildControllers()
    {
        StringBuilder report = new StringBuilder();

        Directory.CreateDirectory(outputFolder);
        AssetDatabase.Refresh();

        // The customer. Empty handed walking and standing come from the pack's
        // own clips -- those are the two states a customer spends nearly all
        // its life in, and they are the two the capsule animals were actually
        // animated for. The carrying pair still has to be retargeted, because
        // nobody has ever animated one of these holding a tray
        report.Append(Controller(customerController, "MUSTERI", new Dictionary<string, string[]>
        {
            { "Walk", new[] { ownWalk, null } },
            { "Idle", new[] { ownIdle, null } },
            { "WalkWithPlateau", new[] { humanClips, "WalkWithPlateau" } },
            { "IdleWithPlateau", new[] { humanClips, "IdleWithPlateau" } },
            { "Sit", new[] { sitClip, null } },
        }));

        report.AppendLine();

        // The worker. Carrying is where the waiter pack earns its place: a tray
        // is exactly what a plateau is, and Tray_Walk_Forward is a person
        // carrying one rather than a person walking with their arms held out.
        // The two empty handed states stay on the old clips for the reason above
        report.Append(Controller(workerController, "CALISAN", new Dictionary<string, string[]>
        {
            { "Walk", new[] { ownWalk, null } },
            { "Idle", new[] { ownIdle, null } },
            // Carrying uses the pack's own clips too, and the tray is left to
            // the plateau object rather than to the animation.
            //
            // Waiter_Tray_Walk_Forward is a person holding a tray out in front
            // of their chest, and it retargets onto these bodies with the hands
            // INSIDE the torso. That is not a rigging fault and no avatar work
            // fixes it: the arm on a capsule animal is shorter than the radius
            // of its own body, so an angle that puts a human's hand in front of
            // their chest cannot put this one anywhere but inside it.
            //
            // The plateau is a separate object parented into a hand bone, so it
            // arrives in the hand whatever the arms are doing. A character
            // walking correctly with a tray held at hand height reads as
            // carrying; a character whose arms are buried in its own stomach
            // reads as broken. Between an animation that is right and a pose
            // that is right, the pose wins -- it is the thing being looked at.
            //
            // To go back: swap these two lines for
            //   waiterFolder + "Waiter_Tray_Walk_Forward.fbx"
            //   waiterFolder + "Waiter_Tray_Idle.fbx"
            { "WalkWithPlateau", new[] { ownWalk, null } },
            { "IdleWithPlateau", new[] { ownIdle, null } },
            { "Sit", new[] { sitClip, null } },
        }));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine("Ikisinde de moveSpeed float parametresi var ve");
        report.AppendLine("Apply Root Motion kapali olmali -- konumu NavMeshAgent");
        report.AppendLine("suruyor, klip de surerse ikisi kavga eder.");
        report.AppendLine();
        report.AppendLine("Sonraki: Cooked Fast > Karakter > 3");

        Show(report.ToString());
    }

    private static string Controller(string path, string label, Dictionary<string, string[]> map)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(label + "  ->  " + Path.GetFileName(path));

        // Rebuilt rather than patched. A controller half wired to old clips and
        // half to new ones is a shape nobody authored and nobody can read
        AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("moveSpeed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // No transitions between them on purpose.
        //
        // CustomerAnimator does not use transitions -- it calls Play by name
        // every frame, which cuts straight to the state. Wiring conditions here
        // as well would mean two things steering one machine, and the one that
        // loses is whichever the reader was not thinking about
        foreach (string state in states)
        {
            if (!map.TryGetValue(state, out string[] source))
                continue;

            AnimationClip clip = FirstClip(source[0], source[1]);

            AnimatorState added = machine.AddState(state);

            if (clip == null)
            {
                report.AppendLine("  " + state.PadRight(16) + "KLIP YOK: " + source[0] +
                                  (source[1] == null ? "" : "  (" + source[1] + ")"));
                continue;
            }

            added.motion = clip;

            // Foot IK, on every state.
            //
            // This is the answer to feet that come out crooked. Retargeting
            // matches MUSCLE ANGLES, not positions -- it takes the angle the
            // human's ankle was at and puts the animal's ankle at the same
            // angle. On a body with legs a third the length that lands the foot
            // somewhere the human's never went, tilted and off the floor.
            //
            // Foot IK runs afterwards and solves the feet back onto the ground
            // plane, which is the correction the retarget cannot make for
            // itself because it never knew where the ground was
            added.iKOnFeet = true;

            report.AppendLine("  " + state.PadRight(16) + clip.name);
        }

        // Whatever ended up first would otherwise be the default, and that is
        // decided by dictionary order -- which is to say by nothing
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name != "Idle")
                continue;

            machine.defaultState = child.state;
            break;
        }

        EditorUtility.SetDirty(controller);

        return report.ToString();
    }

    // ---- 3: the swap ---------------------------------------------------------

    [MenuItem("Cooked Fast/Karakter/3 - Karakterleri Kapsul Hayvanlara Cevir", priority = 707)]
    public static void Convert()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(customerController) == null)
        {
            Show("Once 2. komutu calistir -- controllerlar yok.");
            return;
        }

        // Named out loud before anything is touched. This edits prefabs, and a
        // prefab in this project carries hand placed work that cannot be got
        // back from git
        if (!EditorUtility.DisplayDialog("Karakterleri Cevir",
                "Su prefablarin modeli degisecek:\n\n" +
                "  7 musteri  -> her birine farkli hayvan\n" +
                "  Player     -> Panda\n" +
                "  Worker     -> Bear\n\n" +
                "ESKI MODEL SILINMIYOR. Kapatilip adinin sonuna\n" +
                "\"(ESKI - kontrol et, sonra sil)\" yaziliyor. Ustunde\n" +
                "elle koydugun bir sey kalmis mi bakarsin, sonra sen\n" +
                "silersin.\n\n" +
                "Plateau'lar koke tasinacak -- eski elin kemigine\n" +
                "bagliydilar. Yeniden baglamak icin:\n" +
                "  Cooked Fast > Arac > Attach Plateaus To Hands\n\n" +
                "Devam edilsin mi?",
                "Cevir", "Vazgec"))
            return;

        StringBuilder report = new StringBuilder();

        string[] paths = Prefabs();

        // Lines up with Prefabs(): seven customers, then Player, then Worker.
        // Fifteen animals for seven customers, spread across the list rather
        // than taken off the front, so the queue does not read as one family
        string[] chosen =
        {
            "Rabbit", "Fox", "Cat", "Deer", "Koala", "Squirrel", "Mouse",
            "Panda", "Bear",
        };

        for (int i = 0; i < paths.Length && i < chosen.Length; i++)
        {
            report.Append(Swap(paths[i], chosen[i],
                i < paths.Length - 2 ? customerController : workerController));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine("SIRADAKI, ELDE:");
        report.AppendLine("  1. Cooked Fast > Arac > Attach Plateaus To Hands");
        report.AppendLine("  2. Play'e bas, yuruyus ve tepsi tutusuna bak");
        report.AppendLine();
        report.AppendLine("Prefab adlari hala Customer_Rabbit_* -- icerideki");
        report.AppendLine("hayvan degisti ama dosya adini degistirmedim,");
        report.AppendLine("sahnedeki ve koddaki referanslar kirilmasin diye.");

        Show(report.ToString());
    }

    // One prefab: new mesh, same everything else.
    //
    // Done through LoadPrefabContents rather than by editing an instance,
    // because that opens the prefab in isolation and hands back a real
    // hierarchy to work on -- the alternative is applying overrides from a
    // scene instance, which quietly picks up whatever else that instance had
    // been nudged into
    private static string Swap(string prefabPath, string animal, string controllerPath)
    {
        string name = Path.GetFileNameWithoutExtension(prefabPath);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return "- " + name + ": PREFAB YOK, atlandi\n";

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model(animal));

        if (model == null)
            return "- " + name + ": " + animal + " modeli yok, atlandi\n";

        Avatar avatar = LoadAvatar();
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Animator old = root.GetComponentInChildren<Animator>(true);

            if (old == null)
                return "- " + name + ": Animator bulunamadi, atlandi\n";

            Transform host = old.transform.parent == null ? root.transform : old.transform.parent;

            Vector3 place = old.transform.localPosition;
            Quaternion turn = old.transform.localRotation;
            Vector3 size = old.transform.localScale;

            // Lifted out of the old hand before anything else happens.
            //
            // The plateau is parented to a hand BONE, and that bone belongs to
            // the body being replaced. Moved up to the root it keeps its
            // settings and its references and loses only its place in a hand --
            // which is the one thing there is already a command for. Found by
            // reading the field rather than by guessing which child looks like
            // a tray, because a wrong guess here moves a finger
            string rescued = Rescue(root);

            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model, host);

            body.name = "Body";
            body.transform.localPosition = place;
            body.transform.localRotation = turn;
            body.transform.localScale = size;

            Animator fresh = body.GetComponent<Animator>();

            if (fresh == null)
                fresh = body.AddComponent<Animator>();

            fresh.avatar = avatar;
            fresh.runtimeAnimatorController = controller;
            fresh.applyRootMotion = false;

            string retired = Retire(root, body);

            string wired = Rewire(root, fresh, old);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            return "- " + name + ": " + animal + wired + rescued + retired + "\n";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private const string oldSuffix = " (ESKI - kontrol et, sonra sil)";

    // Every body the prefab is wearing, not just the first one found.
    //
    // This is the bug that put two characters on screen at once. The swap asked
    // for GetComponentInChildren<Animator> and got ONE -- and a customer prefab
    // turns out to carry two rigged bodies, the Hypercasual humanoid that drove
    // the animation and a Rabbit_*_Visual that was the actual look. Switching
    // off the first left the second standing, so the new animal was drawn
    // inside the old rabbit and both rendered.
    //
    // A skinned mesh is the honest test for "this is a character body". The
    // plateau is a plain mesh and the dust trail is particles, so nothing that
    // is merely being CARRIED can be mistaken for a body and switched off --
    // which is the failure that would actually cost something here
    private static List<GameObject> Bodies(GameObject root, GameObject keep)
    {
        List<GameObject> found = new List<GameObject>();

        foreach (SkinnedMeshRenderer skin in
                 root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin == null)
                continue;

            GameObject owner = Owner(root, skin.transform);

            if (owner == null || owner == keep || owner == root || found.Contains(owner))
                continue;

            found.Add(owner);
        }

        return found;
    }

    // The top of the model instance a skin belongs to, which is the child of
    // the root -- a skin lives several bones deep and disabling the bone would
    // leave the rest of the body behind
    private static GameObject Owner(GameObject root, Transform what)
    {
        Transform walk = what;

        while (walk != null && walk.parent != null && walk.parent != root.transform)
            walk = walk.parent;

        return walk == null ? null : walk.gameObject;
    }

    // NOT destroyed. Switched off and renamed.
    //
    // This is somebody's prefab with hand placed work on it, and the thing
    // being removed is the skeleton every one of those hand placements was
    // measured against. Deleting it is a decision that cannot be walked back
    // from a dialog box, so the old body stays in the prefab, disabled, with
    // "sil" in its name -- anything still hanging off it can be pulled across,
    // and deleting it afterwards is one click that the person who can SEE the
    // result gets to make
    private static string Retire(GameObject root, GameObject keep)
    {
        StringBuilder said = new StringBuilder();

        foreach (GameObject body in Bodies(root, keep))
        {
            if (!body.activeSelf && body.name.EndsWith(oldSuffix))
                continue;

            if (!body.name.EndsWith(oldSuffix))
                body.name = body.name + oldSuffix;

            body.SetActive(false);

            said.Append("\n    kapatildi: " + body.name);
        }

        return said.ToString();
    }

    // The nine, in one place, because two commands now walk the same list
    private static string[] Prefabs()
    {
        string folder = "Assets/Tiny Coffee Shop/Prefabs/Characters/";

        string[] customers =
        {
            "Bald", "Blond", "Cyan", "Green", "Grey", "Pink", "Purple",
        };

        List<string> all = new List<string>();

        foreach (string customer in customers)
            all.Add(folder + "Customers/Customer_Rabbit_" + customer + ".prefab");

        all.Add(folder + "Player.prefab");
        all.Add(folder + "Worker.prefab");

        return all.ToArray();
    }

    // For the prefabs that were already converted by the version that missed
    // the second body. Idempotent -- it switches off what is still on and says
    // nothing about what is already off, so running it twice costs nothing
    [MenuItem("Cooked Fast/Karakter/3b - Kalan Eski Govdeleri Kapat", priority = 708)]
    public static void RetireLeftovers()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("ESKI GOVDELER");
        report.AppendLine();

        int touched = 0;

        foreach (string path in Prefabs())
        {
            string name = Path.GetFileNameWithoutExtension(path);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                report.AppendLine("- " + name + ": PREFAB YOK");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Transform kept = root.transform.Find("Body");

                if (kept == null)
                {
                    report.AppendLine("- " + name + ": Body yok -- once 3. komut");
                    continue;
                }

                string said = Retire(root, kept.gameObject);

                if (string.IsNullOrEmpty(said))
                {
                    report.AppendLine("- " + name + ": temiz");
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);

                touched++;

                report.AppendLine("- " + name + ":" + said);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine(touched + " prefab degisti.");
        report.AppendLine();
        report.AppendLine("Sahnedeki musteriler CANLI uretiliyor, yani Play'e");
        report.AppendLine("tekrar basinca duzelmis olarak gelirler.");

        Show(report.ToString());
    }

    private const string capsuleSuffix = " (KAPSUL - kapali)";

    // Back to the bodies the prefabs came with.
    //
    // Nothing was destroyed on the way in, so nothing has to be rebuilt on the
    // way out -- the old bodies are still sitting in every prefab wearing the
    // ESKI suffix. This takes the suffix off, switches the right one back on
    // and switches the capsule off.
    //
    // Which one is "the right one" was read out of git rather than guessed at,
    // and the answer was not what the swap assumed. In the seven customers the
    // live body is Rabbit_*_Visual and the Hypercasual character was ALREADY
    // disabled before any of this -- so the swap took the first Animator it
    // found, disabled a body that was off, and left the one that was on. That
    // is the whole reason two characters ended up on screen. Player and Worker
    // have no Visual, and there the Hypercasual really is the live one
    [MenuItem("Cooked Fast/Karakter/3c - Kapsul Govdeyi Geri Al", priority = 709)]
    public static void Revert()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Kapsul Govdeyi Geri Al",
                "9 prefab eski govdesine donecek:\n\n" +
                "  Rabbit_* Visual / Hypercasual  -> geri acilir\n" +
                "  Body (kapsul hayvan)           -> kapatilir\n\n" +
                "HICBIR SEY SILINMIYOR. Kapsul govde prefabta kaliyor,\n" +
                "adinin sonuna \"(KAPSUL - kapali)\" yaziliyor. Fikrin\n" +
                "degisirse geri almak yine tek komut.\n\n" +
                "Devam edilsin mi?",
                "Geri Al", "Vazgec"))
            return;

        StringBuilder report = new StringBuilder();

        report.AppendLine("ESKI GOVDELERE DONUS");
        report.AppendLine();

        foreach (string path in Prefabs())
        {
            string name = Path.GetFileNameWithoutExtension(path);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                report.AppendLine("- " + name + ": PREFAB YOK");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                string said = Unswap(root, name);

                report.Append(said);

                if (!said.Contains("atlandi"))
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine("SIRADAKI:");
        report.AppendLine("  Cooked Fast > Arac > Attach Plateaus To Hands");
        report.AppendLine();
        report.AppendLine("Cevirme sirasinda plateau'lar eski elin kemiginden");
        report.AppendLine("koke tasinmisti. Eski govde geri geldigine gore o");
        report.AppendLine("kemik de geri geldi, komut tepsileri yerine koyar.");

        Show(report.ToString());
    }

    private static string Unswap(GameObject root, string name)
    {
        // The capsule is included in the search on purpose -- it is the one
        // that has to end up off, so it cannot be excluded from the list
        List<GameObject> bodies = Bodies(root, null);

        GameObject capsule = null;
        List<GameObject> old = new List<GameObject>();

        foreach (GameObject body in bodies)
        {
            if (body.name == "Body" || body.name.EndsWith(capsuleSuffix))
            {
                capsule = body;
                continue;
            }

            old.Add(body);
        }

        if (old.Count <= 0)
            return "- " + name + ": eski govde yok, atlandi\n";

        GameObject live = null;

        foreach (GameObject body in old)
        {
            if (body.name.Contains("Visual"))
            {
                live = body;
                break;
            }
        }

        // No Visual and more than one candidate is a shape this has not seen.
        // Guessing which body a prefab wears is exactly the mistake that got us
        // here, so it says so and changes nothing
        if (live == null && old.Count > 1)
            return "- " + name + ": hangi govde canli belirsiz, ELLE bak, atlandi\n";

        live ??= old[0];

        StringBuilder said = new StringBuilder();

        said.Append("- " + name + ":");

        foreach (GameObject body in old)
        {
            if (body.name.EndsWith(oldSuffix))
                body.name = body.name.Substring(0, body.name.Length - oldSuffix.Length);

            bool on = body == live;

            body.SetActive(on);

            said.Append("\n    " + (on ? "ACILDI : " : "kapali : ") + body.name);
        }

        if (capsule != null)
        {
            if (!capsule.name.EndsWith(capsuleSuffix))
                capsule.name = capsule.name + capsuleSuffix;

            capsule.SetActive(false);

            said.Append("\n    kapatildi: " + capsule.name);

            // Only the fields that were pointed at the capsule come back. One
            // that was left alone on the way in gets left alone on the way out
            Animator back = live.GetComponent<Animator>();

            if (back != null)
                said.Append(Rewire(root, back, capsule.GetComponent<Animator>()));
        }

        said.AppendLine();

        return said.ToString();
    }

    // The real tray, in the test animal's hand.
    //
    // Worth having because the question the carry clips are being judged on is
    // not "where are the hands" -- it is "does this read as somebody carrying a
    // tray", and an empty hand cannot answer that. Arms buried in a stomach
    // look broken on their own and can look perfectly fine the moment there is
    // a tray sitting where the hand is.
    //
    // The hand comes from the AVATAR rather than from a name search. The
    // project's own attach command hunts for a transform called something like
    // "hand", which is what you do when you have no humanoid mapping to ask --
    // and here there is one, and it has already been checked
    private static string HandTray(Animator animator, GameObject body, TraySetup setup)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(plateauPrefab);

        if (asset == null)
            return "plateau prefabi yok: " + plateauPrefab;

        Transform hand = animator.GetBoneTransform(
            setup.right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);

        if (hand == null)
            return "el kemigi yok";

        // Reused if one is already there.
        //
        // This used to make a new one every call, which was fine while the only
        // caller was "build the whole preview from scratch". Now that a number
        // in a panel can move it, making and destroying a prefab instance per
        // keystroke would fill the undo stack with noise and lose the selection
        GameObject tray = FindTray(body);

        if (tray == null)
        {
            tray = (GameObject)PrefabUtility.InstantiatePrefab(asset);

            // Unpacked, deliberately.
            //
            // Nothing wants a prefab LINK here. This is a preview prop that
            // gets shoved around by hand, and the link's one power is pushing
            // those shoves back into Plateau.prefab -- the real one, the one
            // every customer in the game carries. A stray Apply would resize
            // every tray in the kitchen to whatever a rabbit was holding.
            //
            // It also stops Unity treating it as an added-object override on
            // the model prefab instance it now lives inside, which is the kind
            // of thing that quietly does not survive an asset reimport
            PrefabUtility.UnpackPrefabInstance(
                tray, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            tray.name = trayName;

            Undo.RegisterCreatedObjectUndo(tray, "Test plateau");
        }

        // Out of the hand and back to its authored scale before measuring.
        //
        // Width reads world bounds, so measuring a tray that is already in a
        // hand measures it at whatever scale it was last given -- and dividing
        // by that a second time shrinks it again, and again, until it is a dot.
        // Cheap to avoid: unparent, restore the authored scale, measure that
        tray.transform.SetParent(null, false);
        tray.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        tray.transform.localScale = asset.transform.localScale;

        // Measured from what is actually drawn, at the scale it was authored.
        //
        // The prefab's own transform says nothing useful -- its root sits at
        // scale one while the Renderer underneath is scaled seventy five times.
        // The only number that means anything is how wide the thing LOOKS, and
        // the only way to get that is to look at the renderers
        float drawn = Width(tray);

        float wanted = setup.size > .0001f ? setup.size : Width(body) * .42f;

        float factor = drawn > .0001f && wanted > .0001f ? wanted / drawn : 1f;

        Transform socket = Socket(body, hand);

        // Everything that is a decision goes on the socket.
        //
        // Zero is the right DEFAULT for the same reason PlateauAttach uses it:
        // carry clips are animated assuming the carried thing sits at the hand's
        // origin. It is not always the right final answer, which is why there
        // is an offset at all
        socket.localPosition = setup.place;
        socket.localRotation = Quaternion.Euler(setup.turn);

        // Divided by the bone's own scale, because these rigs scale their bones
        // a long way from one and a local scale of one would produce a tray the
        // size of the room
        Vector3 bone = hand.lossyScale;

        socket.localScale = new Vector3(
            Divide(factor, bone.x),
            Divide(factor, bone.y),
            Divide(factor, bone.z));

        // And the tray goes on at nothing at all.
        //
        // Which also fixes an arithmetic slip that was hiding here: the old code
        // overwrote the tray's own scale with the compensation, so the prefab's
        // authored root scale was measured in but never applied. It happened to
        // come out right because Plateau.prefab's root is at one. Keeping the
        // authored scale on the prop and the compensation on the socket is
        // correct whatever the prefab's root turns out to be
        tray.transform.SetParent(socket, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localRotation = Quaternion.identity;
        tray.transform.localScale = asset.transform.localScale;

        // Said out loud, because "it vanished" and "it is a millimetre wide"
        // and "it is behind the camera" all look identical from the Scene view
        return hand.name +
               "  |  genislik " + wanted.ToString("0.000") +
               "  |  kemik olcegi " + bone.x.ToString("0.000");
    }

    // Found or made, and always on the hand being asked about -- the Sag/Sol
    // toggle moves an existing socket rather than leaving a stray one behind
    private static Transform Socket(GameObject body, Transform hand)
    {
        Transform socket = Child(body, socketName);

        if (socket == null)
        {
            GameObject made = new GameObject(socketName);

            socket = made.transform;

            Undo.RegisterCreatedObjectUndo(made, "Plateau soketi");
        }

        if (socket.parent != hand)
            socket.SetParent(hand, false);

        return socket;
    }

    private static Transform Child(GameObject body, string name)
    {
        foreach (Transform child in body.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == name)
                return child;
        }

        return null;
    }

    private static GameObject FindTray(GameObject body)
    {
        Transform found = Child(body, trayName);

        return found == null ? null : found.gameObject;
    }

    // Remembered between calls, because this is now asked on every repaint of
    // the browser window and the honest answer costs a sweep of every
    // GameObject in the kitchen. Unity's null check covers the object being
    // destroyed underneath us, which is the only way the cache can go stale
    private static GameObject cachedBody;

    private static GameObject TestBody()
    {
        if (cachedBody != null && cachedBody.name.StartsWith(testName))
            return cachedBody;

        cachedBody = null;

        foreach (GameObject found in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (found == null || !found.name.StartsWith(testName))
                continue;

            cachedBody = found;
            break;
        }

        return cachedBody;
    }

    // Move the tray that is already there, without rebuilding anything else.
    //
    // The panel used to say "change a number, then click a clip again", and
    // that was the wrong bargain. Re-clicking a clip destroys the model, writes
    // a controller asset to disk, re-instantiates, re-frames the Scene view and
    // restarts the animation -- all to answer "is it a centimetre left". This
    // does the one thing that was actually being asked for, which is a
    // transform write, and costs nothing
    internal static string Retray(TraySetup setup)
    {
        GameObject body = TestBody();

        if (body == null)
            return "sahnede test objesi yok -- once bir klibe tikla";

        if (!setup.on)
        {
            GameObject already = FindTray(body);

            if (already != null)
                Undo.DestroyObjectImmediate(already);

            return "plateau kapali";
        }

        Animator animator = body.GetComponent<Animator>();

        if (animator == null)
            return "test objesinde Animator yok";

        string where = HandTray(animator, body, setup);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return where;
    }

    // Hand the socket to the Scene view's own move and rotate tools.
    //
    // Typing numbers at a thing you can see is the wrong way round, and it was
    // only ever the way round because there was nothing selectable to grab
    internal static bool SelectSocket(out string trouble)
    {
        trouble = "";

        GameObject body = TestBody();

        if (body == null)
        {
            trouble = "Sahnede test objesi yok -- once bir klibe tikla.";
            return false;
        }

        Transform socket = Child(body, socketName);

        if (socket == null)
        {
            trouble = "Soket yok. Plateau'yu ac, sonra tekrar dene.";
            return false;
        }

        Selection.activeGameObject = socket.gameObject;

        return true;
    }

    // Put the tray back if it is gone, and touch nothing if it is not.
    //
    // The difference from Retray matters more than it looks. Retray writes the
    // panel's numbers onto the socket, which is right when a number just
    // changed and wrong every other time -- a socket that was dragged in the
    // Scene view is newer than the panel, and overwriting it throws the
    // dragging away. So the button that only means "make sure it is there"
    // gets a call that only does that
    internal static string Restore(TraySetup setup)
    {
        GameObject body = TestBody();

        if (body == null)
            return "sahnede test objesi yok -- once bir klibe tikla";

        if (!setup.on)
            return "plateau kapali";

        if (FindTray(body) != null && Child(body, socketName) != null)
            return "yerinde";

        return Retray(setup);
    }

    // Has the socket been dragged away from what the panel thinks it says?
    //
    // Asked every repaint while the panel is open, so the fields can be pulled
    // back into line before anything writes through them. Walking a couple of
    // hundred bones per repaint is nothing next to what it prevents
    internal static bool Moved(TraySetup setup)
    {
        GameObject body = TestBody();

        if (body == null)
            return false;

        Transform socket = Child(body, socketName);

        if (socket == null)
            return false;

        return Vector3.Distance(socket.localPosition, setup.place) > .0005f ||
               Quaternion.Angle(socket.localRotation, Quaternion.Euler(setup.turn)) > .5f;
    }

    // The other half of dragging it: what the hand put there, back into the
    // numbers, so it can be copied out and written into the real setup
    internal static bool ReadBack(ref TraySetup setup, out string trouble)
    {
        trouble = "";

        GameObject body = TestBody();

        if (body == null)
        {
            trouble = "Sahnede test objesi yok.";
            return false;
        }

        Transform socket = Child(body, socketName);

        if (socket == null)
        {
            trouble = "Soket yok -- okunacak bir sey bulunamadi.";
            return false;
        }

        setup.place = socket.localPosition;
        setup.turn = socket.localEulerAngles;

        // Read off the tray rather than unpicking the scale chain backwards.
        // Width is the number the panel actually holds, and the tray sitting
        // there is already the answer to it
        GameObject tray = FindTray(body);

        if (tray != null)
            setup.size = Width(tray);

        // Which hand it ended up on, in case it was re-parented by dragging it
        // in the hierarchy rather than by using the toggle
        Animator animator = body.GetComponent<Animator>();

        if (animator != null)
        {
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (socket.parent == right)
                setup.right = true;
            else if (socket.parent == left)
                setup.right = false;
        }

        return true;
    }

    // How wide the thing looks, across every renderer it has
    private static float Width(GameObject what)
    {
        Bounds all = default;
        bool any = false;

        foreach (Renderer renderer in what.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!any)
            {
                all = renderer.bounds;
                any = true;
                continue;
            }

            all.Encapsulate(renderer.bounds);
        }

        return any ? Mathf.Max(all.size.x, all.size.z) : 0f;
    }

    private static float Divide(float world, float parent)
    {
        return Mathf.Abs(parent) < .0001f ? 1f : world / parent;
    }

    // The plateau, out of the old hand and onto the root.
    //
    // Read off CustomerAnimator's own field, which is the only thing in the
    // project that actually knows which object this is
    private static string Rescue(GameObject root)
    {
        foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null)
                continue;

            SerializedProperty field = new SerializedObject(script).FindProperty("plateau");

            if (field == null || field.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            if (field.objectReferenceValue is not GameObject plateau)
                continue;

            // Kept where it was in the world, not where it was in the hand.
            // worldPositionStays: true, so it does not jump across the room on
            // the way out and make the next person wonder what moved it
            plateau.transform.SetParent(root.transform, true);

            return "  (plateau koke tasindi)";
        }

        return "";
    }

    // The scripts hold the Animator by reference and that reference now points
    // at a body that is switched off. Written by SerializedObject rather than
    // through a public setter, because the field is private and should stay
    // that way -- this is editor surgery, not an API the game needs
    private static string Rewire(GameObject root, Animator fresh, Animator old)
    {
        string done = "";

        foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null)
                continue;

            SerializedObject so = new SerializedObject(script);
            SerializedProperty field = so.FindProperty("animator");

            if (field == null || field.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            // Matched on the FIELD's declared type, not on what happens to be
            // in it. CustomerAnimator holds an Animator; Customer and Worker
            // hold a CustomerAnimator, and "PPtr<$CustomerAnimator>" ends in
            // the same six characters as the one being looked for -- which is
            // exactly the sort of near miss that assigns the wrong thing and
            // reports success
            if (field.type != "PPtr<$Animator>")
                continue;

            // Only the one that was pointing at the body just retired. A field
            // deliberately left pointing somewhere else is left alone
            if (field.objectReferenceValue != null &&
                field.objectReferenceValue != old)
                continue;

            field.objectReferenceValue = fresh;
            so.ApplyModifiedProperties();

            done += ", " + script.GetType().Name + " baglandi";
        }

        return done;
    }

    // ---- bits ----------------------------------------------------------------

    private static string Model(string animal)
    {
        return animalFolder + "DGN_" + animal + "_Outline.fbx";
    }

    private static Avatar LoadAvatar()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(avatarSource))
        {
            if (asset is Avatar avatar)
                return avatar;
        }

        return null;
    }

    // Clips live inside the FBX as sub assets, and what they are CALLED in
    // there is not what the file is called -- the Hypercasual ones carry their
    // whole Mixamo path ("Armature.001|Armature|mixamo.com|Walk"), the waiter
    // ones carry something else again. So: match on the last segment when a
    // name is asked for, and take the only clip in the file when it is not
    private static AnimationClip FirstClip(string path, string wanted)
    {
        AnimationClip fallback = null;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is not AnimationClip clip)
                continue;

            // Unity's own scrubbing clip, present in every imported model
            if (clip.name.StartsWith("__preview__"))
                continue;

            if (wanted == null)
                return clip;

            string[] parts = clip.name.Split('|');

            if (parts[parts.Length - 1] == wanted)
                return clip;

            fallback ??= clip;
        }

        return wanted == null ? null : fallback;
    }

    private static void Show(string report)
    {
        Debug.Log("[Kapsul Karakter]\n" + report);
        EditorUtility.DisplayDialog("Kapsul Karakterler", report, "Tamam");
    }
}
#endif
