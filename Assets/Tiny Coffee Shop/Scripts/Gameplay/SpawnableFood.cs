using UnityEngine;
using UnityEngine.Serialization;

public abstract class SpawnableFood : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private MeshFilter filter;
    [SerializeField] private MeshRenderer meshRenderer;

    [Header(" Settings ")]
    [SerializeField] private float cleanYOffsetOnPlateau;
    [SerializeField] private float dirtyYOffsetOnPlateau;
    [SerializeField] private Mesh dirtyMesh;

    // One flag on the food, rather than a rule anywhere else. Serving asks the
    // item whether it may go out and nothing else has to know which foods have
    // an oven, which ones are burger parts, or what a customer ordered.
    //
    // Renamed from needsCooking once bread and cheese needed the same treatment:
    // they are not raw, they are simply not a meal on their own. The old name is
    // kept for deserialisation so the flag already set on meat survives
    [Header(" Servis ")]
    [Tooltip("Isaretliyse tek basina musteriye verilemez -- cig et, burger parcalari")]
    [FormerlySerializedAs("needsCooking")]
    [SerializeField] private bool ingredientOnly;

    // Carried by the food itself rather than looked up in a table somewhere.
    // A new food that arrives with its own picture needs nothing else told
    // about it, and there is no list to fall out of step with the prefabs
    [Header(" Ikon ")]
    [Tooltip("Siparis balonunda gorunecek resim. Bos ise modelin kendisi kullanilir")]
    [SerializeField] private Sprite icon;

    public Sprite Icon => icon;

    [Header(" Yanik ")]
    [Tooltip("Yanan yemegin alacagi renk")]
    [SerializeField] private Color burntColour = new Color(.08f, .06f, .05f);

    private bool isDirty;
    private bool burnt;

    public float CleanYOffsetOnPlateau => cleanYOffsetOnPlateau;
    public float DirtyYOffsetOnPlateau => dirtyYOffsetOnPlateau;
    public bool IsDirty => isDirty;
    public bool IsVisible => meshRenderer.enabled;

    public bool IngredientOnly => ingredientOnly;

    // Burnt food is good for exactly one thing, and every rule that keeps it out
    // of everything else reads this. Kept on SpawnableFood rather than on the
    // oven so the food carries its own state once it leaves the pan
    public bool IsBurnt => burnt;

    // Virtual because one food answers this from its own state rather than from
    // a checkbox: a burger is servable once it has all its layers, and not
    // before, and that changes while the player is carrying it
    public virtual bool CanBeServed => !ingredientOnly && !burnt;

    private static readonly int baseColourId = Shader.PropertyToID("_BaseColor");
    private static readonly int colourId = Shader.PropertyToID("_Color");
    private static readonly int tintId = Shader.PropertyToID("_Tint");

    public void MarkAsBurnt()
    {
        if (burnt)
            return;

        burnt = true;

        // renderer.materials, not sharedMaterials and not a property block.
        //
        // The instanced copy is the safe one: reading .materials clones them for
        // this renderer alone, so the asset on disk and every other piece of
        // meat in the kitchen are untouched. It was a property block first, out
        // of a misplaced worry about exactly that -- and a block only lands if
        // the shader declares the property being written, which is a silent
        // no-op on any shader that names its colour something else
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                // Whichever the shader actually has. URP Lit uses _BaseColor,
                // older and hand written ones _Color, and some toon shaders
                // tint through _Tint instead
                if (material.HasProperty(baseColourId))
                    material.SetColor(baseColourId, burntColour);

                if (material.HasProperty(colourId))
                    material.SetColor(colourId, burntColour);

                if (material.HasProperty(tintId))
                    material.SetColor(tintId, burntColour);
            }

            renderer.materials = materials;
        }
    }

    public void MarkAsDirty()
    {
        isDirty = true;
        filter.mesh = dirtyMesh;
    }

    public void Display()
    {
        meshRenderer.enabled = true;
    }

    public void Hide()
    {
        meshRenderer.enabled = false;
    }
}
