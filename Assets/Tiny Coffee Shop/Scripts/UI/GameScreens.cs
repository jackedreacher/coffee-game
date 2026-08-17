using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The three screens that are not the game: press play, get ready, you died.
//
// One component for all three because they are one thing -- a state the game is
// in rather than three widgets. Two of them stop the clock and one does not,
// and keeping that decision in a single place is what stops the game being left
// paused by whichever screen closed last
public class GameScreens : MonoBehaviour
{
    // Read by RoundManager before it starts the first round. Static so a scene
    // without this component answers "nothing is in the way" rather than
    // needing a reference to something that is not there
    public static bool Blocking { get; private set; }

    [Header(" Ekranlar ")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject roundPanel;
    [SerializeField] private GameObject overPanel;

    [Header(" Yazilar ")]
    [SerializeField] private TextMeshProUGUI readyLabel;
    [SerializeField] private TextMeshProUGUI roundLabel;
    [SerializeField] private TextMeshProUGUI overTitle;
    [SerializeField] private TextMeshProUGUI overDetail;

    [Header(" Tuslar ")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button restartButton;

    [Header(" Settings ")]
    [Tooltip("HAZIRLAN yazisinin girip cikma suresi, saniye. 0 = varsayilan 0.25")]
    [SerializeField] private float bannerTime = .25f;

    private float BannerTime => bannerTime > .01f ? bannerTime : .25f;

    private Lives lives;
    private Coroutine banner;

    private void Awake()
    {
        // Set in Awake, not Start. RoundManager reads it in the first frame of
        // its coroutine and Start order between the two is not something to bet
        // a hung game on
        Blocking = startPanel != null;

        Switch(startPanel, startPanel != null);
        Switch(roundPanel, false);
        Switch(overPanel, false);

        if (playButton != null)
            playButton.onClick.AddListener(Play);

        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);
    }

    private void OnEnable()
    {
        RoundManager.RoundAnnounced += Announce;
        RoundManager.RoundStarted += Begin;
        RoundManager.AllRoundsFinished += Won;
    }

    private void OnDisable()
    {
        RoundManager.RoundAnnounced -= Announce;
        RoundManager.RoundStarted -= Begin;
        RoundManager.AllRoundsFinished -= Won;

        Unbind();
    }

    private void Start()
    {
        // Paused from the first frame if there is a start screen. Doing it in
        // Start rather than Awake gives everything else its one frame to wake
        // up -- an Awake that never runs because time stopped first is a whole
        // scene that never initialises
        if (startPanel != null)
            Time.timeScale = 0f;
    }

    // Lives may wake after this does. Retried until it is there, then not again
    private void Update()
    {
        if (lives != null || Lives.Instance == null)
            return;

        lives = Lives.Instance;
        lives.Emptied += Died;
    }

    private void Unbind()
    {
        if (lives != null)
            lives.Emptied -= Died;

        lives = null;
    }

    // ---- the three screens --------------------------------------------------

    private void Play()
    {
        Switch(startPanel, false);

        Blocking = false;
        Time.timeScale = 1f;
    }

    private void Announce(int round)
    {
        if (roundPanel == null)
            return;

        if (readyLabel != null)
            readyLabel.text = "HAZIRLAN";

        if (roundLabel != null)
            roundLabel.text = "ROUND " + round;

        Switch(roundPanel, true);

        if (banner != null)
            StopCoroutine(banner);

        banner = StartCoroutine(Popping(roundPanel.transform, true));
    }

    private void Begin(int round)
    {
        if (roundPanel == null || !roundPanel.activeSelf)
            return;

        if (banner != null)
            StopCoroutine(banner);

        banner = StartCoroutine(Popping(roundPanel.transform, false));
    }

    private void Died()
    {
        Show(overPanel, "OLDUN", "Canlarin bitti");
    }

    private void Won()
    {
        // A game that runs out of rounds and then just stands there looks
        // exactly like a game that broke
        Show(overPanel, "BITTI", "Butun raundlari gectin");
    }

    private void Show(GameObject panel, string title, string detail)
    {
        if (panel == null || panel.activeSelf)
            return;

        if (overTitle != null)
            overTitle.text = title;

        if (overDetail != null)
        {
            string money = CurrencyManager.instance == null
                ? ""
                : "\nKazanc: " + CurrencyManager.instance.Currency;

            overDetail.text = detail + money;
        }

        Switch(roundPanel, false);
        Switch(panel, true);

        Blocking = true;
        Time.timeScale = 0f;
    }

    // Reloading rather than putting everything back by hand. Half a dozen
    // singletons, a customer pool, a round coroutine and a navmesh full of
    // carved holes -- a scene load resets all of it and cannot forget one
    private void Restart()
    {
        Blocking = false;

        // Before the load, not after. A scene that loads while time is stopped
        // comes up stopped, and nothing in the new scene knows to start it
        Time.timeScale = 1f;

        Unbind();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---- the banner ---------------------------------------------------------

    private IEnumerator Popping(Transform target, bool inwards)
    {
        float span = BannerTime;
        float age = 0f;

        while (age < span)
        {
            // Unscaled: the banner has to play while the game underneath it is
            // paused, which is the whole reason it is there
            age += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(age / span);

            target.localScale = Vector3.one * (inwards ? Overshoot(t) : 1f - t);

            yield return null;
        }

        target.localScale = inwards ? Vector3.one : Vector3.zero;

        if (!inwards)
            target.gameObject.SetActive(false);

        banner = null;
    }

    // Past the end and back, so it lands rather than stops
    private static float Overshoot(float t)
    {
        const float pull = 1.7f;
        float back = t - 1f;

        return back * back * ((pull + 1f) * back + pull) + 1f;
    }

    private static void Switch(GameObject target, bool on)
    {
        if (target != null && target.activeSelf != on)
            target.SetActive(on);
    }
}
