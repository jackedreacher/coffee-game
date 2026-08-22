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
    // there is nothing to come out wrong.
    //
    // Except that it does, and this is the sentence that cost the most time in
    // the whole exercise. Tried side by side in the clip browser, the pack's
    // own Test_Walking comes out BROKEN on its own animals and the retargeted
    // waiter and Hypercasual clips come out clean -- the exact opposite of what
    // the paragraph above predicts. The reasoning is sound and the premise is
    // false: these FBXs are imported animationType 3, so they are not playing
    // on their own skeleton at all. They are being pushed through muscle space
    // like everything else, from a rig that was never configured for it.
    //
    // So the controllers use the Hypercasual clips, which are the game's own
    // four and demonstrably survive the trip. These two are left here for the
    // browser and for 1b, because seeing the bad one next to the good one is
    // how this was found and is how it would be found again
    private const string ownWalk =
        "Assets/DGN_15_CapsuleAnimals/Animations/Character@Test_Walking.fbx";

    private const string ownIdle =
        "Assets/DGN_15_CapsuleAnimals/Animations/Character@Test_LookingAround.fbx";

    private const string outputFolder = "Assets/Tiny Coffee Shop/Animations/Capsule";

    private const string customerController = outputFolder + "/Capsule Customer.controller";
    private const string workerController = outputFolder + "/Capsule Worker.controller";
    // Read by HatPowerSetup, which needs to know which clip the Idle state
    // is carrying before it can arrange for a hat to replace it.
    internal const string PlayerControllerPath =
        outputFolder + "/Capsule Player.controller";

    private const string playerController = PlayerControllerPath;

    // Off, because the pack has it off.
    //
    // I turned this ON as "the answer to feet that come out crooked", with a
    // tidy story attached: retargeting matches muscle ANGLES rather than
    // positions, so on legs a third the length the foot lands somewhere the
    // human's never went, and foot IK solves it back onto the ground.
    //
    // The story is true about retargeting and wrong about the remedy. Foot IK
    // does not nudge a foot onto the floor, it re-solves the whole leg chain to
    // reach a goal -- and on a body whose legs are a stub either side of a
    // sphere, that solver has almost no room to work in and folds the legs to
    // get there. CharacterTest_AnimatorController, the pack's own, shipped and
    // working on these exact bodies, has m_IKOnFeet: 0 on both its states. It
    // was the ONLY setting that differed from mine.
    //
    // Kept as a named constant rather than deleted, because it is the first
    // thing to try again if the legs are ever wrong in a way that IK could
    // actually fix -- on a character with legs long enough to solve
    private const bool footIK = false;

    // Every state name PlayerAnimator can ask for, read off PlayerAnimator
    // rather than off the controller it happens to have -- the code is what
    // decides, and a state the code never plays is dead weight either way
    private static readonly string[] playerStates =
    {
        "Walk", "Idle", "WalkWithPlateau", "IdleWithPlateau", "Sit",
        "TurnLeft", "TurnRight",
        "Assembly_Start", "Assembly_Loop", "Assembly_End",
        "Pan_Start", "Pan_Loop", "Pan_End",

        // One clip each, so only a _Start. ActionRoutine's PlayOnce skips a
        // state the controller does not have, so these play their one motion
        // and blend straight back to the walk
        "Serve_Start", "PickUp_Start", "PickUpCooked_Start", "Drop_Start",
        "Greet_Start",

        // Only ever filled if a clip for it turns up. See GunslingerClip.
        "Shoot_Start",
    };

    // The state names CustomerAnimator plays by string. They are not a
    // convention, they are an interface -- animator.Play("Walk") fails silently
    // against a state called anything else, and a customer that stands still
    // while gliding across the floor is what that failure looks like
    private static readonly string[] states =
    {
        "Walk", "Idle", "WalkWithPlateau", "IdleWithPlateau", "Sit",
        "TurnLeft", "TurnRight",
        "React_ChefsKiss",
        "React_NoGesture",
        "Leave_Turn180",
    };

    // Customers can also be shot, so they can also die and run.
    //
    // Their own list rather than three more entries on the shared one: a worker
    // has no reason to own a death pose, and a state with no clip in it is a
    // thing that looks broken to whoever opens the controller next.
    private static readonly string[] customerStates = Grow(states,
        "Death", "Death_Idle", "Run");

    private static string[] Grow(string[] head, params string[] tail)
    {
        string[] all = new string[head.Length + tail.Length];

        head.CopyTo(all, 0);
        tail.CopyTo(all, head.Length);

        return all;
    }

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

        // Only here once command 6 has made it. Files() returns nothing for a
        // path that does not exist, so an un-run command costs an empty shelf
        // rather than an error
        new[] { "Panda (humanoid)", pandaHuman },

        // The cowboy hat's pack. One shelf rather than the fourteen folders it
        // ships as -- Files() walks subdirectories, and Crouch/, Cover/ and the
        // rest are worth seeing next to each other when the question is which
        // clip to hang a power off. All humanoid, so they retarget onto the
        // capsule animals like everything else here.
        new[] { "Wild West (silah)", westFolder },

        // The weapon models' OWN clips -- the hammer and cylinder moving, not a
        // character. Generic, so they will not retarget onto an animal and are
        // here to be looked at rather than used: this is the shelf that answers
        // "what does Revovler_Shooting actually animate".
        new[] { "Dead West (silahin kendisi)",
            "Assets/Dead West - Animated Western Weapons/Animations" },
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

    // Squirrel is the chef/player. Keeping this list explicit makes it
    // impossible for a future "pick from Animals" edit to quietly put a
    // second squirrel in the customer queue.
    private static readonly string[] customerAnimals =
    {
        "Bear", "Beaver", "Bull", "Cat", "Cow", "Deer", "Dog", "Fox",
        "Koala", "Mouse", "Panda", "Pig", "Rabbit", "Ram",
    };

    private const string customerSource =
        "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers/Customer_Rabbit_Bald.prefab";

    private const string randomCustomerFolder =
        "Assets/Tiny Coffee Shop/Prefabs/Characters/Customers/Capsule Random";

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
        test.iKOnFeet = footIK;

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
        // The bear on purpose: this command is about the one file the other
        // fourteen copy their mapping from, so it asks that file directly
        Avatar avatar = AvatarIn(avatarSource);

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

        // Customers use the same four Hypercasual motions the old rabbits used:
        // empty walk/idle and tray walk/idle all come from one FBX. Mixing the
        // waiter carry pose into this controller made the customer's grip and
        // waiting silhouette change when its plateau appeared.
        report.Append(Controller(customerController, "MUSTERI", customerStates, false,
            new Dictionary<string, string[]>
        {
            { "Walk", new[] { humanClips, "Walk" } },
            { "Idle", new[] { humanClips, "Idle" } },
            { "WalkWithPlateau", new[] { humanClips, "WalkWithPlateau" } },
            { "IdleWithPlateau", new[] { humanClips, "IdleWithPlateau" } },
            // Same empty-prop turn family as Leave_Turn180. The tray variants
            // lock both hands around an object while customers arrive with the
            // plateau deliberately hidden.
            { "TurnLeft", new[] { waiterFolder + "Waiter_Pitcher_Turn_Left90.fbx", null } },
            { "TurnRight", new[] { waiterFolder + "Waiter_Pitcher_Turn_Right90.fbx", null } },
            { "React_ChefsKiss", new[] { waiterFolder + "Waiter_Idle_ChefsKiss.fbx", null } },
            { "React_NoGesture", new[] { waiterFolder + "Waiter_Idle_TakeOrder_NoGesture.fbx", null } },
            { "Leave_Turn180", new[] { waiterFolder + "Waiter_Pitcher_Turn_180.fbx", null } },
            { "Sit", new[] { sitClip, null } },

            // Shot by the cowboy hat. Death drops them, Death_Idle keeps them
            // down, and Run is what everybody else does about it.
            { "Death", new[] { westFolder + "Death/Death.fbx", null } },
            { "Death_Idle", new[] { westFolder + "Death/Death_Idle.fbx", null } },
            { "Run", new[] { westFolder + "Run/Run.fbx", null } },
        }));

        report.AppendLine();

        // The worker. Carrying is where the waiter pack earns its place: a tray
        // is exactly what a plateau is, and Tray_Walk_Forward is a person
        // carrying one rather than a person walking with their arms held out.
        // The two empty handed states stay on the old clips for the reason above
        report.Append(Controller(workerController, "CALISAN", states, false,
            new Dictionary<string, string[]>
        {
            // Empty hands get the game's own clips, hands get the waiter's.
            //
            // The waiter pack has no empty handed walk or stand -- every one of
            // its 65 clips is holding a tray or a pitcher. Putting Tray_Idle on
            // a character with nothing in its hands is a character miming a
            // tray that is not there, arms up around an empty space
            { "Walk", new[] { humanClips, "Walk" } },
            { "Idle", new[] { waiterFolder + "Waiter_Pitcher_Idle.fbx", null } },
            { "WalkWithPlateau", new[] { waiterFolder + "Waiter_Tray_Walk_Forward.fbx", null } },
            { "IdleWithPlateau", new[] { waiterFolder + "Waiter_Tray_Idle.fbx", null } },
            { "TurnLeft", new[] { waiterFolder + "Waiter_Tray_Turn_Left90.fbx", null } },
            { "TurnRight", new[] { waiterFolder + "Waiter_Tray_Turn_Right90.fbx", null } },
            { "Sit", new[] { sitClip, null } },
        }));

        report.AppendLine();

        // The player, which is a bigger job than either of the other two.
        //
        // PlayerAnimator does not just walk and stand: it plays Assembly_* and
        // Pan_* by name for the work at the counter and the hob, and it checks
        // HasState before each one -- so a missing state is not an error, it is
        // an animation that never happens and never says why.
        //
        // Those six cannot be borrowed from where the player has them now.
        // Panda.fbx is GENERIC, and a generic clip has no muscle space to
        // retarget through; it can only ever drive the skeleton it was authored
        // on. So the work states come from the waiter pack, which is humanoid,
        // and the mapping below is a first guess by someone who has not seen it
        // move. Audition replacements in the clip browser and say which
        report.Append(Controller(playerController, "PLAYER", playerStates, true,
            new Dictionary<string, string[]>
        {
            // Empty hands get the game's own clips, hands get the waiter's.
            //
            // The waiter pack has no empty handed walk or stand -- every one of
            // its 65 clips is holding a tray or a pitcher. Putting Tray_Idle on
            // a character with nothing in its hands is a character miming a
            // tray that is not there, arms up around an empty space
            { "Walk", new[] { humanClips, "Walk" } },
            { "Idle", new[] { waiterFolder + "Waiter_Pitcher_Idle.fbx", null } },
            { "WalkWithPlateau", new[] { waiterFolder + "Waiter_Tray_Walk_Forward.fbx", null } },
            { "IdleWithPlateau", new[] { waiterFolder + "Waiter_Tray_Idle.fbx", null } },
            { "TurnLeft", new[] { waiterFolder + "Waiter_Tray_Turn_Left90.fbx", null } },
            { "TurnRight", new[] { waiterFolder + "Waiter_Tray_Turn_Right90.fbx", null } },
            { "Sit", new[] { sitClip, null } },

            // Assembly is plating up: reach to the counter, work, withdraw
            { "Assembly_Start", new[] { waiterFolder + "Waiter_Tray_BarTop_Plate_PickUp.fbx", null } },
            { "Assembly_Loop", new[] { waiterFolder + "Waiter_Idle_TakeOrder_WriteDown.fbx", null } },
            { "Assembly_End", new[] { waiterFolder + "Waiter_Tray_BarTop_Plate_DropOff.fbx", null } },

            // Pan_Loop wants a shaking cycle rather than a reach, and the
            // pepper grinder is the only thing in the pack that shakes
            { "Pan_Start", new[] { waiterFolder + "Waiter_Idle_WipeTable_Start.fbx", null } },
            { "Pan_Loop", new[] { waiterFolder + "Waiter_Idle_PepperGrinder.fbx", null } },
            { "Pan_End", new[] { waiterFolder + "Waiter_Idle_WipeTable_End.fbx", null } },

            // Handing a plate over and putting one down are the same motion,
            // so they are the same clip -- the difference is who is standing
            // in front of the character, and that is not the animation's job
            { "Serve_Start", new[] { waiterFolder + "Waiter_Tray_BarTop_DropOff.fbx", null } },
            { "Drop_Start", new[] { waiterFolder + "Waiter_Tray_BarTop_DropOff.fbx", null } },
            // Two kinds of fetching. Lifting a plate off a counter is the
            // ordinary one; the kiss is what you do over something you cooked,
            // so it belongs to the hob and the fryer and nowhere else. Played
            // on every pickup it stops meaning anything
            { "PickUp_Start", new[] { waiterFolder + "Waiter_Tray_BarTop_PickUp.fbx", null } },
            { "PickUpCooked_Start", new[] { waiterFolder + "Waiter_Idle_ChefsKiss.fbx", null } },
            { "Greet_Start", new[] { waiterFolder + "Waiter_Idle_Greeting_Bow.fbx", null } },

            // Whatever this project has that looks most like an arm coming up.
            { "Shoot_Start", new[] { GunslingerClip(), null } },
        }));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine("Hepsinde moveSpeed float parametresi var ve");
        report.AppendLine("Apply Root Motion kapali olmali -- konumu NavMeshAgent");
        report.AppendLine("suruyor, klip de surerse ikisi kavga eder.");
        report.AppendLine();
        report.AppendLine("Shoot_Start: kovboy sapkasinin atisi. Yukarida hangi");
        report.AppendLine("klibin dustugu yaziyor -- gunslinger paketi kurulursa");
        report.AppendLine("bu komutu tekrar calistir, kendi bulur. Klip yoksa");
        report.AppendLine("atis yine calisir, karakter sadece kolunu kaldirmaz.");
        report.AppendLine();
        report.AppendLine("Controllerlar YERINDE guncellendi.");
        report.AppendLine("Sahnedeki sincabin govdesine ve plateau ayarina dokunulmadi.");
        report.AppendLine("4 veya 5 komutunu tekrar calistirma -- gerek yok.");

        Show(report.ToString());
    }

    // One signature, and the state list comes in as an argument.
    //
    // The player needs eleven states where a customer needs five, and the
    // difference is not decoration -- PlayerAnimator asks for Assembly_Start,
    // Pan_Loop and the rest by name, and a state that is not there is an
    // animation that silently does not play
    private static string Controller(string path, string label, string[] order,
                                     bool actionSpeed, Dictionary<string, string[]> map)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(label + "  ->  " + Path.GetFileName(path));

        // Preserve the asset itself -- especially its GUID. Deleting and
        // recreating this file made every Animator that referenced it lose the
        // link, which then forced command 5 to rebuild the scene squirrel and
        // destroyed the hand-tuned tray placement as collateral damage.
        //
        // The controller is tool-owned, so its contents are refreshed below,
        // but the asset identity never changes.
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Parameters are cheap and owned by this generator. Resetting them in
        // place avoids duplicates while keeping the controller asset alive.
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
            controller.RemoveParameter(i);

        controller.AddParameter("moveSpeed", AnimatorControllerParameterType.Float);

        // PlayerAnimator checks for this one before setting it, because
        // SetFloat on a parameter a controller does not have logs a warning on
        // every tap. Present here means the speed slider in the inspector does
        // something; absent means it is quietly ignored
        if (actionSpeed)
            controller.AddParameter("actionSpeed", AnimatorControllerParameterType.Float);

        AnimatorControllerLayer[] layers = controller.layers;

        if (layers.Length <= 0 || layers[0].stateMachine == null)
        {
            AnimatorStateMachine created = new AnimatorStateMachine
            {
                name = "Base Layer",
            };

            AssetDatabase.AddObjectToAsset(created, controller);

            layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Base Layer",
                    defaultWeight = 1f,
                    stateMachine = created,
                },
            };
        }
        else if (layers.Length > 1)
        {
            // This generator has one layer. Drop only the references to stale
            // generated layers; the controller asset and its GUID stay put.
            layers = new[] { layers[0] };
        }

        controller.layers = layers;

        AnimatorStateMachine machine = layers[0].stateMachine;

        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            machine.RemoveAnyStateTransition(transition);

        foreach (AnimatorTransition transition in machine.entryTransitions)
            machine.RemoveEntryTransition(transition);

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            machine.RemoveStateMachine(child.stateMachine);

        HashSet<string> wantedStates = new HashSet<string>(order);
        Dictionary<string, AnimatorState> existingStates =
            new Dictionary<string, AnimatorState>();

        foreach (ChildAnimatorState child in machine.states)
        {
            if (!wantedStates.Contains(child.state.name) ||
                existingStates.ContainsKey(child.state.name))
            {
                machine.RemoveState(child.state);
                continue;
            }

            existingStates.Add(child.state.name, child.state);

            foreach (AnimatorStateTransition transition in child.state.transitions)
                child.state.RemoveTransition(transition);
        }

        // No transitions between them on purpose.
        //
        // CustomerAnimator does not use transitions -- it calls Play by name
        // every frame, which cuts straight to the state. Wiring conditions here
        // as well would mean two things steering one machine, and the one that
        // loses is whichever the reader was not thinking about
        foreach (string state in order)
        {
            if (!map.TryGetValue(state, out string[] source))
                continue;

            AnimationClip clip = FirstClip(source[0], source[1]);

            AnimatorState added = existingStates.TryGetValue(state, out AnimatorState found)
                ? found
                : machine.AddState(state);

            if (clip == null)
            {
                report.AppendLine("  " + state.PadRight(16) + "KLIP YOK: " + source[0] +
                                  (source[1] == null ? "" : "  (" + source[1] + ")"));
                continue;
            }

            added.motion = clip;

            added.iKOnFeet = footIK;

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

    // Builds a separate customer prefab for every capsule species except the
    // squirrel, then fills the CustomerManager array in the OPEN scene. The
    // seven hand-authored rabbit prefabs are used only as a template and are
    // never edited, so this cannot repeat the old two-bodies regression on the
    // user's originals.
    [MenuItem("Cooked Fast/Karakter/3d - Rastgele Hayvan Musterileri Hazirla", priority = 709)]
    public static void BuildRandomAnimalCustomers()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(customerSource) == null)
        {
            Show("Kaynak musteri prefabi yok:\n" + customerSource);
            return;
        }

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(customerController);

        if (controller == null)
        {
            Show("Once 2 - Animator Controllerlarini Uret komutunu calistir.");
            return;
        }

        CustomerManager manager = Object.FindFirstObjectByType<CustomerManager>(
            FindObjectsInactive.Include);

        if (manager == null)
        {
            Show("Acik sahnede CustomerManager yok. Kitchen sahnesini acip tekrar calistir.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Rastgele Hayvan Musteriler",
                "14 farkli musteri prefabi URETILECEK:\n\n" +
                "  Bear, Beaver, Bull, Cat, Cow, Deer, Dog, Fox,\n" +
                "  Koala, Mouse, Panda, Pig, Rabbit, Ram\n\n" +
                "SQUIRREL listeye konmayacak; o sef olarak kalacak.\n" +
                "Eski 7 tavsan prefabi degistirilmeyecek. Acik sahnedeki\n" +
                "CustomerManager bu 14 yeni prefaba baglanacak.\n\n" +
                "Devam edilsin mi?",
                "Hazirla", "Vazgec"))
            return;

        EnsureAssetFolder(randomCustomerFolder);

        Avatar avatar = LoadAvatar();
        PlateauAttach.Placement placement = PlateauAttach.KnownGoodCustomer;
        List<Customer> made = new List<Customer>();
        StringBuilder report = new StringBuilder();

        report.AppendLine("RASTGELE MUSTERILER (Squirrel HARIC)");
        report.AppendLine();

        foreach (string animal in customerAnimals)
        {
            string path = randomCustomerFolder + "/Customer_" + animal + ".prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null &&
                !AssetDatabase.CopyAsset(customerSource, path))
            {
                report.AppendLine("- " + animal + ": prefab kopyalanamadi");
                continue;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model(animal));

            if (model == null)
            {
                report.AppendLine("- " + animal + ": model yok");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                Fit(root, model, avatar, controller, animal, customerController, false);

                Plateau plateau = root.GetComponentInChildren<Plateau>(true);
                Transform visual = PlateauAttach.FindVisual(root.transform);

                // Fit normally preserves the body it replaces. That is right
                // when changing one humanoid for another and wrong here: the
                // old rabbit visual was authored at 0.5 while DGN animals use
                // one. Copying 0.5 made both the customer and everything hanging
                // from its hand half size. The small customer-only reduction is
                // deliberate and shared with CustomerSetup so regenerating the
                // random animals cannot silently make them large again.
                visual.localScale = Vector3.one * CustomerSetup.CapsuleVisualScale;

                Transform bone = plateau == null
                    ? null
                    : PlateauAttach.ResolveBone(visual, placement.bonePath);

                if (plateau != null && bone != null)
                    PlateauAttach.ApplyPlacement(plateau, bone, placement);

                PrefabUtility.SaveAsPrefabAsset(root, path);

                if (plateau == null)
                    report.AppendLine("- " + animal + ": olustu, UYARI plateau yok");
                else if (bone == null)
                    report.AppendLine("- " + animal + ": olustu, UYARI el kemigi yok");
                else
                    report.AppendLine("- " + animal + ": olustu, plateau " + bone.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Customer customer = asset == null ? null : asset.GetComponent<Customer>();

            if (customer != null)
                made.Add(customer);
        }

        SerializedObject managerSo = new SerializedObject(manager);
        SerializedProperty choices = managerSo.FindProperty("customerPrefabs");
        SerializedProperty fallback = managerSo.FindProperty("customerPrefab");

        choices.arraySize = made.Count;

        for (int i = 0; i < made.Count; i++)
            choices.GetArrayElementAtIndex(i).objectReferenceValue = made[i];

        if (made.Count > 0 && fallback.objectReferenceValue == null)
            fallback.objectReferenceValue = made[0];

        managerSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine("CustomerManager'a " + made.Count + " prefab baglandi.");
        report.AppendLine("Squirrel: LISTE DISI (sef/player).");
        report.AppendLine("Sahne KAYDEDILMEDI. Kontrol et, sonra Ctrl+S.");

        Show(report.ToString());
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent))
            EnsureAssetFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
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

    private const string playerAnimal = "Squirrel";
    private const string playerPrefab = "Assets/Tiny Coffee Shop/Prefabs/Characters/Player.prefab";

    // The player alone, on its own animal, with its own controller.
    //
    // Kept apart from command 3 because it can be run again and again without
    // stacking bodies up, which 3 cannot -- 3 assumes it is meeting a prefab
    // for the first time. This one meets whatever is there and ends in the
    // same place regardless
    [MenuItem("Cooked Fast/Karakter/4 - Player'a Sincap Koy", priority = 710)]
    public static void PlayerToAnimal()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(playerController) == null)
        {
            Show("Once 2. komutu calistir -- " + Path.GetFileName(playerController) + " yok.\n\n" +
                 "Player'in 11 state'i var, eski 5'lik controller yetmez.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Player'a " + playerAnimal,
                "Player'in govdesi " + playerAnimal + " olacak.\n\n" +
                "  eski govde  -> kapatilir, adina (ESKI) yazilir\n" +
                "  sapka       -> yeni kafa kemigine tasinir\n" +
                "  controller  -> " + Path.GetFileName(playerController) + "\n\n" +
                "Onceki kapsul govde varsa SILINIR -- onu bu komut\n" +
                "yapmisti ve tek tusla yeniden yapilir. Elle konmus\n" +
                "govdelere dokunulmuyor.\n\n" +
                "Devam edilsin mi?",
                "Koy", "Vazgec"))
            return;

        string report = PutAnimal(playerPrefab, playerAnimal, playerController);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Show("PLAYER -> " + playerAnimal + "\n\n" + report + "\n" +
             "Play'e bas. Yurume ve durma calisir; tezgah ve ocak\n" +
             "animasyonlari waiter paketinden tahminle secildi --\n" +
             "0 - Klip Tarayici'da baskasini dene, hangisi olsun soyle.");
    }

    // Indented, so the shape reads as well as the names -- which limb a bone
    // belongs to is a fact about where it sits, not about what it is called
    private static void Skeleton(Transform bone, int depth, StringBuilder into)
    {
        if (bone == null || depth > 8)
            return;

        into.AppendLine(new string(' ', depth * 2) + bone.name);

        foreach (Transform child in bone)
            Skeleton(child, depth + 1, into);
    }

    private const string pandaSource = "Assets/Tiny Coffee Shop/FBX-2/Panda.fbx";
    internal const string pandaHuman = outputFolder + "/Panda_Humanoid.fbx";

    // A humanoid COPY of Panda.fbx, so its clips can drive a capsule animal.
    //
    // Panda.fbx is imported Generic, and a generic clip has no muscle space to
    // travel through -- it can only ever play on the skeleton it was authored
    // on. That is why none of the panda's animations could come along, and it
    // is a real loss: that file has Run, Idle, Idle_Holding, Chop, Jump, Duck
    // and the rest, which is the set somebody actually made for THIS game.
    //
    // A copy rather than a re-import of the original, because the original is
    // still doing a job. Panda.controller points at it, the retired panda body
    // in Player.prefab points at that, and 3c's way back depends on both. Turn
    // the original humanoid and all three break at once; copy it and nothing
    // that works today stops working.
    //
    // ANSWER: it cannot be done, and the reason is the rig, not the names.
    //
    // Run once, the skeleton came back like this:
    //
    //   Torso -> Shoulder.L -> UpperArm.L          (and there it stops)
    //   Hips  -> UpperLeg.L -> LowerLeg.L -> _end
    //   Root  -> Foot.L                            (a SIBLING of the leg)
    //   Root  -> PoleTarget.L
    //
    // Two things kill it. There is no lower arm and no hand at all -- the arm
    // is two bones -- and Unity's humanoid requires UpperArm, LowerArm and Hand
    // on both sides as mandatory bones. And the feet are not in the leg chain:
    // Foot.L hangs off Root beside a PoleTarget, which is how an IK rig is
    // built, while humanoid needs Hips -> UpperLeg -> LowerLeg -> Foot as one
    // unbroken descent.
    //
    // Neither is fixable by mapping. A HumanDescription can rename what exists;
    // it cannot invent a hand or reparent a foot. isValid: True with isHuman:
    // False says exactly that -- the avatar built fine as a GENERIC one.
    //
    // Kept, with the finding written down, because "why not just make the panda
    // humanoid" is the obvious question and this is the answer to it. Getting
    // those clips onto a capsule animal needs the panda re-rigged in Blender,
    // which is not an import setting
    [MenuItem("Cooked Fast/Karakter/6 - Panda Kliplerini Humanoid Yap", priority = 713)]
    public static void HumanisePanda()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(pandaSource) == null)
        {
            Show("Kaynak yok: " + pandaSource);
            return;
        }

        Directory.CreateDirectory(outputFolder);

        AssetDatabase.DeleteAsset(pandaHuman);

        if (!AssetDatabase.CopyAsset(pandaSource, pandaHuman))
        {
            Show("Kopyalanamadi:\n" + pandaSource + "\n  ->  " + pandaHuman);
            return;
        }

        AssetDatabase.ImportAsset(pandaHuman, ImportAssetOptions.ForceUpdate);

        ModelImporter importer = AssetImporter.GetAtPath(pandaHuman) as ModelImporter;

        if (importer == null)
        {
            Show("Importer okunamadi: " + pandaHuman);
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        importer.SaveAndReimport();

        StringBuilder report = new StringBuilder();

        report.AppendLine("PANDA -> HUMANOID");
        report.AppendLine();
        report.AppendLine(pandaHuman);
        report.AppendLine();

        Avatar made = AvatarIn(pandaHuman);

        if (made == null || !made.isValid || !made.isHuman)
        {
            report.AppendLine("ESLESMEDI.");

            if (made != null)
            {
                report.AppendLine("  isValid: " + made.isValid +
                                  "   isHuman: " + made.isHuman);
            }

            report.AppendLine();

            // The names, because the names are the whole problem.
            //
            // Unity's auto-mapper reads bone names and gives up silently on
            // anything it does not recognise -- "Required human bone 'LeftFoot'
            // not found" means it never saw a name it could take for a foot,
            // not that the rig has no foot. Writing an explicit mapping needs
            // the real names, they are not in the .meta (skeleton: []) and the
            // fbx is binary, so the only place to get them is from Unity. This
            // is that: the command that failed prints what it was looking at
            report.AppendLine("ISKELET:");
            report.AppendLine();

            GameObject copied = AssetDatabase.LoadAssetAtPath<GameObject>(pandaHuman);

            if (copied == null)
                report.AppendLine("  (model yuklenemedi)");
            else
                Skeleton(copied.transform, 0, report);

            // Taken away again. A humanoid copy that is not humanoid is an
            // asset whose only job is to mislead the next person who finds it
            AssetDatabase.DeleteAsset(pandaHuman);

            report.AppendLine();
            report.AppendLine("Kopya silindi -- ise yaramiyor.");

            Show(report.ToString());
            return;
        }

        report.AppendLine("avatar   : " + made.name);
        report.AppendLine("isValid  : " + made.isValid);
        report.AppendLine("isHuman  : " + made.isHuman);
        report.AppendLine();

        int count = 0;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(pandaHuman))
        {
            if (asset is not AnimationClip clip || clip.name.StartsWith("__preview__"))
                continue;

            count++;
        }

        report.AppendLine(count + " klip humanoid oldu.");
        report.AppendLine();
        report.AppendLine("0 - Klip Tarayici'yi ac, Yenile'ye bas.");
        report.AppendLine("\"Panda (humanoid)\" rafinda cikacaklar.");
        report.AppendLine("Run ve Idle'i sincapta dene, hangisi olsun soyle.");

        Show(report.ToString());
    }

    // The one in the scene, which is a different object from the one in the
    // prefab and always was.
    //
    // Kitchen.unity is saved in BINARY, so none of this could be worked out by
    // reading the file -- every grep came back empty and read as "there is no
    // player in the scene", which was the wrong conclusion drawn confidently.
    // The hierarchy showed a Player carrying a Panda Visual and no Body, and
    // that settles it: the scene's player is hand built, not an instance, so
    // four commands' worth of edits to Player.prefab never reached it
    [MenuItem("Cooked Fast/Karakter/5 - Sahnedeki Player'a Sincap Koy", priority = 711)]
    public static void ScenePlayerToAnimal()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz -- degisiklik Play bitince kaybolur.");
            return;
        }

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(playerController);

        if (controller == null)
        {
            Show("Once 2. komutu calistir -- " + Path.GetFileName(playerController) + " yok.");
            return;
        }

        GameObject root = ScenePlayer();

        if (root == null)
        {
            Show("Sahnede PlayerAnimator tasiyan bir obje bulunamadi.\n\n" +
                 "Kitchen sahnesi acik mi?");
            return;
        }

        Transform currentVisual = PlateauAttach.FindVisual(root.transform);

        // Command 2 used to break the controller reference, which made this
        // command look necessary after every animation change. If the squirrel
        // is already here, rebuilding it is precisely the wrong operation: Fit
        // rescues the tray from the old hand before replacing the body. Repair
        // the two references in place and preserve every hand-tuned transform.
        if (IsModel(currentVisual, Model(playerAnimal)))
        {
            Undo.RegisterFullObjectHierarchyUndo(root, "Sincabi yerinde onar");

            Animator currentAnimator = currentVisual.GetComponent<Animator>();

            if (currentAnimator == null)
                currentAnimator = currentVisual.gameObject.AddComponent<Animator>();

            currentAnimator.avatar = LoadAvatar();
            currentAnimator.runtimeAnimatorController = controller;
            currentAnimator.applyRootMotion = false;

            string plateauReport = RepairPlayerPlateau(root, currentVisual);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Show("SAHNEDEKI SQUIRREL YERINDE ONARILDI\n\n" +
                 "Body SILINMEDI / yeniden uretilmedi.\n" +
                 "Controller baglandi.\n" + plateauReport + "\n\n" +
                 "Kontrol et; iyi ise Ctrl+S.");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model(playerAnimal));

        if (model == null)
        {
            Show("Model yok: " + Model(playerAnimal));
            return;
        }

        if (!EditorUtility.DisplayDialog("Sahnedeki Player'a " + playerAnimal,
                "Sahnedeki \"" + root.name + "\" objesinin govdesi " + playerAnimal + " olacak.\n\n" +
                "Panda Visual KAPATILIR, silinmez -- adina (ESKI) yazilir.\n" +
                "Geri almak icin Ctrl+Z yeter, kaydetmeden once.\n\n" +
                "Devam edilsin mi?",
                "Koy", "Vazgec"))
            return;

        // The whole hierarchy, in one undo step. Anything less and Ctrl+Z
        // leaves the character half swapped, which is worse than either end
        Undo.RegisterFullObjectHierarchyUndo(root, "Player'a " + playerAnimal);

        string said = Fit(root, model, LoadAvatar(), controller,
            playerAnimal, playerController, true);

        string plateauReportAfterFit = RepairPlayerPlateau(
            root, PlateauAttach.FindVisual(root.transform));

        Selection.activeGameObject = root;

        EditorSceneManager.MarkSceneDirty(root.scene);

        Show("SAHNEDEKI PLAYER -> " + playerAnimal + "\n\n" +
             "obje: " + root.name + "\n" + said + "\n" +
             plateauReportAfterFit + "\n" +
             "Sahne KAYDEDILMEDI. Bak, begenirsen Ctrl+S.\n" +
             "Begenmezsen Ctrl+Z.");
    }

    private static bool IsModel(Transform visual, string modelPath)
    {
        if (visual == null)
            return false;

        string instancePath =
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.gameObject);

        if (instancePath == modelPath)
            return true;

        // GetPrefabAssetPath can be empty for a nested model whose outer prefab
        // owns the nearest root. The rendered mesh still belongs to the FBX and
        // gives an unambiguous answer without relying on the object's name.
        foreach (SkinnedMeshRenderer skin in
                 visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh != null && AssetDatabase.GetAssetPath(skin.sharedMesh) == modelPath)
                return true;
        }

        return false;
    }

    // Only repairs a tray that command 5 stranded outside the live body. A tray
    // already under the squirrel rig is hand-tuned and is deliberately left
    // byte-for-byte alone.
    private static string RepairPlayerPlateau(GameObject root, Transform visual)
    {
        Plateau plateau = root.GetComponentInChildren<Plateau>(true);

        if (plateau == null)
            return "Plateau yok; dokunulmadi.";

        if (visual == null)
            return "Canli Body bulunamadi; plateau dokunulmadi.";

        if (plateau.transform.IsChildOf(visual))
            return "Plateau zaten " + plateau.transform.parent.name +
                   " altinda; ELLE AYARI KORUNDU.";

        PlateauAttach.Placement placement = PlateauAttach.KnownGoodCustomer;
        Transform bone = PlateauAttach.ResolveBone(visual, placement.bonePath);

        if (bone == null)
            return "Plateau kokte kaldi: kayitli el kemigi bulunamadi.";

        PlateauAttach.ApplyPlacement(plateau, bone, placement);

        return "Plateau " + bone.name + " altina geri baglandi (kayitli ayar).";
    }

    // Recovery command with deliberately narrow authority: it cannot create,
    // replace, disable or delete a body. It only reconnects the controller and
    // a tray that was stranded at the Player root by the old command 5.
    [MenuItem("Cooked Fast/Karakter/5c - Sincabi Yerinde Onar", priority = 712)]
    public static void RepairSceneSquirrelInPlace()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        GameObject root = ScenePlayer();
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(playerController);

        if (root == null || controller == null)
        {
            Show(root == null
                ? "Sahnede PlayerAnimator tasiyan obje yok."
                : "Capsule Player.controller yok; once 2 komutunu calistir.");
            return;
        }

        Transform visual = PlateauAttach.FindVisual(root.transform);

        if (!IsModel(visual, Model(playerAnimal)))
        {
            Show("Canli govde Squirrel degil. HICBIR SEY DEGISTIRILMEDI.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Sincabi yerinde onar");

        Animator animator = visual.GetComponent<Animator>();

        if (animator == null)
            animator = visual.gameObject.AddComponent<Animator>();

        animator.avatar = LoadAvatar();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        string plateauReport = RepairPlayerPlateau(root, visual);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Show("SINCABIN GOVDESINE DOKUNULMADI\n\n" +
             "Controller baglandi.\n" + plateauReport + "\n\n" +
             "Kontrol et; iyi ise Ctrl+S.");
    }

    // The hat off, in both places it exists.
    //
    // Carrying it across was the wrong call. A chef's hat was modelled for a
    // human head and sized against one, and no amount of arithmetic makes it
    // belong on a squirrel -- the proportional resize made it a black disc
    // wider than the character instead of a white one, which is a different
    // wrong answer to a question that should not have been asked.
    //
    // Deleted rather than disabled because that is what was asked for. It
    // costs nothing to undo: in the scene Ctrl+Z brings it back before the
    // save, and Chef_Hat.fbx itself is untouched either way
    [MenuItem("Cooked Fast/Karakter/5b - Kafa Takisini Sil", priority = 712)]
    public static void DropHeadgear()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("KAFA TAKISI");
        report.AppendLine();

        GameObject scene = ScenePlayer();

        if (scene == null)
        {
            report.AppendLine("- sahne: PlayerAnimator'lu obje yok");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(scene, "Kafa takisini sil");

            string said = Strip(scene, true);

            report.AppendLine("- sahne (" + scene.name + "):" +
                              (said.Length <= 0 ? " taki yok" : said));

            EditorSceneManager.MarkSceneDirty(scene.scene);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefab) == null)
        {
            report.AppendLine("- prefab: YOK");
        }
        else
        {
            GameObject root = PrefabUtility.LoadPrefabContents(playerPrefab);

            try
            {
                string said = Strip(root, false);

                if (said.Length > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, playerPrefab);

                report.AppendLine("- Player.prefab:" +
                                  (said.Length <= 0 ? " taki yok" : said));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine("Sahne kaydedilmedi. Ctrl+S ya da Ctrl+Z.");

        Show(report.ToString());
    }

    // Anything that is its own prefab instance hanging off the head bone --
    // which is exactly the set of things Hook put there, and nothing that
    // belongs to the animal's own model
    private static string Strip(GameObject root, bool inScene)
    {
        StringBuilder said = new StringBuilder();

        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || !animator.isHuman)
                continue;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            if (head == null)
                continue;

            List<GameObject> doomed = new List<GameObject>();

            foreach (Transform child in head)
            {
                if (child != null && PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                    doomed.Add(child.gameObject);
            }

            // Collected first, deleted after -- removing while walking the
            // children skips every other one
            foreach (GameObject what in doomed)
            {
                said.Append("\n    silindi: " + what.name);

                if (inScene)
                    Undo.DestroyObjectImmediate(what);
                else
                    Object.DestroyImmediate(what);
            }
        }

        return said.ToString();
    }

    // Found by the component rather than by the name, because "Player" is a
    // name anything can have and PlayerAnimator is the thing that actually
    // drives this character
    private static GameObject ScenePlayer()
    {
        foreach (MonoBehaviour script in Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (script != null && script.GetType().Name == "PlayerAnimator")
                return script.gameObject;
        }

        return null;
    }

    // Idempotent by construction: it reads the state it finds, throws away only
    // what it made itself, and writes the same answer whether it is the first
    // run or the fifth
    private static string PutAnimal(string prefabPath, string animal, string controllerPath)
    {
        string name = Path.GetFileNameWithoutExtension(prefabPath);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return "- " + name + ": PREFAB YOK\n";

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model(animal));

        if (model == null)
            return "- " + name + ": " + animal + " modeli yok\n";

        Avatar avatar = LoadAvatar();

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            string said = Fit(root, model, avatar, controller, animal, controllerPath, false);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            return said;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The same operation on a prefab and on a scene object.
    //
    // Split out because the scene's Player is NOT an instance of Player.prefab
    // -- it is its own hand built object, so every edit to the prefab sailed
    // past it and the panda stayed exactly where it was. Two commands, one
    // piece of code, because "put this animal on this character" does not
    // become a different job depending on where the character is kept.
    //
    // The only thing that differs is who is allowed to destroy: in a prefab
    // being written to disk a DestroyImmediate is final, and in a scene it has
    // to go through Undo or the person who tries Ctrl+Z gets nothing back
    private static string Fit(GameObject root, GameObject model, Avatar avatar,
                              AnimatorController controller, string animal,
                              string controllerPath, bool inScene)
    {
        {
            StringBuilder said = new StringBuilder();

            GameObject live = null;
            GameObject stale = null;

            foreach (GameObject body in Bodies(root, null))
            {
                if (body.name == "Body" || body.name.EndsWith(capsuleSuffix))
                {
                    stale = body;
                    continue;
                }

                if (live == null && body.activeSelf)
                    live = body;
            }

            // Where the new body goes is copied off whatever is standing there
            // now, rather than assumed to be the origin. A character that was
            // offset or scaled in its prefab stays offset and scaled
            GameObject reference = live != null ? live : stale;

            Transform host = root.transform;
            Vector3 place = Vector3.zero;
            Quaternion turn = Quaternion.identity;
            Vector3 size = Vector3.one;

            if (reference != null)
            {
                host = reference.transform.parent == null
                    ? root.transform
                    : reference.transform.parent;

                place = reference.transform.localPosition;
                turn = reference.transform.localRotation;
                size = reference.transform.localScale;
            }

            said.Append(Rescue(root));

            // Taken off every body BEFORE anything is destroyed.
            //
            // On the second run the hat is hanging off the head bone of the
            // capsule body from the first run -- which is the object about to
            // be deleted. Collecting afterwards would collect nothing, and the
            // hat would go in the bin with the animal wearing it. It has to
            // come off first, and off ALL the bodies, not just the live one
            List<float> shares = new List<float>();
            List<GameObject> loose = Unhook(root, shares);

            // The one thing here that gets destroyed, and only because this
            // tool is the only thing that has ever touched it: a disabled
            // capsule body from a previous run. Keeping them would mean a
            // prefab that grows a new dead animal every time somebody changes
            // their mind about which one
            if (stale != null)
            {
                said.AppendLine("    onceki kapsul govde silindi: " + stale.name);

                if (inScene)
                    Undo.DestroyObjectImmediate(stale);
                else
                    Object.DestroyImmediate(stale);
            }

            GameObject body2 = (GameObject)PrefabUtility.InstantiatePrefab(model, host);

            if (inScene)
                Undo.RegisterCreatedObjectUndo(body2, "Kapsul govde");

            body2.name = "Body";
            body2.transform.localPosition = place;
            body2.transform.localRotation = turn;
            body2.transform.localScale = size;

            Animator fresh = body2.GetComponent<Animator>();

            if (fresh == null)
                fresh = body2.AddComponent<Animator>();

            fresh.avatar = avatar;
            fresh.runtimeAnimatorController = controller;
            fresh.applyRootMotion = false;

            said.AppendLine("    govde: " + animal + ", controller: " +
                            Path.GetFileName(controllerPath));

            said.Append(Hook(fresh, body2, loose, shares));
            said.Append(Retire(root, body2));

            Animator was = live == null ? null : live.GetComponent<Animator>();

            string wired = Rewire(root, fresh, was);

            if (wired.Length > 0)
                said.AppendLine("   " + wired.Substring(2));

            return said.ToString();
        }
    }

    // Hats and caps, off whatever body they are hanging on, parked on the root.
    //
    // Chef_Hat is parented to a BONE of a body that is about to be switched off
    // or deleted, so it goes with it -- the player comes out bare headed and
    // nothing says why. Anything that is its own prefab instance sitting inside
    // a body is an attachment rather than part of that body, which is what
    // tells a hat apart from a shoulder.
    //
    // What gets remembered is not the hat's size but its SHARE of the body it
    // was on. An absolute size means nothing across this move: a chef's hat at
    // local scale 1.97 was 1.97 of a human head bone, and the same number on a
    // squirrel's head bone is the white blob that swallows the character. A
    // fraction of the body's width survives the change, because that is what
    // "a hat this big" actually means
    private static List<GameObject> Unhook(GameObject root, List<float> shares)
    {
        List<GameObject> loose = new List<GameObject>();

        foreach (GameObject body in Bodies(root, null))
        {
            float was = Width(body);

            foreach (Transform child in body.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || child.gameObject == body)
                    continue;

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                    continue;

                loose.Add(child.gameObject);
                shares.Add(was > .0001f ? Width(child.gameObject) / was : 0f);
            }
        }

        // Collected first, moved after. Reparenting inside the walk would leave
        // the rest of the list pointing at objects that have since moved house
        foreach (GameObject what in loose)
            what.transform.SetParent(root.transform, true);

        return loose;
    }

    // And back on, at the head of the body that is actually there now
    private static string Hook(Animator fresh, GameObject body,
                               List<GameObject> loose, List<float> shares)
    {
        if (loose.Count <= 0)
            return "";

        Transform head = fresh.GetBoneTransform(HumanBodyBones.Head);

        if (head == null)
            return "    (yeni govdede kafa kemigi yok, taki KOKTE birakildi)\n";

        float now = Width(body);

        StringBuilder said = new StringBuilder();

        for (int i = 0; i < loose.Count; i++)
        {
            GameObject what = loose[i];

            // At the bone's origin, same bargain as the plateau: the offset
            // that was dialled in was dialled in against a different bone at a
            // different scale, so carrying the numbers across carries nothing
            what.transform.SetParent(head, false);
            what.transform.localPosition = Vector3.zero;
            what.transform.localRotation = Quaternion.identity;

            float wanted = i < shares.Count ? shares[i] * now : 0f;
            float drawn = Width(what);

            if (wanted <= .0001f || drawn <= .0001f)
            {
                said.AppendLine("    " + what.name + " -> " + head.name +
                                "  (olcu okunamadi, ELLE bak)");
                continue;
            }

            float factor = wanted / drawn;
            Vector3 had = what.transform.localScale;

            what.transform.localScale = new Vector3(
                had.x * factor, had.y * factor, had.z * factor);

            // Carried across but switched OFF.
            //
            // Twice now the honest answer to "how big should this hat be on
            // this head" has been a thing that swallowed the character, and the
            // reason is not the arithmetic: a chef's hat was modelled for a
            // human skull and a capsule animal does not have one. Off by
            // default means nothing is lost and nothing is imposed -- the
            // checkbox turns it back on, 5b removes it for good
            what.SetActive(false);

            said.AppendLine("    " + what.name + " -> " + head.name +
                            "  KAPALI (genislik " + wanted.ToString("0.000") + ")");
        }

        return said.ToString();
    }

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

    // The bear's avatar, on all fifteen -- which is what the PACK does.
    //
    // I argued myself out of this once, on the theory that an Avatar carries a
    // skeleton as well as a mapping and that a squirrel is not a small bear. It
    // reads well and it was wrong: DGN_Squirrel_Outline Variant.prefab, shipped
    // and working, has m_Avatar pointing at DGN_Bear_Outline. All fifteen are
    // the same DGN_Armature with the same proportions and only the mesh
    // differs, so one avatar genuinely serves them all.
    //
    // Written down because the wrong version is the more persuasive one, and
    // the next person to look at this -- me included -- will reason their way
    // back to it unless the evidence is sitting here
    private static Avatar LoadAvatar()
    {
        return AvatarIn(avatarSource);
    }

    private static Avatar AvatarIn(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
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
    private const string westFolder =
        "Assets/YashMakesGames/Wild West Animation Pack/";

    // Standing, and firing. In order of preference.
    //
    // Named outright rather than searched for, because the search got it wrong
    // in the way searches do: "CrouchAimWRevolver" contains both "revolver" and
    // "aim", matches every rule a name filter can express, and is a man
    // squatting on the floor. The pack is on disk and its files have names --
    // there is nothing left to infer.
    //
    // Quickdraw first because it is the whole shot in one motion: hand to hip,
    // gun up, fire. The others are fallbacks in case a future version of the
    // pack drops it.
    private static readonly string[] shootClips =
    {
        westFolder + "Idle/Quickdraw.fbx",
        westFolder + "Idle/Idle_Fulldraw_Revolver.fbx",
        westFolder + "Idle/Fanning.fbx",
        westFolder + "Idle/Idle_w_Revolver.fbx",
    };

    // Anything that is not a character standing on both feet. A shot fired from
    // one of these poses is not the shot this game is asking for, however well
    // its name scores.
    private static readonly string[] notStanding =
    {
        "crouch", "cover", "vault", "dodge", "death", "prone", "slide",
        "sit", "walk", "run", "holster", "reload",
    };

    // The best available firing clip: the named ones first, then a search for
    // anything that turns up if the pack is ever moved or renamed, then a
    // stand-in so the state is never left empty.
    private static string GunslingerClip()
    {
        for (int i = 0; i < shootClips.Length; i++)
            if (Humanoid(shootClips[i]))
                return shootClips[i];

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            // Its Revovler_Shooting is a GENERIC clip that animates the gun's
            // own hammer and cylinder. Retargeting it onto an animal would be
            // driving a rabbit with a revolver's skeleton.
            if (path.Contains("Dead West"))
                continue;

            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            if (Any(file, notStanding))
                continue;

            bool western = file.Contains("gunslinger") || file.Contains("revolver") ||
                           file.Contains("pistol") || file.Contains("cowboy") ||
                           file.Contains("quickdraw");

            if (!western)
                continue;

            if (!file.Contains("shoot") && !file.Contains("fire") &&
                !file.Contains("aim") && !file.Contains("draw"))
                continue;

            if (!Humanoid(path))
                continue;

            return path;
        }

        // Not a draw, but the one clip in this project that holds an arm out in
        // front gripping something, which is most of what a shot looks like
        // from this camera.
        return waiterFolder + "Waiter_Pitcher_TableTop_Pour.fbx";
    }

    private static bool Any(string name, string[] words)
    {
        for (int i = 0; i < words.Length; i++)
            if (name.Contains(words[i]))
                return true;

        return false;
    }

    private static bool Humanoid(string path)
    {
        // Doubles as the existence check: GetAtPath answers null for a file
        // that is not there, which is the same "cannot use this" as a clip that
        // was imported Generic. A generic clip in a humanoid controller plays
        // nothing and says nothing about why.
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

        return importer != null &&
               importer.animationType == ModelImporterAnimationType.Human;
    }

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
