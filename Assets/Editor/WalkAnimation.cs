using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Swaps the player's walk clips, and finds the controller by asking the scene.
//
// The first version had the controller path as a constant, which was wrong in a
// way that cost two rounds: the player in this scene is a Panda on
// Panda.controller, not the Hypercasual character on Player Animator v2, so
// every swap landed on an asset the game never loads. Both the change and the
// report agreed with each other and neither had anything to do with the game.
//
// So nothing here is named in advance. The controller comes off the player's
// Animator, and the replacement clips come out of the same fbx the current
// clips came from -- which also means the rig always matches and there is no
// retargeting to get wrong
public static class WalkAnimation
{
    // state on the controller -> clip wanted, by the part after the '|'
    private static readonly string[,] fastWalk =
    {
        { "Walk", "Run" },
        { "WalkWithPlateau", "Run_Holding" },
    };

    [MenuItem("Cooked Fast/Animasyon: 1 - Yurumeyi Kosmaya Cevir", priority = 400)]
    public static void SwapWalk()
    {
        AnimatorController controller = FindPlayerController(out string problem);

        if (controller == null)
        {
            EditorUtility.DisplayDialog("Animasyon", problem, "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Controller: " + controller.name);
        report.AppendLine();

        bool touched = false;

        for (int i = 0; i < fastWalk.GetLength(0); i++)
        {
            string stateName = fastWalk[i, 0];
            string wanted = fastWalk[i, 1];

            AnimatorState state = FindState(controller, stateName);

            if (state == null)
            {
                report.AppendLine(stateName + ": boyle bir state yok");
                continue;
            }

            AnimationClip old = state.motion as AnimationClip;

            report.AppendLine(stateName);
            report.AppendLine("  eski: " + Describe(old));

            if (old == null)
            {
                report.AppendLine("  <-- klip yok, hangi fbx'ten alacagimi bilemiyorum");
                continue;
            }

            AnimationClip fresh = Sibling(old, wanted);

            if (fresh == null)
            {
                report.AppendLine("  <-- ayni fbx icinde \"" + wanted + "\" bulunamadi");
                continue;
            }

            if (state.motion == fresh)
            {
                report.AppendLine("  yeni: (zaten " + wanted + ")");
                continue;
            }

            Undo.RecordObject(state, "Swap walk clip");

            state.motion = fresh;

            EditorUtility.SetDirty(state);

            touched = true;

            report.AppendLine("  yeni: " + Describe(fresh) + "  DEGISTIRILDI");
            report.Append(StrideNote(old, fresh, state));
        }

        if (touched)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        Debug.Log("[Animasyon]\n" + report);
        EditorUtility.DisplayDialog("Yurume Animasyonu", report.ToString(), "Tamam");
    }

    private const string speedParameter = "moveSpeed";
    private const string actionSpeedParameter = "actionSpeed";

    // The three part work animations PlayerAnimator.PlayAction asks for, by the
    // exact state names it builds. Both sides read the same clip names off the
    // character fbx, so a name changed here has to be changed in the enum too
    private static readonly string[] actionClips =
    {
        "Assembly_Start", "Assembly_Loop", "Assembly_End",
        "Pan_Start", "Pan_Loop", "Pan_End",
    };

    [MenuItem("Cooked Fast/Animasyon: 4 - Is Animasyonlarini Ekle", priority = 403)]
    public static void AddActionStates()
    {
        AnimatorController controller = FindPlayerController(out string problem);

        if (controller == null)
        {
            EditorUtility.DisplayDialog("Animasyon", problem, "Tamam");
            return;
        }

        // Any existing clip identifies the character's fbx, which is where the
        // work clips have to come from. Taking them from anywhere else is the
        // retargeting mistake this file already made once
        AnimationClip reference = AnyClip(controller);

        if (reference == null)
        {
            EditorUtility.DisplayDialog("Animasyon",
                "Controller'da hicbir klip yok, karakterin fbx'ini bulamiyorum.",
                "Tamam");
            return;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        StringBuilder report = new StringBuilder();

        report.AppendLine("Controller: " + controller.name);
        report.AppendLine("Kaynak    : " + AssetDatabase.GetAssetPath(reference));
        report.AppendLine();

        if (!HasFloat(controller, actionSpeedParameter))
        {
            controller.AddParameter(actionSpeedParameter, AnimatorControllerParameterType.Float);

            report.AppendLine("\"" + actionSpeedParameter + "\" float parametresi EKLENDI");
            report.AppendLine();
        }

        int added = 0;

        for (int i = 0; i < actionClips.Length; i++)
        {
            string name = actionClips[i];

            AnimationClip clip = Sibling(reference, name);

            if (clip == null)
            {
                report.AppendLine(name + ": KLIP YOK -- bu karakterde bulunmuyor");
                continue;
            }

            AnimatorState state = FindState(controller, name);

            if (state == null)
            {
                // Explicit positions. States stacked on one another make the
                // Animator window throw while drawing its graph
                state = machine.AddState(name, new Vector3(620f, 60f + i * 60f, 0f));

                added++;
            }

            Undo.RecordObject(state, "Add action state");

            state.motion = clip;

            // Its own parameter, not moveSpeed. These are hand motions at a
            // counter, not strides across the floor -- tying them to how fast
            // the player walks would make the character work faster after a
            // speed upgrade. A separate one keeps them tunable without that
            state.speed = 1f;
            state.speedParameterActive = true;
            state.speedParameter = actionSpeedParameter;

            EditorUtility.SetDirty(state);

            report.AppendLine(name + " -> " + Describe(clip) +
                              (clip.isLooping == name.EndsWith("_Loop")
                                  ? ""
                                  : name.EndsWith("_Loop")
                                      ? "   <-- LOOP KAPALI, acilmali"
                                      : "   <-- LOOP ACIK, kapatilmali"));
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        report.AppendLine();
        report.AppendLine(added + " yeni state eklendi, geri kalani guncellendi.");
        report.AppendLine();
        report.AppendLine("Tetikleme yerleri:");
        report.AppendLine("  ocak (CookingStation)  -> Pan");
        report.AppendLine("  diger butun istasyonlar -> Assembly");
        report.AppendLine("  musteriye servis        -> Assembly");
        report.AppendLine();
        report.AppendLine("Oynatma hizi : Player > Player Animator > Action Speed");
        report.AppendLine("Ortadaki Loop suresi: Player > Player Animator > Action Hold Time");
        report.AppendLine("Yurumeye baslayinca is animasyonu iptal olur.");

        Debug.Log("[Animasyon]\n" + report);
        EditorUtility.DisplayDialog("Is Animasyonlari", report.ToString(), "Tamam");
    }

    private static AnimationClip AnyClip(AnimatorController controller)
    {
        AnimationClip found = null;

        foreach (AnimatorControllerLayer layer in controller.layers)
            FindAnyClip(layer.stateMachine, ref found);

        return found;
    }

    private static void FindAnyClip(AnimatorStateMachine machine, ref AnimationClip found)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (found == null && child.state != null && child.state.motion is AnimationClip clip)
                found = clip;
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            FindAnyClip(child.stateMachine, ref found);
    }

    // Makes the leg tempo follow how fast the character is actually travelling.
    //
    // Without this a state plays at a fixed rate, so every change to movement
    // speed has to be paid for a second time by hand in the Animator -- and the
    // two drift apart silently, which is what skating across the kitchen is.
    // PlayerAnimator already writes the parameter every frame; nothing was
    // reading it
    [MenuItem("Cooked Fast/Animasyon: 3 - Bacak Temposunu Hiza Bagla", priority = 402)]
    public static void BindSpeedParameter()
    {
        AnimatorController controller = FindPlayerController(out string problem);

        if (controller == null)
        {
            EditorUtility.DisplayDialog("Animasyon", problem, "Tamam");
            return;
        }

        StringBuilder report = new StringBuilder();

        report.AppendLine("Controller: " + controller.name);
        report.AppendLine();

        if (!HasFloat(controller, speedParameter))
        {
            controller.AddParameter(speedParameter, AnimatorControllerParameterType.Float);

            report.AppendLine("\"" + speedParameter + "\" float parametresi EKLENDI");
            report.AppendLine();
        }

        for (int i = 0; i < fastWalk.GetLength(0); i++)
        {
            string stateName = fastWalk[i, 0];

            AnimatorState state = FindState(controller, stateName);

            if (state == null)
            {
                report.AppendLine(stateName + ": boyle bir state yok");
                continue;
            }

            Undo.RecordObject(state, "Bind speed parameter");

            report.AppendLine(stateName);
            report.AppendLine("  once : Speed " + state.speed.ToString("0.00") +
                              (state.speedParameterActive
                                  ? " x " + state.speedParameter
                                  : "  (parametre kapali)"));

            // Back to 1 on purpose. Any hand tuning that lives here would
            // multiply with the parameter and the result would be neither
            state.speed = 1f;
            state.speedParameterActive = true;
            state.speedParameter = speedParameter;

            EditorUtility.SetDirty(state);

            report.AppendLine("  sonra: Speed 1.00 x " + speedParameter);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        report.AppendLine();
        report.AppendLine("Tempo artik hiza gore hesaplaniyor:");
        report.AppendLine("  Player > Player Animator > Walk Speed Reference");
        report.AppendLine("  Karakter O hizda giderken klip normal hizinda oynar.");
        report.AppendLine("  Su an 20, oyuncunun hizi 40 -> bacaklar 2 kat tempoda.");
        report.AppendLine();
        report.AppendLine("Ayaklar ONE kayiyorsa Reference'i DUSUR (bacaklar hizlanir).");
        report.AppendLine("Ayaklar GERI kayiyorsa Reference'i YUKSELT.");

        Debug.Log("[Animasyon]\n" + report);
        EditorUtility.DisplayDialog("Bacak Temposu", report.ToString(), "Tamam");
    }

    private static bool HasFloat(AnimatorController controller, string name)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Float)
                return true;
        }

        return false;
    }

    // Everything the game will actually use, read off the scene rather than
    // assumed -- including the full clip list of whatever character is in there,
    // because what the project owns turned out to be the thing nobody knew
    [MenuItem("Cooked Fast/Animasyon: 2 - Durumu Soyle", priority = 401)]
    public static void Explain()
    {
        StringBuilder report = new StringBuilder();

        PlayerAnimator player = Object.FindFirstObjectByType<PlayerAnimator>(
            FindObjectsInactive.Include);

        if (player == null)
        {
            EditorUtility.DisplayDialog("Animasyon",
                "Sahnede PlayerAnimator yok.", "Tamam");
            return;
        }

        Animator animator = TargetAnimator(player);

        report.AppendLine("Oyuncu: " + player.name);

        if (animator == null)
        {
            EditorUtility.DisplayDialog("Animasyon",
                "Oyuncuda Animator yok.", "Tamam");
            return;
        }

        report.Append(ListAnimators(player, animator));

        report.AppendLine("  Animator  : " + animator.name);
        report.AppendLine("  Controller: " + (animator.runtimeAnimatorController == null
            ? "YOK"
            : animator.runtimeAnimatorController.name));

        report.AppendLine("  Avatar    : " + (animator.avatar == null
            ? "YOK"
            : animator.avatar.name + (animator.avatar.isHuman
                ? "  HUMANOID -- disaridan humanoid klip retarget olur"
                : "  GENERIC -- baska rigden klip ALINMAZ, sadece kendi fbx'i")));

        report.AppendLine("  Root Motion: " + (animator.applyRootMotion
            ? "ACIK <-- kapat, NavMeshAgent ile cakisir"
            : "kapali (dogru)"));

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;

        report.AppendLine();

        if (controller == null)
        {
            report.AppendLine("Controller bir AnimatorController degil " +
                              "(Override Controller olabilir), state'ler okunamadi.");
        }
        else
        {
            report.AppendLine("State'ler");

            AnimationClip any = null;

            foreach (AnimatorControllerLayer layer in controller.layers)
                AppendStates(layer.stateMachine, report, ref any);

            report.AppendLine();
            report.Append(AppendLibrary(any));
        }

        Debug.Log("[Animasyon]\n" + report);
        EditorUtility.DisplayDialog("Animasyon Durumu", report.ToString(), "Tamam");
    }

    // What the character can do but the controller never asks for. This is the
    // list that matters when deciding what to build next, and until now the only
    // way to see it was clicking the fbx and scrolling
    private static string AppendLibrary(AnimationClip reference)
    {
        if (reference == null)
            return "Klip kutuphanesi okunamadi (hicbir state'te klip yok).\n";

        string path = AssetDatabase.GetAssetPath(reference);

        StringBuilder list = new StringBuilder();

        list.AppendLine("Karakterin fbx'indeki butun klipler (" + path + ")");

        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        {
            AnimationClip clip = asset as AnimationClip;

            if (clip == null || clip.name.StartsWith("__preview__"))
                continue;

            list.AppendLine("  " + Short(clip.name) + "  (" +
                            clip.length.ToString("0.00") + " sn)");
        }

        return list.ToString();
    }

    private static void AppendStates(AnimatorStateMachine machine, StringBuilder report,
        ref AnimationClip any)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state == null)
                continue;

            AnimationClip clip = child.state.motion as AnimationClip;

            if (clip != null && any == null)
                any = clip;

            report.AppendLine("  " + child.state.name + " -> " + Describe(clip) +
                              "  [Speed " + child.state.speed.ToString("0.00") +
                              (child.state.speedParameterActive
                                  ? " x " + child.state.speedParameter
                                  : " sabit") + "]");
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            AppendStates(child.stateMachine, report, ref any);
    }

    // The Animator the GAME talks to, which is the one in PlayerAnimator's own
    // field -- not whichever one a component search happens to reach first.
    //
    // Those are different objects here. Swapping the player's character leaves
    // the previous body under the player switched off rather than deleting it,
    // so there are two Animators in the hierarchy and the disabled one comes
    // first. Searching found that one, reported its controller, and edited it,
    // while the game ran the other. Everything agreed and nothing was true
    private static Animator TargetAnimator(PlayerAnimator player)
    {
        Animator field = new SerializedObject(player)
            .FindProperty("animator").objectReferenceValue as Animator;

        return field != null ? field : player.GetComponentInChildren<Animator>(true);
    }

    // Printed whenever there is more than one, because a second body under the
    // player is invisible in play mode and explains most of what looks impossible
    private static string ListAnimators(PlayerAnimator player, Animator target)
    {
        Animator[] all = player.GetComponentsInChildren<Animator>(true);

        if (all.Length <= 1)
            return "";

        StringBuilder list = new StringBuilder();

        list.AppendLine("  Oyuncunun altinda " + all.Length + " Animator var:");

        foreach (Animator animator in all)
        {
            list.AppendLine("    " + (animator == target ? "-> " : "   ") + animator.name +
                            (animator.gameObject.activeInHierarchy ? " (acik)" : " (KAPALI)") +
                            "  " + (animator.runtimeAnimatorController == null
                                ? "controller yok"
                                : animator.runtimeAnimatorController.name) +
                            (animator == target ? "   <-- oyunun kullandigi" : ""));
        }

        return list.ToString();
    }

    private static AnimatorState FindState(AnimatorController controller, string wanted)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            AnimatorState found = FindState(layer.stateMachine, wanted);

            if (found != null)
                return found;
        }

        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string wanted)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null && child.state.name == wanted)
                return child.state;
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState found = FindState(child.stateMachine, wanted);

            if (found != null)
                return found;
        }

        return null;
    }

    private static AnimatorController FindPlayerController(out string problem)
    {
        problem = null;

        PlayerAnimator player = Object.FindFirstObjectByType<PlayerAnimator>(
            FindObjectsInactive.Include);

        if (player == null)
        {
            problem = "Sahnede PlayerAnimator yok.";
            return null;
        }

        Animator animator = TargetAnimator(player);

        if (animator == null)
        {
            problem = "Oyuncuda Animator yok.";
            return null;
        }

        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;

        if (controller == null)
            problem = "Oyuncunun controller'i okunamadi: " +
                      (animator.runtimeAnimatorController == null
                          ? "hic atanmamis"
                          : animator.runtimeAnimatorController.name +
                            " bir AnimatorController degil");

        return controller;
    }

    // A clip out of the same fbx as the one already in the state. Same file
    // means same rig, so this can never produce the retargeting problem that a
    // clip picked from anywhere else can
    private static AnimationClip Sibling(AnimationClip reference, string wanted)
    {
        string path = AssetDatabase.GetAssetPath(reference);

        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        {
            AnimationClip clip = asset as AnimationClip;

            if (clip == null || clip.name.StartsWith("__preview__"))
                continue;

            if (Short(clip.name) == wanted)
                return clip;
        }

        return null;
    }

    // "CharacterArmature|Run_Holding" -> "Run_Holding". These exporters put the
    // armature name in front of every clip and it is noise in every comparison
    private static string Short(string name)
    {
        int bar = name.LastIndexOf('|');

        return bar < 0 ? name : name.Substring(bar + 1);
    }

    private static string Describe(AnimationClip clip)
    {
        if (clip == null)
            return "klip yok";

        return Short(clip.name) + " (" + clip.length.ToString("0.00") + " sn" +
               (clip.isLooping ? ", loop" : ", LOOP KAPALI") + ")";
    }

    // A walk cycle that does not match how fast the character crosses the floor
    // reads as sliding, and the fix is a number in one of two places that nobody
    // remembers. Working it out here is cheaper than noticing it while playing
    private static string StrideNote(AnimationClip old, AnimationClip fresh, AnimatorState state)
    {
        if (old == null || fresh == null || fresh.length <= .001f)
            return "";

        float ratio = old.length / fresh.length;

        if (ratio < 1.05f && ratio > .95f)
            return "";

        return "  dongusu " + ratio.ToString("0.0") + " kat " +
               (ratio > 1f ? "hizli" : "yavas") + " -- ayaklar kayarsa " +
               state.name + " > Speed = " + (1f / ratio).ToString("0.00") + "\n";
    }
}
