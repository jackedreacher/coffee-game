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

    [Tooltip("Raund girisinde kayip pop yapacak kart. Bos ise Round/Bar bulunur")]
    [SerializeField] private RectTransform roundCard;

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

    private float BannerTime => bannerTime > .01f
        ? Mathf.Max(.38f, bannerTime)
        : .42f;

    private Lives lives;
    private Coroutine banner;
    private int lastRound = -1;
    private FinishState finishState;
    private Vector2 roundCardHome;
    private Vector3 roundCardScale = Vector3.one;
    private bool roundCardCaptured;

    private enum FinishState
    {
        None,
        Died,
        Won,
    }

    private void Awake()
    {
        // Set in Awake, not Start. RoundManager reads it in the first frame of
        // its coroutine and Start order between the two is not something to bet
        // a hung game on
        Blocking = startPanel != null;

        Switch(startPanel, startPanel != null);
        Switch(roundPanel, false);
        Switch(overPanel, false);

        CaptureRoundCard();

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
        GameLocalization.LanguageChanged += RefreshLanguage;
    }

    private void OnDisable()
    {
        RoundManager.RoundAnnounced -= Announce;
        RoundManager.RoundStarted -= Begin;
        RoundManager.AllRoundsFinished -= Won;
        GameLocalization.LanguageChanged -= RefreshLanguage;

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

    // Called by the cartoon main menu. Kept public so the menu does not need
    // to fake a click on the old start button or duplicate this state logic.
    public void StartGameFromMenu()
    {
        Play();
    }

    public void ReturnToMainMenu()
    {
        if (banner != null)
        {
            StopCoroutine(banner);
            banner = null;
        }

        Switch(roundPanel, false);
        Switch(overPanel, false);
        Switch(startPanel, true);

        Blocking = true;
        Time.timeScale = 0f;
    }

    private void Announce(int round)
    {
        if (roundPanel == null)
            return;

        lastRound = round;
        RefreshRoundText();

        Switch(roundPanel, true);

        if (banner != null)
            StopCoroutine(banner);

        SoundManager.Play(SoundManager.Sound.RoundIntro);
        banner = StartCoroutine(Popping(true));
    }

    private void Begin(int round)
    {
        if (roundPanel == null || !roundPanel.activeSelf)
            return;

        if (banner != null)
            StopCoroutine(banner);

        banner = StartCoroutine(Popping(false));
    }

    private void Died()
    {
        Show(overPanel, FinishState.Died);
    }

    private void Won()
    {
        // A game that runs out of rounds and then just stands there looks
        // exactly like a game that broke
        Show(overPanel, FinishState.Won);
    }

    private void Show(GameObject panel, FinishState result)
    {
        if (panel == null || panel.activeSelf)
            return;

        finishState = result;
        RefreshFinishText();

        Switch(roundPanel, false);
        Switch(panel, true);

        Blocking = true;
        Time.timeScale = 0f;
    }

    private void RefreshLanguage()
    {
        RefreshRoundText();
        RefreshFinishText();
    }

    private void RefreshRoundText()
    {
        if (readyLabel != null)
            readyLabel.text = GameLocalization.Get("get_ready", "GET READY");

        if (roundLabel != null && lastRound >= 0)
            roundLabel.text = GameLocalization.Format(
                "round", "ROUND {0}", lastRound);
    }

    private void RefreshFinishText()
    {
        if (finishState == FinishState.None)
            return;

        bool won = finishState == FinishState.Won;

        if (overTitle != null)
            overTitle.text = won
                ? GameLocalization.Get("finished", "COMPLETE")
                : GameLocalization.Get("game_over", "GAME OVER");

        if (overDetail == null)
            return;

        string detail = won
            ? GameLocalization.Get("all_rounds", "You completed every round")
            : GameLocalization.Get("out_of_lives", "You ran out of lives");

        if (CurrencyManager.instance != null)
        {
            detail += "\n" + GameLocalization.Format(
                "earnings", "Earnings: {0}",
                CurrencyManager.instance.Currency);
        }

        overDetail.text = detail;
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

    private IEnumerator Popping(bool inwards)
    {
        RectTransform card = CaptureRoundCard();
        CanvasGroup group = roundPanel.GetComponent<CanvasGroup>();

        if (group == null)
            group = roundPanel.AddComponent<CanvasGroup>();

        float span = BannerTime;
        float age = 0f;

        Vector2 startPosition = inwards
            ? roundCardHome + Vector2.up * 210f
            : card != null ? card.anchoredPosition : roundCardHome;
        Vector2 endPosition = inwards
            ? roundCardHome
            : roundCardHome + Vector2.up * 90f;
        Vector3 startScale = inwards
            ? roundCardScale * .72f
            : card != null ? card.localScale : roundCardScale;
        Vector3 endScale = inwards
            ? roundCardScale
            : roundCardScale * .86f;
        float startAlpha = inwards ? 0f : group.alpha;
        float endAlpha = inwards ? 1f : 0f;

        if (card != null && inwards)
        {
            card.anchoredPosition = startPosition;
            card.localScale = startScale;
            card.localRotation = Quaternion.Euler(0f, 0f, -4f);
        }

        group.alpha = startAlpha;

        while (age < span)
        {
            // Unscaled: the banner has to play while the game underneath it is
            // paused, which is the whole reason it is there
            age += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(age / span);

            // Position and opacity ease cleanly; scale overshoots only on the
            // entrance, giving the title a readable landing instead of a plain
            // resize. The exit stays restrained and gets out of gameplay's way.
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (card != null)
            {
                card.anchoredPosition = Vector2.LerpUnclamped(
                    startPosition, endPosition, eased);
                card.localScale = inwards
                    ? roundCardScale * Mathf.LerpUnclamped(.72f, 1f, Overshoot(t))
                    : Vector3.Lerp(startScale, endScale, eased);
                card.localRotation = Quaternion.Euler(0f, 0f,
                    inwards ? Mathf.Lerp(-4f, 0f, eased) : 0f);
            }

            group.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);

            yield return null;
        }

        if (card != null)
        {
            card.anchoredPosition = roundCardHome;
            card.localScale = roundCardScale;
            card.localRotation = Quaternion.identity;
        }

        group.alpha = 1f;

        if (!inwards)
            roundPanel.SetActive(false);

        banner = null;
    }

    private RectTransform CaptureRoundCard()
    {
        if (roundCard == null && roundPanel != null)
        {
            Transform bar = roundPanel.transform.Find("Bar");
            roundCard = bar as RectTransform;

            if (roundCard == null)
                roundCard = roundPanel.transform as RectTransform;
        }

        if (roundCard != null && !roundCardCaptured)
        {
            roundCardCaptured = true;
            roundCardHome = roundCard.anchoredPosition;
            roundCardScale = roundCard.localScale;
        }

        return roundCard;
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
