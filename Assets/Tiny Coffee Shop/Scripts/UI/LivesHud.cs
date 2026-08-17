using UnityEngine;
using UnityEngine.UI;

// The row of hearts at the top of the screen.
//
// A SCREEN space canvas, unlike everything else this project draws. The rule
// learned the hard way is about WORLD space canvases -- their scale, their
// facing and their draw order are all decided somewhere else and all of them
// fail silently. A screen overlay has none of those questions: it is at the top
// of the screen because it is at the top of the screen.
//
// Both sprites are swapped on the same Image rather than stacking a full heart
// over an empty one. Two objects per slot is two things to keep aligned
public class LivesHud : MonoBehaviour
{
    [Header(" Elements ")]
    [Tooltip("Soldan saga can slotlari")]
    [SerializeField] private Image[] hearts;

    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;

    // The pack's heart is white, so the colour has to come from here. Kept as
    // fields rather than written once at build time: a life being lost is the
    // hardest thing on this screen to notice, and how hard it reads is worth
    // being able to try
    [Header(" Renkler ")]
    [SerializeField] private Color fullColour = new Color(.91f, .26f, .30f);

    [Tooltip("Kaybedilen can. Sonuk, ama hala orada -- kac tane oldugu bilinsin")]
    [SerializeField] private Color emptyColour = new Color(.32f, .27f, .29f, .5f);

    private Lives lives;

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        if (lives != null)
            lives.Changed -= Draw;

        lives = null;
    }

    // Lives may wake after this does, and the order of two Awakes in a scene is
    // not something to rely on. Retried every frame until it is there, then not
    // again -- the alternative is a HUD that is empty for one run in three
    private void Update()
    {
        if (lives == null)
            Bind();
    }

    private void Bind()
    {
        if (lives != null || Lives.Instance == null)
            return;

        lives = Lives.Instance;
        lives.Changed += Draw;

        Draw();
    }

    private void Draw()
    {
        if (hearts == null || lives == null)
            return;

        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null)
                continue;

            // Slots past the maximum are hidden rather than drawn empty. A row
            // of five with two greyed out reads as two lives already lost
            bool exists = i < lives.Max;

            hearts[i].gameObject.SetActive(exists);

            if (!exists)
                continue;

            bool held = i < lives.Left;

            hearts[i].sprite = held ? fullHeart : emptyHeart;
            hearts[i].color = held ? fullColour : emptyColour;
        }
    }
}
