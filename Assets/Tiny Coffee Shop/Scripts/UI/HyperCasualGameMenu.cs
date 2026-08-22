using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Tabsil.Sijil;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Menu behaviour only. Every visible element is supplied by Hyper_Casual_UI;
// this component deliberately does not manufacture labels, colours or panels.
public class HyperCasualGameMenu : MonoBehaviour
{
    [Header("Oyun baglantisi")]
    [SerializeField] private GameScreens screens;

    [Header("Ekranlar")]
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject settingsMenu;
    [SerializeField] private GameObject characterMenu;
    [SerializeField] private GameObject quitMenu;
    [SerializeField] private GameObject gameButtons;
    [SerializeField] private CharacterSkinPreview skinPreview;

    [Header("Ana menu")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button characterButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Pause ve oyun ici")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button quickQuitButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseSettingsButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button pauseQuitButton;

    [Header("Ayarlar")]
    [SerializeField] private Button soundButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private Button settingsOkButton;
    [SerializeField] private Image soundToggle;
    [SerializeField] private Image musicToggle;
    [SerializeField] private Sprite toggleOn;
    [SerializeField] private Sprite toggleOff;
    [SerializeField] private Slider effectsSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Dropdown qualityDropdown;
    [SerializeField] private Dropdown languageDropdown;

    [Header("Karakter")]
    [SerializeField] private Button chooseCharacterButton;
    [SerializeField] private Button characterBackButton;

    [Header("Cikis")]
    [SerializeField] private Button confirmQuitButton;
    [SerializeField] private Button cancelQuitButton;

    private GameObject returnPanel;
    private bool gameStarted;
    private Coroutine opening;
    private Transform animatedPanel;
    private RectTransform animatedRect;
    private Vector2 animatedRectHome;
    private CanvasGroup animatedGroup;
    private Image animatedDim;
    private Color animatedDimHome;

    private void Awake()
    {
        GameLocalization.Initialize();
        MatchMainButtonSizes();

        Bind(playButton, Play);
        Bind(characterButton, OpenCharacters);
        Bind(settingsButton, OpenSettingsFromMain);
        Bind(quitButton, OpenQuitFromMain);

        Bind(pauseButton, Pause);
        Bind(quickQuitButton, OpenQuitFromGame);
        Bind(retryButton, Restart);
        Bind(resumeButton, Resume);
        Bind(pauseSettingsButton, OpenSettingsFromPause);
        Bind(homeButton, Home);
        Bind(pauseQuitButton, OpenQuitFromPause);

        Bind(soundButton, ToggleSound);
        Bind(musicButton, ToggleMusic);
        Bind(settingsOkButton, BackFromSettings);
        Bind(chooseCharacterButton, ChooseCharacter);
        Bind(characterBackButton, BackFromCharacters);
        Bind(confirmQuitButton, Quit);
        Bind(cancelQuitButton, CancelQuit);

        if (effectsSlider != null)
            effectsSlider.onValueChanged.AddListener(SetEffectsVolume);
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        if (languageDropdown != null)
            languageDropdown.onValueChanged.AddListener(SetLanguage);

        PrepareSettings();
        RefreshToggles();
        SoundManager.SetLobby(true);
        ShowOnly(mainMenu);
        Switch(gameButtons, false);
        Time.timeScale = 0f;

        // The station/upgrade/customer UI is instantiated at different times.
        // Unscaled polling also works while the front menu has paused time.
        StartCoroutine(LocalizationWatch());
    }

    private void MatchMainButtonSizes()
    {
        if (characterButton == null || settingsButton == null)
            return;

        RectTransform shopRect = (RectTransform)characterButton.transform;
        RectTransform settingsRect = (RectTransform)settingsButton.transform;
        settingsRect.sizeDelta = shopRect.sizeDelta;

        Image shopImage = characterButton.GetComponent<Image>();
        Image settingsImage = settingsButton.GetComponent<Image>();

        if (shopImage != null)
            shopImage.preserveAspect = false;
        if (settingsImage != null)
            settingsImage.preserveAspect = false;
    }

    private IEnumerator LocalizationWatch()
    {
        yield return null;

        while (true)
        {
            GameLocalization.RefreshAll();
            yield return new WaitForSecondsRealtime(.4f);
        }
    }

    private void Update()
    {
        if (gameStarted && GameScreens.Blocking &&
            !IsOpen(pauseMenu) && !IsOpen(settingsMenu) && !IsOpen(quitMenu))
        {
            Switch(gameButtons, false);
        }

        if (!BackPressed())
            return;

        if (IsOpen(quitMenu))
            CancelQuit();
        else if (IsOpen(settingsMenu))
            BackFromSettings();
        else if (IsOpen(characterMenu))
            BackFromCharacters();
        else if (IsOpen(pauseMenu))
            Resume();
        else if (gameStarted)
            Pause();
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }

    private void Play()
    {
        if (skinPreview != null)
            skinPreview.ApplyToPlayer();

        SoundManager.SetLobby(false);
        gameStarted = true;
        ShowOnly(null);
        Switch(gameButtons, true);

        if (screens != null)
            screens.StartGameFromMenu();
        else
            Time.timeScale = 1f;
    }

    private void Pause()
    {
        if (!gameStarted)
            return;

        Time.timeScale = 0f;
        Switch(gameButtons, false);
        Open(pauseMenu);
    }

    private void Resume()
    {
        if (IsOpen(pauseMenu))
        {
            ClosePanel(pauseMenu, null, FinishResume);
            return;
        }

        FinishResume();
    }

    private void FinishResume()
    {
        ShowOnly(null);
        Switch(gameButtons, true);
        Time.timeScale = 1f;
    }

    private void Home()
    {
        SaveCurrentGame();
        gameStarted = false;
        returnPanel = mainMenu;

        SoundManager.SetLobby(true);
        Switch(gameButtons, false);

        if (screens != null)
            screens.ReturnToMainMenu();
        else
            Time.timeScale = 0f;

        // Nothing in the kitchen is destroyed or recreated. Customers,
        // stations, carried food, round coroutine and player position remain
        // exactly where they were, frozen behind the full-screen main menu.
        StableMain();

        if (IsOpen(pauseMenu))
            ClosePanel(pauseMenu, mainMenu, null);
        else
            ShowOnly(mainMenu);
    }

    private static void SaveCurrentGame()
    {
        // Sijil normally saves each system when it changes. Home is an explicit
        // checkpoint as well: flush every saveable now so player position and
        // upgrades do not depend on their next periodic save.
        if (Sijil.instance != null)
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IWantToBeSaved saveable)
                {
                    try
                    {
                        saveable.Save();
                    }
                    catch (System.Exception exception)
                    {
                        // One optional upgrade panel must not be able to trap
                        // the player in Pause. Keep saving the other systems
                        // and still open Home, while leaving exact evidence.
                        Debug.LogException(exception, behaviours[i]);
                    }
                }
            }
        }

        PlayerPrefs.Save();
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OpenSettingsFromMain()
    {
        returnPanel = mainMenu;
        RefreshToggles();
        Open(settingsMenu);
    }

    private void OpenSettingsFromPause()
    {
        returnPanel = pauseMenu;
        RefreshToggles();
        Open(settingsMenu);
    }

    private void BackFromSettings()
    {
        ClosePanel(settingsMenu, returnPanel != null ? returnPanel : mainMenu, null);
    }

    private void ToggleSound()
    {
        SoundManager.SetEffectsLevel(SoundManager.EffectsLevel > .01f ? 0f : 1f);
        RefreshToggles();
    }

    private void ToggleMusic()
    {
        SoundManager.SetMusicLevel(SoundManager.MusicLevel > .01f ? 0f : 1f);
        RefreshToggles();
    }

    private void RefreshToggles()
    {
        if (soundToggle != null)
            soundToggle.sprite = SoundManager.EffectsLevel > .01f ? toggleOn : toggleOff;

        if (musicToggle != null)
            musicToggle.sprite = SoundManager.MusicLevel > .01f ? toggleOn : toggleOff;

        if (effectsSlider != null)
            effectsSlider.SetValueWithoutNotify(SoundManager.EffectsLevel);
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(SoundManager.MusicLevel);
    }

    private void PrepareSettings()
    {
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(
                QualitySettings.GetQualityLevel(), 0,
                Mathf.Max(0, QualitySettings.names.Length - 1)));
            qualityDropdown.RefreshShownValue();
        }

        if (languageDropdown != null)
        {
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string>(
                GameLocalization.LanguageNames));
            languageDropdown.SetValueWithoutNotify(GameLocalization.CurrentIndex);
            languageDropdown.RefreshShownValue();
        }
    }

    private static void SetEffectsVolume(float value)
    {
        SoundManager.SetEffectsLevel(value);
    }

    private static void SetMusicVolume(float value)
    {
        SoundManager.SetMusicLevel(value);
    }

    private static void SetQuality(int index)
    {
        if (QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(Mathf.Clamp(index, 0,
                QualitySettings.names.Length - 1), true);
    }

    private static void SetLanguage(int index)
    {
        GameLocalization.SetLanguage(index);
    }

    private void OpenCharacters()
    {
        returnPanel = mainMenu;
        Open(characterMenu);
    }

    private void BackFromCharacters()
    {
        ClosePanel(characterMenu, mainMenu, null);
    }

    private void ChooseCharacter()
    {
        if (skinPreview != null)
            skinPreview.ConfirmSelection();
        StartPanelPop(characterMenu);
    }

    private void OpenQuitFromMain()
    {
        returnPanel = mainMenu;
        OpenQuitOverlay();
    }

    private void OpenQuitFromPause()
    {
        returnPanel = pauseMenu;
        OpenQuitOverlay();
    }

    private void OpenQuitFromGame()
    {
        Time.timeScale = 0f;
        Switch(gameButtons, false);
        returnPanel = null;
        OpenQuitOverlay();
    }

    private void OpenQuitOverlay()
    {
        ShowOnly(returnPanel);
        Switch(quitMenu, true);
        StartPanelPop(quitMenu);
    }

    private void CancelQuit()
    {
        if (returnPanel != null)
        {
            ClosePanel(quitMenu, returnPanel, null);
            return;
        }

        ClosePanel(quitMenu, null, FinishResume);
    }

    private void Quit()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Open(GameObject panel)
    {
        if (panel == mainMenu)
        {
            StableMain();
            ShowOnly(mainMenu);
            return;
        }

        Switch(panel, true);
        panel.transform.SetAsLastSibling();
        StartPanelPop(panel);
    }

    private void StartPanelPop(GameObject panel)
    {
        if (panel == null)
            return;

        StopPanelAnimation();

        if (panel == pauseMenu)
        {
            opening = StartCoroutine(OpenPause(panel));
            return;
        }

        if (IsPage(panel))
        {
            opening = StartCoroutine(SlidePageIn(panel));
            return;
        }

        animatedPanel = panel.transform;
        opening = StartCoroutine(Pop(panel.transform));
    }

    private IEnumerator OpenPause(GameObject panel)
    {
        Transform card = PauseCard(panel);
        Image dim = PauseDim(panel);
        Color dimColour = dim != null ? dim.color : Color.clear;

        // Pause is a full-screen modal made from two independent pieces. The
        // backdrop fades over the live kitchen; only the actual card pops.
        // Scaling the root also scales the backdrop, exposing the corners and
        // making the blur look like another panel.
        panel.transform.localScale = Vector3.one;
        animatedPanel = card;
        animatedDim = dim;
        animatedDimHome = dimColour;

        if (card != null)
            card.localScale = Vector3.one * .86f;

        if (dim != null)
        {
            Color clear = dimColour;
            clear.a = 0f;
            dim.color = clear;
        }

        float age = 0f;
        const float duration = .16f;

        while (age < duration)
        {
            age += Time.unscaledDeltaTime;
            float amount = Mathf.Clamp01(age / duration);

            if (card != null)
            {
                float t = amount - 1f;
                float overshoot = t * t * (2.2f * t + 1.2f) + 1f;
                card.localScale = Vector3.one * overshoot;
            }

            if (dim != null)
            {
                Color colour = dimColour;
                colour.a *= amount * amount * (3f - 2f * amount);
                dim.color = colour;
            }

            yield return null;
        }

        if (card != null)
            card.localScale = Vector3.one;
        if (dim != null)
            dim.color = dimColour;

        opening = null;
        animatedPanel = null;
        animatedDim = null;
    }

    private IEnumerator SlidePageIn(GameObject panel)
    {
        RectTransform rect = (RectTransform)panel.transform;
        CanvasGroup group = PageGroup(panel);
        Vector2 home = rect.anchoredPosition;
        Vector2 outside = home + Vector2.right * 90f;

        animatedPanel = rect;
        animatedRect = rect;
        animatedRectHome = home;
        animatedGroup = group;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = outside;
        group.alpha = 0f;

        float age = 0f;
        const float duration = .18f;

        while (age < duration)
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.LerpUnclamped(outside, home, eased);
            group.alpha = t;
            yield return null;
        }

        rect.anchoredPosition = home;
        group.alpha = 1f;
        opening = null;
        animatedPanel = null;
        animatedRect = null;
        animatedGroup = null;
    }

    private IEnumerator Pop(Transform target)
    {
        target.localScale = Vector3.one * .86f;
        float age = 0f;
        const float duration = .16f;

        while (age < duration)
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / duration) - 1f;
            float overshoot = t * t * (2.2f * t + 1.2f) + 1f;
            target.localScale = Vector3.one * overshoot;
            yield return null;
        }

        target.localScale = Vector3.one;
        opening = null;
        animatedPanel = null;
    }

    private void ClosePanel(GameObject panel, GameObject reveal,
        System.Action finished)
    {
        if (panel == null || !panel.activeSelf)
        {
            if (reveal != null)
                Switch(reveal, true);

            if (reveal == mainMenu)
                StableMain();

            finished?.Invoke();
            return;
        }

        if (reveal != null)
            Switch(reveal, true);

        if (reveal == mainMenu)
            StableMain();

        // The page being dismissed remains above the stable destination until
        // its closing motion ends. Otherwise the destination covers the close
        // and it looks like another instant cut.
        panel.transform.SetAsLastSibling();
        StopPanelAnimation();

        if (panel == pauseMenu)
        {
            opening = StartCoroutine(ClosePause(panel, finished));
            return;
        }

        if (IsPage(panel))
        {
            opening = StartCoroutine(ClosePage(panel, finished));
            return;
        }

        animatedPanel = panel.transform;
        opening = StartCoroutine(Close(panel, finished));
    }

    private IEnumerator ClosePause(GameObject panel, System.Action finished)
    {
        Transform card = PauseCard(panel);
        Image dim = PauseDim(panel);
        Color dimColour = dim != null ? dim.color : Color.clear;

        panel.transform.localScale = Vector3.one;
        animatedPanel = card;
        animatedDim = dim;
        animatedDimHome = dimColour;

        float age = 0f;
        const float duration = .13f;

        while (age < duration)
        {
            age += Time.unscaledDeltaTime;
            float amount = Mathf.Clamp01(age / duration);
            float eased = amount * amount * (3f - 2f * amount);

            if (card != null)
                card.localScale = Vector3.one * Mathf.Lerp(1f, .86f, eased);

            if (dim != null)
            {
                Color colour = dimColour;
                colour.a *= 1f - eased;
                dim.color = colour;
            }

            yield return null;
        }

        if (card != null)
            card.localScale = Vector3.one;
        if (dim != null)
            dim.color = dimColour;

        panel.SetActive(false);
        opening = null;
        animatedPanel = null;
        animatedDim = null;
        finished?.Invoke();
    }

    private IEnumerator ClosePage(GameObject panel, System.Action finished)
    {
        RectTransform rect = (RectTransform)panel.transform;
        CanvasGroup group = PageGroup(panel);
        Vector2 home = rect.anchoredPosition;
        Vector2 outside = home + Vector2.right * 120f;

        animatedPanel = rect;
        animatedRect = rect;
        animatedRectHome = home;
        animatedGroup = group;
        rect.localScale = Vector3.one;
        group.alpha = 1f;

        float age = 0f;
        const float duration = .17f;

        while (age < duration)
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / duration);
            float eased = t * t * (3f - 2f * t);
            rect.anchoredPosition = Vector2.LerpUnclamped(home, outside, eased);
            group.alpha = 1f - eased;
            yield return null;
        }

        rect.anchoredPosition = home;
        group.alpha = 1f;
        panel.SetActive(false);
        opening = null;
        animatedPanel = null;
        animatedRect = null;
        animatedGroup = null;
        finished?.Invoke();
    }

    private IEnumerator Close(GameObject panel, System.Action finished)
    {
        Transform target = panel.transform;
        float age = 0f;
        const float duration = .13f;

        while (age < duration)
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / duration);
            float eased = t * t * (3f - 2f * t);
            target.localScale = Vector3.one * Mathf.Lerp(1f, .86f, eased);
            yield return null;
        }

        target.localScale = Vector3.one;
        panel.SetActive(false);
        opening = null;
        animatedPanel = null;
        finished?.Invoke();
    }

    private void StopPanelAnimation()
    {
        if (opening != null)
            StopCoroutine(opening);

        if (animatedPanel != null)
            animatedPanel.localScale = Vector3.one;

        if (animatedRect != null)
            animatedRect.anchoredPosition = animatedRectHome;

        if (animatedGroup != null)
            animatedGroup.alpha = 1f;

        if (animatedDim != null)
            animatedDim.color = animatedDimHome;

        opening = null;
        animatedPanel = null;
        animatedRect = null;
        animatedGroup = null;
        animatedDim = null;
    }

    private bool IsPage(GameObject panel)
    {
        return panel != null && (panel == settingsMenu || panel == characterMenu);
    }

    private static CanvasGroup PageGroup(GameObject panel)
    {
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        return group != null ? group : panel.AddComponent<CanvasGroup>();
    }

    private static Transform PauseCard(GameObject panel)
    {
        return panel != null ? panel.transform.Find("Hyper Casual Pause") : null;
    }

    private static Image PauseDim(GameObject panel)
    {
        Transform dim = panel != null ? panel.transform.Find("Dim Behind") : null;
        return dim != null ? dim.GetComponent<Image>() : null;
    }

    private void StableMain()
    {
        if (mainMenu != null)
            mainMenu.transform.localScale = Vector3.one;
    }

    private void ShowOnly(GameObject panel)
    {
        Switch(mainMenu, panel == mainMenu);
        Switch(pauseMenu, panel == pauseMenu);
        Switch(settingsMenu, panel == settingsMenu);
        Switch(characterMenu, panel == characterMenu);
        Switch(quitMenu, panel == quitMenu);
    }

    private static void Switch(GameObject target, bool on)
    {
        if (target != null && target.activeSelf != on)
            target.SetActive(on);
    }

    private static bool IsOpen(GameObject target)
    {
        return target != null && target.activeSelf;
    }

    private static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }
}
