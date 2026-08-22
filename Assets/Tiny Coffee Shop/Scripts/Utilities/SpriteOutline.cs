using UnityEngine;

// A white edge around a sprite, built out of copies of the sprite itself.
//
// Both ticks in this game land on top of something dark. One sits over an oven,
// the other over a picture of food that has just been greyed out to say it is
// dealt with -- and a green mark on a dark ground is a green mark you have to
// go looking for. The edge is what makes it findable without looking.
//
// EIGHT COPIES NUDGED OUTWARDS, rather than the obvious cheaper trick of one
// white copy scaled up a little. Scaling moves pixels away from the CENTRE, so
// the fringe it leaves grows with a pixel's distance from the middle: on a tick
// -- whose stroke runs straight through the middle -- that is a fat white blob
// at the two ends and no edge at all where the strokes cross. An offset copy
// moves every pixel the same distance in one direction, and eight of those
// spaced around a circle is an outline of even width.
//
// Always drawn one sorting order BEHIND the mark, which is the whole reason the
// mark's own colour is never touched by any of this
public static class SpriteOutline
{
    // As a fraction of the sprite's own size. Thinner than this and it stops
    // reading at the distance the kitchen camera sits at
    private const float thickness = .08f;

    public static void Build(GameObject host, Sprite icon, int order)
    {
        Build(host, icon, order, thickness);
    }

    // Built only if it is not there already, from whatever sprite the mark is
    // actually showing.
    //
    // The station ticks are made by an editor command, so a tick sitting in a
    // scene saved before that command grew an outline simply does not have one
    // -- and asking somebody to re-run a setup tool to get a border is asking
    // them to remember. This is the same edge, added at runtime, and it costs
    // nothing on a tick that already has one
    public static void Ensure(GameObject host)
    {
        if (host == null)
            return;

        SpriteRenderer mark = host.GetComponent<SpriteRenderer>();

        if (mark == null)
            mark = host.GetComponentInChildren<SpriteRenderer>(true);

        if (mark == null || mark.sprite == null)
            return;

        // Onto the RENDERER's own object, not the root it was asked about. The
        // offsets below are in that transform's units, so a border parented a
        // level up would be scaled by whatever sits in between
        if (mark.transform.Find("Border") != null)
            return;

        Build(mark.gameObject, mark.sprite, mark.sortingOrder - 1);
    }

    public static void Build(GameObject host, Sprite icon, int order, float fraction)
    {
        if (host == null || icon == null)
            return;

        // In the host's own units, deliberately.
        //
        // Sprite.bounds is the size the renderer draws at a scale of one, which
        // is exactly the space these offsets live in -- so the edge keeps the
        // same width RELATIVE to the mark no matter what scale the mark is
        // given afterwards. A stroke in world units would be hairline on the
        // station tick and a slab on the bubble one
        float stroke = Mathf.Max(icon.bounds.size.x, icon.bounds.size.y) * fraction;

        if (stroke <= .0001f)
            return;

        GameObject border = new GameObject("Border");

        border.transform.SetParent(host.transform, false);
        border.transform.localPosition = Vector3.zero;
        border.transform.localRotation = Quaternion.identity;
        border.transform.localScale = Vector3.one;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;

            GameObject arm = new GameObject("Edge " + i);

            arm.transform.SetParent(border.transform, false);

            // Same depth as the mark, not pushed behind it. Between two
            // transparent sprites the sorting order decides and the distance
            // does not, so a depth offset would buy nothing here and would
            // start to show as a shadow the moment the pair is seen at an angle
            arm.transform.localPosition =
                new Vector3(Mathf.Cos(angle) * stroke, Mathf.Sin(angle) * stroke, 0f);

            arm.transform.localRotation = Quaternion.identity;

            SpriteRenderer edge = arm.AddComponent<SpriteRenderer>();

            edge.sprite = icon;
            edge.color = Color.white;
            edge.sortingOrder = order;
        }
    }
}
