using System.Collections.Generic;
using UnityEngine;

// Runtime hat mounting shared by the menu preview and the real player.
// LowpolyHats use different pivots and dimensions, while the capsule animals
// have very different heads. Measuring both meshes makes one catalogue usable
// on every animal without authoring hundreds of prefab overrides.
public static class PlayerHatFitter
{
    public const string MountedName = "PLAYER ACCESSORY (HAT)";

    // Both numbers are multiples of the feet-to-head-bone distance, and that
    // distance is read off a BONE rather than off the artwork. Three earlier
    // attempts are worth writing down, because each one looked reasonable:
    //
    // 1. Brim a fraction of the BODY's width above the head bone. But on this
    //    rig the head bone is at the neck -- 1.23 up a 2.17-tall animal, with
    //    the whole skull above it -- and body width has nothing to do with head
    //    height. Every hat ended up buried, each by a different amount.
    //
    // 2. Brim a fraction of the way from the head bone to the TOP of the
    //    animal. Correct on the pug, floating on the bull and the cat, because
    //    the top of a bull is its horns and the top of a cat is its ears.
    //
    // 3. Same, measured from the bottom of the renderer bounds in world space.
    //    This made the menu and the game disagree about the same animal with
    //    the same numbers: the wardrobe leans its model 16 degrees towards the
    //    camera, and a tilted model has a different world bounding box.
    //
    // 4. All of the above fixed, and the menu still fitted a hat 29 percent
    //    smaller than the game on the same animal with the same numbers. The
    //    numbers were never the problem: the hat was being MEASURED with
    //    Renderer.bounds, which is a box aligned to the world axes rather than
    //    to the hat. The wardrobe holds its model at a 16 degree lean, so a
    //    long hat -- a shark, not a beret -- got a box a third larger than
    //    itself and was shrunk to fit that box. The two ratio lines in the
    //    diagnostic agreed to within 3 percent throughout, because they were
    //    measured with the same inflated ruler that caused the fault.
    //
    // What survives all four: the model origin sits at the feet, so the head
    // bone's height in the character's own space is the measurement, and it
    // reads 1.23 on every one of the fifteen -- tilted or upright, scaled or
    // not, carrying a tray or not. The skull's real height above that bone
    // still varies per animal and cannot be measured (the models import with
    // Read/Write off), which is what HatFitBook is for. The hat itself is
    // measured off its mesh, never off a world-aligned box, so no pose and no
    // facing can change the size it comes out.
    public const float DefaultCrown = 1.55f;
    public const float DefaultHeadWidth = .69f;

    // Along the head bone's forward. Zero is centred on the bone, which is
    // where a hat belongs until an animal's snout says otherwise.
    public const float DefaultForward = 0f;

    // Where the brim sits and how wide the hat is, both as multiples of the
    // feet-to-head-bone distance. Fields rather than constants so the tuner
    // window can move them while the game is running; whatever value settles
    // here belongs back in the defaults above.
    public static float CrownMultiplier = DefaultCrown;
    public static float HeadWidthMultiplier = DefaultHeadWidth;
    public static float ForwardMultiplier = DefaultForward;

    private static readonly Dictionary<Material, Material> convertedMaterials =
        new Dictionary<Material, Material>();

    // `animal` is the wardrobe's name for this skin, and it is the key into
    // HatFitBook. It has to be handed in rather than read off the character,
    // because in game the object is called "Body" and in the menu it is called
    // "Preview Cat" -- neither of which identifies the animal.
    public static GameObject Equip(Animator animator, GameObject character,
        GameObject hatPrefab, string animal = null)
    {
        if (animator == null || character == null)
            return null;

        Remove(animator.transform);

        if (hatPrefab == null)
            return null;

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
        {
            Debug.LogWarning("[Sapka] " + character.name +
                             " icin Humanoid Head kemigi bulunamadi.", character);
            return null;
        }

        // Everything below is in the CHARACTER's own space, and the only thing
        // measured is a bone.
        //
        // Reading the floor off the renderer bounds made the menu and the game
        // disagree on the same animal with the same numbers, because the
        // wardrobe leans its model 16 degrees towards the camera and a tilted
        // model has a different world bounding box than an upright one. Bone
        // positions do not care: on this rig the model origin sits at the feet,
        // so the head bone's local height IS the feet-to-head-bone distance --
        // 1.23 on every one of the fifteen, tilted or not, scaled or not.
        // Everything is measured and placed in the HEAD BONE's own frame, which
        // is the frame the hat lives in for the rest of its life.
        //
        // Doing it in the body's frame and then freezing the result onto the
        // head left one last pose dependency: the lift was applied along the
        // BODY's up while the offset was stored against the HEAD, so a head
        // that happened to be tilted at the moment of fitting handed the hat an
        // offset that swung as soon as the head straightened. Measured here,
        // nothing the animation does can reach the result.
        Transform space = head;

        // The rig's own height, summed along the bone chain rather than read
        // off the live pose.
        //
        // headPlace.y looked like the same constant everywhere and is not: it
        // is where the head IS this frame. The wardrobe fits its hat on the
        // first frame of a greeting clip, the kitchen fits one on whatever the
        // waiter happens to be doing, and a capsule animal's head sits a good
        // tenth of a unit apart between those two poses. Bone LENGTHS do not
        // move -- Unity animates a humanoid by rotating bones, never by
        // stretching them -- and on this rig they sum to 1.23 for all fifteen.
        float skeleton = RigHeight(head, character.transform);

        if (skeleton <= .0001f)
            return null;

        // This animal's own numbers when it has been dialled in, the shared
        // defaults when it has not.
        float crown = CrownMultiplier;
        float width = HeadWidthMultiplier;
        float forward = ForwardMultiplier;

        if (HatFitBook.TryGet(animal, out float tunedCrown, out float tunedWidth,
                out float tunedForward))
        {
            crown = tunedCrown;
            width = tunedWidth;
            forward = tunedForward;
        }

        float wantedWidth = skeleton * width;
        float push = skeleton * forward;

        // Measured up from the HEAD BONE, not to an absolute height inside the
        // body. The seat has to travel with the head; pinning it to a body
        // height makes the result depend on where the animation was holding
        // that head at the instant the hat happened to be fitted.
        float lift = skeleton * (crown - 1f);

        // Parented to the head from the start, rather than fitted somewhere
        // else and moved here afterwards.
        GameObject hat = Object.Instantiate(hatPrefab, head);
        hat.name = MountedName;
        hat.transform.localPosition = Vector3.zero;
        hat.transform.localRotation = Quaternion.identity;
        hat.transform.localScale = Vector3.one;
        SetLayer(hat.transform, character.layer);
        UseUrpMaterials(hat);

        if (!TryLocalBounds(hat.transform, space, out Bounds rawHat))
        {
            // Said out loud. A hat that cannot be measured used to disappear
            // in silence, which reads as "the hat system is broken" rather
            // than "this prefab has no renderers".
            Debug.LogWarning("[Sapka] " + hatPrefab.name +
                             " olculemedi, takilmadi.", character);
            Object.Destroy(hat);
            return null;
        }

        float drawnWidth = Mathf.Max(rawHat.size.x, rawHat.size.z);
        float scale = drawnWidth > .0001f ? wantedWidth / drawnWidth : 1f;
        hat.transform.localScale = Vector3.one * scale;

        if (!TryLocalBounds(hat.transform, space, out Bounds fittedHat))
        {
            Object.Destroy(hat);
            return null;
        }

        // Brim onto the seat, centred on the neck axis. Measured after scaling,
        // so a hat whose pivot is not at its brim still lands correctly.
        // Brim `lift` above the head bone, centred on it. Measured after
        // scaling, so a hat whose pivot is not at its brim still lands right.
        Vector3 hatSeat = new Vector3(fittedHat.center.x, fittedHat.min.y,
            fittedHat.center.z);
        hat.transform.localPosition += new Vector3(0f, lift, push) - hatSeat;
        return hat;
    }

    // Distance from the model root up to the head, walked along the bone chain.
    //
    // Pose independent by construction: every localPosition below the hips is a
    // bone length, and humanoid animation never changes one. On the DGN rig
    // this reads Hips .408 + Spine .360 + Neck .374 + Head .092 = 1.234, which
    // is the same number the diagnostic reports for every animal.
    private static float RigHeight(Transform head, Transform root)
    {
        float total = 0f;
        Transform node = head;

        while (node != null && node != root)
        {
            total += node.localPosition.magnitude;
            node = node.parent;
        }

        return total;
    }

    public static void Remove(Transform character)
    {
        if (character == null)
            return;

        Transform[] all = character.GetComponentsInChildren<Transform>(true);
        for (int i = all.Length - 1; i >= 0; i--)
        {
            if (all[i] == null || all[i].name != MountedName)
                continue;

            all[i].gameObject.SetActive(false);
            Object.Destroy(all[i].gameObject);
        }
    }

    public static bool HasMounted(Transform character)
    {
        if (character == null)
            return false;

        Transform[] all = character.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == MountedName)
                return true;
        return false;
    }

    // Public because the revolver needs it too. The Dead West pack and the
    // LowpolyHats pack have the same problem -- Standard shaders, which this
    // project renders as flat magenta -- and the conversion cache below only
    // works if both go through the one copy of it.
    public static void UseUrpMaterials(GameObject hat)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            return;

        Renderer[] renderers = hat.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] source = renderers[i].sharedMaterials;
            Material[] ready = new Material[source.Length];
            bool changed = false;

            for (int m = 0; m < source.Length; m++)
            {
                ready[m] = Convert(source[m], shader);
                changed |= ready[m] != source[m];
            }

            if (changed)
                renderers[i].sharedMaterials = ready;
        }
    }

    private static Material Convert(Material source, Shader shader)
    {
        if (source == null || source.shader == shader ||
            (source.shader != null && source.shader.name.StartsWith(
                "Universal Render Pipeline/")))
            return source;

        if (convertedMaterials.TryGetValue(source, out Material cached) &&
            cached != null)
            return cached;

        Texture texture = source.HasProperty("_BaseMap")
            ? source.GetTexture("_BaseMap")
            : source.mainTexture;
        Color colour = source.HasProperty("_BaseColor")
            ? source.GetColor("_BaseColor")
            : source.color;
        float smoothness = source.HasProperty("_Smoothness")
            ? source.GetFloat("_Smoothness")
            : source.HasProperty("_Glossiness")
                ? source.GetFloat("_Glossiness")
                : 0f;

        Material converted = new Material(shader)
        {
            name = source.name + " (URP Runtime)",
            enableInstancing = true,
        };
        if (converted.HasProperty("_BaseMap"))
            converted.SetTexture("_BaseMap", texture);
        if (converted.HasProperty("_BaseColor"))
            converted.SetColor("_BaseColor", colour);
        if (converted.HasProperty("_Smoothness"))
            converted.SetFloat("_Smoothness", smoothness);

        convertedMaterials[source] = converted;
        return converted;
    }

    // Bounds of everything under `measured`, expressed in `space`.
    //
    // Inactive renderers are INCLUDED, and that is deliberate. Filtering them
    // out looked tidier and silently broke the wardrobe: the preview model is
    // built inside a panel that is not showing yet, so an active-only sweep
    // came back empty, the fit failed and the hat was destroyed instead of
    // worn. The game kept working because the Player is always active, which
    // is exactly the sort of one-sided break this file keeps producing.
    //
    // The tray and its food hang off a hand bone, so they sit inside the
    // character and were being measured as part of the animal.
    // CharacterSkinPreview.TryVisualBounds has always skipped them; the two
    // measurements have to agree or the menu and the game fit different hats.
    private static bool TryLocalBounds(Transform measured, Transform space,
        out Bounds result)
    {
        bool measuringHat = IsManagedHat(measured);
        Renderer[] renderers = measured.GetComponentsInChildren<Renderer>(true);
        result = default;
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            if (!measuringHat && IsManagedHat(renderer.transform))
                continue;

            if (renderer.GetComponentInParent<Plateau>(true) != null)
                continue;

            // The renderer's own box, carried through its own transform.
            // Renderer.bounds cannot be used here: it is aligned to the WORLD
            // axes, so it is not a measurement of the hat at all, it is a
            // measurement of the hat AND the angle it happens to be held at.
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

    private static bool IsManagedHat(Transform item)
    {
        while (item != null)
        {
            if (item.name == MountedName)
                return true;
            item = item.parent;
        }

        return false;
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }
}

// The menu preview is disabled after Play, so it cannot be responsible for
// watching later Player body replacements. This tiny persistent follower keeps
// the saved accessory on whichever Animator PlayerAnimator currently owns.
public sealed class PlayerHatRuntimeFollower : MonoBehaviour
{
    private CharacterSkinPreview wardrobe;
    private PlayerAnimator player;
    private Animator appliedAnimator;
    private int appliedHat = int.MinValue;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        PlayerHatRuntimeFollower existing = Object.FindFirstObjectByType<
            PlayerHatRuntimeFollower>(FindObjectsInactive.Include);
        if (existing != null)
            return;

        GameObject host = new GameObject("PLAYER ACCESSORY RUNTIME");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<PlayerHatRuntimeFollower>();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + .15f;

        if (wardrobe == null)
            wardrobe = Object.FindFirstObjectByType<CharacterSkinPreview>(
                FindObjectsInactive.Include);
        if (player == null)
            player = Object.FindFirstObjectByType<PlayerAnimator>(
                FindObjectsInactive.Include);
        if (wardrobe == null || player == null || player.CurrentAnimator == null)
            return;

        Animator current = player.CurrentAnimator;
        bool shouldHaveHat = wardrobe.SelectedHat >= 0;
        bool hasHat = PlayerHatFitter.HasMounted(current.transform);
        if (current == appliedAnimator && wardrobe.SelectedHat == appliedHat &&
            shouldHaveHat == hasHat)
            return;

        wardrobe.ApplySelectedHat(current);
        appliedAnimator = current;
        appliedHat = wardrobe.SelectedHat;
    }
}
