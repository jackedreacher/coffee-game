#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Measuring half of the customer sizing job. Writes nothing to the scene --
// CustomerSetup owns that -- so there is exactly one place that decides how far
// apart the queue stands.
//
// The sizes are solved rather than multiplied. A multiplier cannot be re-run:
// the second pass multiplies the first pass's result, and the customers do not
// start from one place anyway -- the capsule prefabs were normalised to .92 by
// this tool while the rabbits were never touched at all, so no single factor
// makes them agree. Measuring each prefab and solving for the scale that lands
// it on one height is idempotent, and makes them the same size as each other on
// the way.
public static class CustomerScale
{
    // What counts as a customer's own body. The order bubble hangs off the ROOT
    // rather than off the artwork, and a carried tray hangs off a hand bone --
    // measure either as part of the animal and the queue spreads out to make
    // room for a card that is not standing on the floor.
    private const string bubbleName = "Order Bubble";

    public struct Body
    {
        public float height;
        public float width;
        public bool valid;
    }

    // The transform the artwork hangs from, which is not the prefab root.
    //
    // The root has to stay at scale one: the NavMeshAgent, the tap target, the
    // order bubble and the queue maths all read off it, and none of them should
    // change size because a rabbit got taller. The rig's Animator is the honest
    // anchor -- the capsule prefabs call it "Body" and the rabbits do not call
    // it anything in particular.
    public static Transform Visual(GameObject root)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);

        if (animator != null && animator.transform != root.transform)
            return animator.transform;

        Transform named = root.transform.Find("Body");

        if (named != null)
            return named;

        // One level down is still better than the root, which cannot be scaled.
        return root.transform.childCount > 0 ? root.transform.GetChild(0) : null;
    }

    // Height and width of what is actually drawn, in the ROOT's space.
    //
    // Renderer.bounds is not used, on purpose. It is a box aligned to the WORLD
    // axes, so it measures the artwork AND the angle it is being held at -- the
    // same fault that made the same hat come out two different sizes in the
    // menu and the kitchen. Taken off the mesh instead, nothing about the pose
    // can reach the number.
    public static bool Measure(Transform visual, Transform space, out Bounds box)
    {
        box = default;

        if (visual == null || space == null)
            return false;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (Under(renderer.transform, bubbleName))
                continue;

            if (renderer.GetComponentInParent<Plateau>(true) != null)
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
                    box = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                    box.Encapsulate(point);
            }
        }

        return found;
    }

    // Scales one already-loaded prefab so its artwork stands `wanted` tall, and
    // reports what it ended up as. The caller owns loading and saving, because
    // the caller is the one that has to decide whether the whole batch is worth
    // writing.
    public static Body Fit(GameObject root, float wanted, out string line)
    {
        Body body = default;
        Transform visual = Visual(root);

        if (visual == null)
        {
            line = root.name + ": govde bulunamadi, atlandi";
            return body;
        }

        if (!Measure(visual, root.transform, out Bounds before))
        {
            line = root.name + ": olculemedi (renderer yok), atlandi";
            return body;
        }

        float had = before.size.y;

        if (had <= .0001f)
        {
            line = root.name + ": boyu sifir olculdu, atlandi";
            return body;
        }

        // Relative to whatever art scale was authored, not an absolute value.
        // Some of these prefabs carry a deliberate .59 on the visual and
        // overwriting it would be re-authoring the model, not resizing it.
        visual.localScale *= wanted / had;

        if (!Measure(visual, root.transform, out Bounds after))
        {
            line = root.name + ": olcekten sonra olculemedi";
            return body;
        }

        body.height = after.size.y;
        body.width = Mathf.Max(after.size.x, after.size.z);
        body.valid = true;

        line = root.name + ": boy " + had.ToString("0.00") + " -> " +
               body.height.ToString("0.00") + "   en " +
               body.width.ToString("0.00") +
               "   (olcek " + visual.localScale.x.ToString("0.000") + ")";

        return body;
    }

    // How wide the queue may be at its own depth before it leaves the frame.
    //
    // Solved against the camera rather than assumed, because the kitchen camera
    // is isometric and portrait: the room is much taller on screen than it is
    // wide, and a row that fits comfortably in the Scene view runs off the side
    // of a phone. Walks outwards from the centre until the viewport says no.
    public static float AvailableWidth(Camera camera, Vector3 centre,
        Vector3 sideDirection, float margin)
    {
        if (camera == null || sideDirection.sqrMagnitude < .0001f)
            return -1f;

        Vector3 step = sideDirection.normalized;

        return Mathf.Min(Reach(camera, centre, step, margin),
                   Reach(camera, centre, -step, margin)) * 2f;
    }

    private static float Reach(Camera camera, Vector3 centre, Vector3 step,
        float margin)
    {
        // Bisection on "is this point still on screen". Twenty rounds takes a
        // 32 unit span down to a millimetre, which is far finer than anything
        // the answer is used for.
        float inside = 0f;
        float outside = 32f;

        if (OnScreen(camera, centre + step * outside, margin))
            return outside;

        for (int i = 0; i < 20; i++)
        {
            float middle = (inside + outside) * .5f;

            if (OnScreen(camera, centre + step * middle, margin))
                inside = middle;
            else
                outside = middle;
        }

        return inside;
    }

    public static bool OnScreen(Camera camera, Vector3 point, float margin)
    {
        Vector3 view = camera.WorldToViewportPoint(point);

        return view.z > 0f &&
               view.x > margin && view.x < 1f - margin &&
               view.y > margin && view.y < 1f - margin;
    }

    // The camera the player actually looks through. Camera.main only answers
    // for a tagged, enabled camera, and the kitchen keeps more than one.
    public static Camera GameCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        Camera[] all = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].isActiveAndEnabled &&
                all[i].targetTexture == null)
                return all[i];

        return null;
    }

    private static bool Under(Transform item, string name)
    {
        while (item != null)
        {
            if (item.name == name)
                return true;
            item = item.parent;
        }

        return false;
    }
}
#endif
