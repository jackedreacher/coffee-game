#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Writes a real T-pose onto the capsule animals' rig, instead of letting Unity
// guess one.
//
// The audit said the bone mapping was fine and it was -- every leg bone and
// both sets of toes are found. The legs are still wrong in almost every clip,
// and the reason is the other half of a humanoid avatar, the half the audit
// could not see: the REFERENCE POSE.
//
// A humanoid avatar is a mapping plus a pose. Unity measures every animation as
// an angle away from that pose, so the pose is the zero. This rig has
// "human: []" and "skeleton: []" in its meta, which means nobody ever set one
// and Unity estimated it from the model's bind pose. These characters are
// modelled standing in a wide, splay-legged stance because that is how they are
// meant to look standing still -- so the estimate takes that stance as zero,
// and then every clip in the project, including the pack's own, is played as an
// offset from a stance that was never neutral. The splay is not added by any
// one animation. It is underneath all of them.
//
// Fixing it is one file. All fifteen animals copy this avatar and so do both of
// the pack's animations, so the pose written here is the pose they all measure
// against.
//
// This only edits IMPORT SETTINGS. The FBX is untouched, and 1f puts it back
public static class CapsuleRigFix
{
    private const string animalFolder =
        "Assets/DGN_15_CapsuleAnimals/Models/Characters/";

    private const string rig = animalFolder + "Outlined_Characters/DGN_Bear_Outline.fbx";

    private const string clipFolder = "Assets/DGN_15_CapsuleAnimals/Animations";

    // How far a limb may lengthen to reach where an animation puts it, instead
    // of swinging out to reach it.
    //
    // Unity ships this at 0.05, which is almost nothing, and on a body with legs
    // this short "almost nothing" is the whole problem: told to put a foot
    // somewhere a human's foot went, a leg that may not stretch has only one way
    // to get there, which is outwards. Letting it stretch a fifth of its length
    // buys back most of that swing, and a fifth is not visible on a leg the size
    // of a thumb
    private const float stretch = .2f;

    // Above this, a correction is treated as evidence against itself. See Aim
    private const float maxCorrection = 60f;

    // ---- the fix -------------------------------------------------------------

    [MenuItem("Cooked Fast/Karakter/1e - Rigi Duzelt (T-pose)", priority = 704)]
    public static void Fix()
    {
        if (EditorApplication.isPlaying)
        {
            Show("Play modundayken calismaz. Once durdur.");
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(rig);

        if (model == null)
        {
            Show("Rig bulunamadi:\n" + rig);
            return;
        }

        if (AssetImporter.GetAtPath(rig) is not ModelImporter importer)
        {
            Show("Import ayarlari okunamadi:\n" + rig);
            return;
        }

        StringBuilder report = new StringBuilder();

        // Read off the CURRENT avatar rather than off bone names.
        //
        // The auto mapping already found everything, so the names it chose are
        // the right answer -- what is being changed is that they get written
        // down instead of re-guessed on every import, and that a pose goes with
        // them. Asking the avatar is also the only way to be right about a rig
        // this code has never seen
        GameObject probe = Object.Instantiate(model);

        try
        {
            probe.name = model.name;

            Animator animator = probe.GetComponent<Animator>();

            if (animator == null)
                animator = probe.AddComponent<Animator>();

            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                Show("Mevcut avatar humanoid degil, once Rig > Humanoid yapilmali.");
                return;
            }

            Dictionary<HumanBodyBones, Transform> bones = Map(animator);

            report.Append(AddChest(bones));

            report.AppendLine();
            report.AppendLine("T-POSE");
            report.Append(Straighten(bones));

            HumanDescription description = importer.humanDescription;

            description.human = Human(bones);
            description.skeleton = Skeleton(probe.transform);

            description.armStretch = stretch;
            description.legStretch = stretch;

            // Left where it was. This one moves the feet sideways to stop them
            // passing through each other, and these feet do not -- they are too
            // far apart, which is the opposite complaint. Changing it would be
            // treating a symptom of the pose while the pose is being fixed
            description.feetSpacing = 0f;

            description.hasTranslationDoF = false;

            importer.humanDescription = description;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // The whole point. Left on, Unity throws away what was just written
            // and goes back to guessing on the next reimport
            importer.autoGenerateAvatarMappingIfUnspecified = false;

            importer.SaveAndReimport();

            report.AppendLine();
            report.AppendLine("  " + description.human.Length + " kemik yazildi");
            report.AppendLine("  " + description.skeleton.Length + " iskelet girdisi");
            report.AppendLine("  arm/leg stretch: " + stretch);
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }

        report.AppendLine();
        report.Append(Refresh());

        report.AppendLine();
        report.AppendLine("Simdi tekrar bak:");
        report.AppendLine("  Cooked Fast > Karakter > 0 - Klip Tarayici");
        report.AppendLine();
        report.AppendLine("Begenmezsen geri alinir:");
        report.AppendLine("  Cooked Fast > Karakter > 1f - Rigi Fabrika Ayarina Dondur");

        Show(report.ToString());
    }

    // ---- the pose ------------------------------------------------------------

    // Arms out, legs down, toes forward. Nothing else.
    //
    // A textbook Enforce T-Pose straightens the spine as well, and this one
    // deliberately does not. A hunched or leaning spine on these characters is
    // a drawing decision -- it is most of why they read as cute -- and pulling
    // it upright would change what the animal looks like standing still in
    // order to fix how it walks. The limbs are where the complaint is and the
    // limbs are what a T-pose is actually for
    private static string Straighten(Dictionary<HumanBodyBones, Transform> bones)
    {
        StringBuilder report = new StringBuilder();

        // Character faces +Z with +Y up, so its own right hand is +X. Getting
        // this backwards mirrors the arms, which is very obvious and not at all
        // subtle to spot
        report.Append(Aim(bones, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, Vector3.left));
        report.Append(Aim(bones, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Vector3.left));
        report.Append(Aim(bones, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, Vector3.right));
        report.Append(Aim(bones, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Vector3.right));

        // Top down, always. Rotating a thigh carries the shin and the foot with
        // it, so a shin straightened first is bent again by its own parent
        report.Append(Aim(bones, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, Vector3.down));
        report.Append(Aim(bones, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, Vector3.down));
        report.Append(Aim(bones, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, Vector3.down));
        report.Append(Aim(bones, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, Vector3.down));

        // The feet are deliberately left exactly as modelled.
        //
        // They used to be aimed too, on the assumption that the line from ankle
        // to toe points forward the way it does on a person. It came back
        // wanting EIGHTY DEGREES on both -- against seven to thirty seven
        // everywhere else -- and turned the feet into the shins.
        //
        // Eighty degrees is not a foot that is badly placed. It is a foot whose
        // toe bone does not point where I assumed, and on a stubby animal foot
        // it very likely points down. There is no reliable way to read "forward"
        // off this geometry, and a foot left in its authored orientation is
        // right far more often than one rotated by a guess
        return report.ToString();
    }

    // Rotate the bone so the line to its child points where it should.
    //
    // FromToRotation gives the shortest rotation between two directions, and
    // pre-multiplying it onto the world rotation turns the bone about its own
    // joint -- which is what a joint does. Doing this on local rotations
    // instead would need the parent's frame worked back out of it, and that is
    // the same arithmetic with more chances to get a sign wrong
    private static string Aim(Dictionary<HumanBodyBones, Transform> bones,
        HumanBodyBones from, HumanBodyBones to, Vector3 target)
    {
        if (!bones.TryGetValue(from, out Transform bone) || bone == null)
            return "  " + from + ": kemik yok, atlandi\n";

        if (!bones.TryGetValue(to, out Transform child) || child == null)
            return "  " + from + ": " + to + " yok, atlandi\n";

        Vector3 have = child.position - bone.position;

        if (have.sqrMagnitude < .0000001f)
            return "  " + from + ": iki kemik ust uste, atlandi\n";

        float was = Vector3.Angle(have, target);

        // A correction this large is a wrong assumption, not a wrong bone.
        //
        // Every limb here needed between seven and thirty seven degrees, which
        // is what a rest pose that is not quite a T-pose looks like. When a bone
        // comes back wanting eighty, the thing that is wrong is the belief about
        // which way that bone is supposed to point -- and acting on it moves the
        // part further from where it belongs than it started. Refused and
        // reported, rather than applied and wondered about later
        if (was > maxCorrection)
            return "  " + from.ToString().PadRight(15) + was.ToString("0") +
                   " derece ISTEDI -- fazla, DOKUNULMADI\n";

        bone.rotation = Quaternion.FromToRotation(have, target) * bone.rotation;

        return "  " + from.ToString().PadRight(15) + was.ToString("0") + " derece duzeltildi\n";
    }

    // ---- reading and writing the description ---------------------------------

    private static Dictionary<HumanBodyBones, Transform> Map(Animator animator)
    {
        Dictionary<HumanBodyBones, Transform> bones =
            new Dictionary<HumanBodyBones, Transform>();

        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            HumanBodyBones bone = (HumanBodyBones)i;

            Transform found = animator.GetBoneTransform(bone);

            if (found != null)
                bones[bone] = found;
        }

        return bones;
    }

    // The one bone the auto mapping missed.
    //
    // Optional in Unity's humanoid, so nothing refuses to work without it -- but
    // without a chest the whole upper body is one rigid segment from the hips
    // up, and every clip that bends a torso gets folded into the spine instead.
    // Found by walking up from the neck rather than by name, because this rig
    // calls nothing "Chest"
    private static string AddChest(Dictionary<HumanBodyBones, Transform> bones)
    {
        if (bones.ContainsKey(HumanBodyBones.Chest))
            return "Chest: zaten bagli\n";

        if (!bones.TryGetValue(HumanBodyBones.Spine, out Transform spine) ||
            !bones.TryGetValue(HumanBodyBones.Neck, out Transform neck))
            return "Chest: spine ya da neck yok, atlandi\n";

        Transform walk = neck.parent;
        Transform found = null;

        while (walk != null && walk != spine)
        {
            found = walk;
            walk = walk.parent;
        }

        if (walk != spine || found == null)
            return "Chest: spine ile neck arasinda kemik yok, bos birakildi\n";

        bones[HumanBodyBones.Chest] = found;

        return "Chest: " + found.name + " baglandi\n";
    }

    private static HumanBone[] Human(Dictionary<HumanBodyBones, Transform> bones)
    {
        List<HumanBone> human = new List<HumanBone>();

        foreach (KeyValuePair<HumanBodyBones, Transform> pair in bones)
        {
            if (pair.Value == null)
                continue;

            human.Add(new HumanBone
            {
                // Unity's own name for the slot, which is NOT always the enum
                // spelling -- the fingers have spaces in theirs. Asking
                // HumanTrait is the only way to be sure the string matches
                humanName = HumanTrait.BoneName[(int)pair.Key],
                boneName = pair.Value.name,
                limit = new HumanLimit { useDefaultValues = true },
            });
        }

        return human.ToArray();
    }

    // Every transform in the model, in the pose it is now standing in. The root
    // comes first because GetComponentsInChildren starts with itself, and Unity
    // expects the model root as the first entry
    private static SkeletonBone[] Skeleton(Transform root)
    {
        List<SkeletonBone> skeleton = new List<SkeletonBone>();

        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
        {
            skeleton.Add(new SkeletonBone
            {
                name = bone.name,
                position = bone.localPosition,
                rotation = bone.localRotation,
                scale = bone.localScale,
            });
        }

        return skeleton.ToArray();
    }

    // ---- everyone who copies this avatar -------------------------------------

    // Fourteen animals and two animations point at the avatar that was just
    // rebuilt, and a copied avatar does not notice its source changing on its
    // own. Reimported by hand, or half the cast keeps the old pose and the
    // difference shows up as some animals walking properly and some not
    private static string Refresh()
    {
        StringBuilder report = new StringBuilder();

        int done = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[]
                 {
                     animalFolder + "Outlined_Characters",
                     animalFolder + "NoOutline_Charcaters",
                     clipFolder,
                 }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (path == rig)
                continue;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            done++;
        }

        report.AppendLine("Avatari kopyalayan " + done + " dosya yeniden alindi.");

        return report.ToString();
    }

    // ---- back to how it was --------------------------------------------------

    [MenuItem("Cooked Fast/Karakter/1f - Rigi Fabrika Ayarina Dondur", priority = 705)]
    public static void Revert()
    {
        if (AssetImporter.GetAtPath(rig) is not ModelImporter importer)
        {
            Show("Import ayarlari okunamadi:\n" + rig);
            return;
        }

        HumanDescription description = importer.humanDescription;

        // Emptied, not deleted. These two being empty is exactly the state the
        // rig shipped in -- it is what "human: []" and "skeleton: []" meant in
        // the meta file all along
        description.human = new HumanBone[0];
        description.skeleton = new SkeletonBone[0];
        description.armStretch = .05f;
        description.legStretch = .05f;
        description.feetSpacing = 0f;

        importer.humanDescription = description;
        importer.autoGenerateAvatarMappingIfUnspecified = true;

        importer.SaveAndReimport();

        Show("Rig fabrika ayarina dondu -- Unity yine kendi tahmin edecek.\n\n" +
             Refresh());
    }

    private static void Show(string report)
    {
        Debug.Log("[Kapsul Rig]\n" + report);
        EditorUtility.DisplayDialog("Kapsul Rig", report, "Tamam");
    }
}
#endif
