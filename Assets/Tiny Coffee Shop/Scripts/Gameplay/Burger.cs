using System.Collections.Generic;
using UnityEngine;

// Built up in the hand, and restacked every time something is added.
//
// Switching each layer on where the model put it looked wrong the moment the
// burger was incomplete: a top bun hanging in the air over nothing, a patty
// floating where the bread should have been. So the layers are not shown in
// place -- they are laid down one on top of another starting from the tray,
// and only the ones collected so far are shown.
//
// The order is the order they are LISTED in, not the order they were picked up.
// Collection order was the first attempt and it reads wrong: cooking the patty
// first is the natural way to play, and it put the patty under the bun. Which
// layer sits where is a property of the recipe, so it belongs in the recipe --
// reorder the Layers array to change the burger.
//
// The lid is the exception. A top bun is not a layer, it is what closes the
// burger, so it only appears once everything else is in
public class Burger : SpawnableFood
{
    [System.Serializable]
    private class Layer
    {
        [Tooltip("Bu katmani temsil eden malzeme prefabi")]
        public SpawnableFood part;

        [Tooltip("O malzeme alininca acilacak obje")]
        public Transform visual;

        [Tooltip("Kalinligi -- ustune gelen katman bu kadar yukari cikar")]
        public float height;

        // Not every model's origin sits on its own underside. A mesh pivoted
        // through its middle placed at the stack height sinks half of itself
        // into the layer below, which is what the cooked patty did to the bun.
        //
        // Display only, deliberately: the layer above is placed from height,
        // which is already right, and adding the lift there as well would push
        // the whole burger up by it
        [Tooltip("Katmani kendi yerinden yukari kaydirir. Mesh'in pivotu ortadaysa gerekir")]
        public float lift;
    }

    [Header(" Katmanlar ")]
    [SerializeField] private Layer[] layers;

    [Header(" Kapak ")]
    [Tooltip("Sadece burger tamamlaninca acilir -- ust ekmek")]
    [SerializeField] private Transform lid;

    // Collection order, which is also stacking order
    private readonly List<System.Type> inside = new List<System.Type>();

    private Vector3[] bases;
    private Vector3 lidBase;

    // Not the serialized flag: this one changes while it is being carried
    public override bool CanBeServed => IsComplete;

    public bool IsComplete => inside.Count >= Needed;

    private int Needed
    {
        get
        {
            int needed = 0;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null && layers[i].part != null)
                    needed++;
            }

            return needed;
        }
    }

    // The sideways placement each layer was authored with is kept; only the
    // height is taken over. Read once, before anything has been moved, or the
    // second rebuild would stack on top of the first one's results
    private void Awake()
    {
        bases = new Vector3[layers.Length];

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null && layers[i].visual != null)
                bases[i] = layers[i].visual.localPosition;
        }

        if (lid != null)
            lidBase = lid.localPosition;

        Rebuild();
    }

    public bool Contains(SpawnableFood food)
    {
        return food != null && inside.Contains(food.GetType());
    }

    public bool Wants(SpawnableFood food)
    {
        // Burnt meat is not an ingredient. Without this it goes into the bun
        // like any other patty, and the burger it produces is servable
        if (food == null || food.IsBurnt)
            return false;

        return IndexOf(food) >= 0 && !Contains(food);
    }

    public void Add(SpawnableFood food)
    {
        // Guarded here as well as in Wants. This is the method that actually
        // mutates, and a caller that decided using Wants on a different object
        // than it later passes in is not hypothetical -- it is the bug that let
        // burnt meat into a burger
        if (food == null || food.IsBurnt)
            return;

        if (IndexOf(food) < 0 || Contains(food))
            return;

        inside.Add(food.GetType());

        Rebuild();
    }

    // Two halves of the same burger put back together.
    //
    // Parking bread on the shelf does not leave bread there, it leaves a burger
    // with one layer in it -- so coming back for it is a burger meeting a
    // burger, not a burger meeting an ingredient. Without this the only way to
    // reunite them is the bin
    public bool CanTake(Burger other)
    {
        if (other == null || other == this || other.inside.Count <= 0)
            return false;

        for (int i = 0; i < other.inside.Count; i++)
        {
            if (inside.Contains(other.inside[i]))
                return false;
        }

        return true;
    }

    // Which end they go on stopped mattering once the stack started reading the
    // Layers list for its order. This is now only "the two halves know between
    // them what has been collected"
    public void Take(Burger other)
    {
        if (!CanTake(other))
            return;

        inside.AddRange(other.inside);

        Rebuild();
    }

    // What is still missing, for the console and for anyone wondering why a
    // burger will not go to a customer
    public string Missing()
    {
        string missing = "";

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null || layers[i].part == null)
                continue;

            if (inside.Contains(layers[i].part.GetType()))
                continue;

            missing += (missing.Length > 0 ? ", " : "") + layers[i].part.GetType().Name;
        }

        return missing;
    }

    private void Rebuild()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null && layers[i].visual != null)
                layers[i].visual.gameObject.SetActive(false);
        }

        if (lid != null)
            lid.gameObject.SetActive(false);

        float height = 0f;

        // Walked in list order rather than in collection order, so a half built
        // burger is the finished one with the missing layers left out -- the bun
        // is on the bottom whether it was picked up first or last
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null || layers[i].part == null || layers[i].visual == null)
                continue;

            if (!inside.Contains(layers[i].part.GetType()))
                continue;

            Transform visual = layers[i].visual;

            visual.gameObject.SetActive(true);

            // Y taken over entirely rather than added to: the bottom of the
            // stack belongs on the tray whichever layer happens to be there
            visual.localPosition = new Vector3(bases[i].x, height + layers[i].lift, bases[i].z);

            height += Mathf.Max(0f, layers[i].height);
        }

        if (!IsComplete || lid == null)
            return;

        lid.gameObject.SetActive(true);
        lid.localPosition = new Vector3(lidBase.x, height, lidBase.z);
    }

    private int IndexOf(SpawnableFood food)
    {
        return food == null ? -1 : IndexOfType(food.GetType());
    }

    private int IndexOfType(System.Type type)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null && layers[i].part != null && layers[i].part.GetType() == type)
                return i;
        }

        return -1;
    }
}
