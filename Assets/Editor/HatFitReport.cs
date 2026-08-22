#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

// Side-by-side measurement of the two places a hat gets fitted: the wardrobe's
// off-screen preview copy and the real Player in the kitchen.
//
// They are supposed to produce the same PROPORTIONS -- the same hat width and
// the same brim height, both expressed as multiples of the feet-to-head-bone
// distance. Anything else is a difference in camera framing, not in fitting.
// The two ratio lines at the bottom of each block are the ones that matter;
// if they match, the fitter is consistent and the eye is being fooled by two
// panels of different sizes showing the same render texture.
//
// Nothing here writes to a scene, a prefab or an asset. Run it in Play Mode.
public static class HatFitReport
{
    private const string stageName = "MENU 3D CHARACTER STAGE";

    [MenuItem("Cooked Fast/Sapka/Sapka Oturmasini Raporla", priority = 224)]
    public static void Report()
    {
        StringBuilder text = new StringBuilder();

        CharacterSkinPreview wardrobe = Object.FindFirstObjectByType<
            CharacterSkinPreview>(FindObjectsInactive.Include);
        string animal = wardrobe == null ? null : wardrobe.SelectedAnimal;

        bool tuned = HatFitBook.TryGet(animal, out float crown, out float width,
            out float forward);
        string source = tuned ? "   (bu hayvana ozel)" : "   (ortak varsayilan)";

        text.AppendLine("hayvan               : " +
                        (string.IsNullOrWhiteSpace(animal) ? "(okunamadi)" : animal));
        text.AppendLine("Yukseklik carpani    : " + crown.ToString("0.###") + source);
        text.AppendLine("Genislik carpani     : " + width.ToString("0.###") + source);
        text.AppendLine("Ileri/Geri carpani   : " + forward.ToString("0.###") + source);
        text.AppendLine();

        PlayerAnimator player = Object.FindFirstObjectByType<PlayerAnimator>(
            FindObjectsInactive.Include);

        Describe("OYUN (Player)",
            player == null ? null : player.CurrentAnimator, crown, width, text);

        Describe("VITRIN (menu onizlemesi)", PreviewRig(), crown, width, text);

        text.AppendLine("Iki bloktaki 'ORAN' satirlari ayni olmali. Ayni ise");
        text.AppendLine("fitting tutarli; fark goruntude ise panel/kamera");
        text.AppendLine("kadrajindan geliyordur.");

        Debug.Log("[Sapka Raporu]\n" + text);
        EditorUtility.DisplayDialog("Sapka Raporu",
            text + "\nTam metin Console'da.", "Tamam");
    }

    // The wardrobe keeps its model 200 units under the kitchen on a stage it
    // builds at runtime, and does not expose it. Finding it by name avoids
    // widening a runtime class's API for a diagnostic.
    private static Animator PreviewRig()
    {
        GameObject stage = GameObject.Find(stageName);

        if (stage == null)
            return null;

        Animator[] rigs = stage.GetComponentsInChildren<Animator>(true);
        return rigs.Length > 0 ? rigs[0] : null;
    }

    private static void Describe(string label, Animator rig, float crown,
        float width, StringBuilder text)
    {
        text.AppendLine("== " + label + " ==");

        if (rig == null)
        {
            text.AppendLine("  yok / bulunamadi");
            text.AppendLine();
            return;
        }

        Transform body = rig.transform;

        text.AppendLine("  nesne              : " + Path(body));
        text.AppendLine("  localScale         : " + body.localScale);
        text.AppendLine("  lossyScale         : " + body.lossyScale);
        text.AppendLine("  localRotation      : " + body.localEulerAngles);

        Transform head = rig.isHuman
            ? rig.GetBoneTransform(HumanBodyBones.Head)
            : null;

        if (head == null)
        {
            text.AppendLine("  Humanoid Head kemigi YOK -- sapka takilamaz.");
            text.AppendLine();
            return;
        }

        // Exactly what Equip measures: the head bone's height in the
        // character's own space. No renderer bounds in the seat maths.
        float skeleton = body.InverseTransformPoint(head.position).y;

        text.AppendLine("  kafa kemigi acisi  : " + head.rotation.eulerAngles);
        text.AppendLine("  ayak -> kafa kemigi: " + skeleton.ToString("0.0000"));
        text.AppendLine("  -> kenarin Y'si    : " +
                        (skeleton * crown).ToString("0.0000"));
        text.AppendLine("  -> sapka eni       : " +
                        (skeleton * width).ToString("0.0000"));

        Transform hat = Mounted(body);

        if (hat == null)
        {
            text.AppendLine("  TAKILI SAPKA YOK.");
            text.AppendLine();
            return;
        }

        text.AppendLine("  sapka parent       : " + hat.parent.name);
        text.AppendLine("  sapka localScale   : " + hat.localScale);

        // Where the brim actually landed, back in the body's own space. This is
        // the measurement that proves the seat was honoured rather than the one
        // that says what was asked for.
        if (LocalBounds(hat, body, out Bounds box))
        {
            text.AppendLine("  gercek kenar Y     : " + box.min.y.ToString("0.0000"));
            text.AppendLine("  gercek sapka eni   : " +
                            Mathf.Max(box.size.x, box.size.z).ToString("0.0000"));
            text.AppendLine("  ORAN kenar/iskelet : " +
                            (box.min.y / skeleton).ToString("0.0000"));
            text.AppendLine("  ORAN eni/iskelet   : " +
                            (Mathf.Max(box.size.x, box.size.z) / skeleton)
                            .ToString("0.0000"));
        }

        // The fitter sizes the hat off Renderer.bounds, which is a box aligned
        // to the WORLD axes -- so a head that is turned or leaning hands it a
        // box bigger than the hat really is, and the hat is shrunk to fit that
        // phantom. Here is the same hat measured off its mesh instead, which no
        // rotation can inflate. 1.000 means the pose is costing nothing; the
        // two sides showing DIFFERENT numbers is the menu and the game
        // disagreeing, and by exactly this much.
        if (LocalBounds(hat, body, out Bounds aabb) &&
            MeshBounds(hat, body, out Bounds real))
        {
            float drawn = Mathf.Max(aabb.size.x, aabb.size.z);
            float honest = Mathf.Max(real.size.x, real.size.z);

            text.AppendLine("  kutu sisme orani   : " +
                            (honest > .0001f ? drawn / honest : 1f)
                            .ToString("0.000"));
        }

        text.AppendLine();
    }

    private static bool LocalBounds(Transform measured, Transform space,
        out Bounds result)
    {
        Renderer[] renderers = measured.GetComponentsInChildren<Renderer>(true);
        result = default;
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            Bounds world = renderer.bounds;
            Vector3 min = world.min;
            Vector3 max = world.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = space.InverseTransformPoint(new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z));

                if (!found)
                {
                    result = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                    result.Encapsulate(point);
            }
        }

        return found;
    }

    // Same sweep as LocalBounds, but each renderer's box is taken in the
    // renderer's OWN space and carried through its transform, so the result is
    // the hat's real extent rather than a world-aligned box drawn around it.
    private static bool MeshBounds(Transform measured, Transform space,
        out Bounds result)
    {
        Renderer[] renderers = measured.GetComponentsInChildren<Renderer>(true);
        result = default;
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            Bounds local = renderer.localBounds;
            Transform frame = renderer.transform;

            if (renderer is SkinnedMeshRenderer skinned && skinned.rootBone != null)
                frame = skinned.rootBone;

            Vector3 min = local.min;
            Vector3 max = local.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = space.InverseTransformPoint(frame.TransformPoint(
                    new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z)));

                if (!found)
                {
                    result = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                    result.Encapsulate(point);
            }
        }

        return found;
    }

    private static Transform Mounted(Transform character)
    {
        Transform[] all = character.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == PlayerHatFitter.MountedName)
                return all[i];

        return null;
    }

    private static string Path(Transform item)
    {
        if (item == null)
            return "(yok)";

        string path = item.name;

        while (item.parent != null)
        {
            item = item.parent;
            path = item.name + "/" + path;
        }

        return path;
    }
}
#endif
