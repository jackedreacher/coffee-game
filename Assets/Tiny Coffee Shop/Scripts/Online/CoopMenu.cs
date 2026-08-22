using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The five online screens, and which one is up.
//
// It reads Coop and nothing else -- no Relay type, no Netcode type, not even a
// using for them. That is what lets the menu be built, opened and looked at
// before a single multiplayer package is installed: with them missing every
// button still works, and says so.
public class CoopMenu : MonoBehaviour
{
    [Header(" Ekranlar ")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject busyPanel;
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private GameObject gamePanel;

    [Header(" Secim ")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button openJoinButton;
    [SerializeField] private Button backButton;

    [Header(" Oda kuran ")]
    [SerializeField] private TextMeshProUGUI codeLabel;
    [SerializeField] private TextMeshProUGUI playersLabel;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button cancelHostButton;

    [Header(" Odaya giren ")]
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button pasteButton;
    [SerializeField] private Button cancelJoinButton;

    [Header(" Bekleme ve hata ")]
    [SerializeField] private TextMeshProUGUI busyLabel;
    [SerializeField] private TextMeshProUGUI errorLabel;
    [SerializeField] private Button errorOkButton;

    [Header(" Oyun icinde ")]
    [SerializeField] private Button leaveButton;

    // Only true between pressing KODLA KATIL and either joining or backing out.
    // Coop has no phase for "typing a code" because nothing is happening on the
    // network while somebody types, and a phase nobody transmits is a phase
    // that belongs to the menu
    private bool typing;

    private void Awake()
    {
        Bind(hostButton, StartHosting);
        Bind(openJoinButton, OpenJoin);
        Bind(backButton, Back);

        Bind(copyButton, Copy);
        Bind(cancelHostButton, Quit);

        Bind(joinButton, StartJoining);
        Bind(pasteButton, Paste);
        Bind(cancelJoinButton, CloseJoin);

        Bind(errorOkButton, Dismiss);
        Bind(leaveButton, Quit);
    }

    private void OnEnable()
    {
        Coop.Changed += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        Coop.Changed -= Refresh;
    }

    // ---- buttons ------------------------------------------------------------

    private void StartHosting()
    {
        typing = false;

        Coop.Host();

        Refresh();
    }

    private void OpenJoin()
    {
        typing = true;

        // Prefilled from the clipboard when it looks like a code was just
        // copied. The two players are on a call or in a chat window while they
        // do this, and the code arrives in the guest's clipboard far more often
        // than it arrives in their memory
        if (codeInput != null && string.IsNullOrEmpty(codeInput.text))
        {
            string pasted = Coop.Tidy(GUIUtility.systemCopyBuffer);

            if (Looks(pasted))
                codeInput.text = pasted;
        }

        Refresh();

        if (codeInput != null)
            codeInput.Select();
    }

    private void CloseJoin()
    {
        typing = false;

        Refresh();
    }

    private void StartJoining()
    {
        if (codeInput == null)
            return;

        typing = false;

        Coop.Join(codeInput.text);

        Refresh();
    }

    private void Paste()
    {
        if (codeInput == null)
            return;

        codeInput.text = Coop.Tidy(GUIUtility.systemCopyBuffer);

        Refresh();
    }

    private void Copy()
    {
        if (string.IsNullOrEmpty(Coop.JoinCode))
            return;

        GUIUtility.systemCopyBuffer = Coop.JoinCode;

        // Said out loud rather than assumed. A copy button that looks identical
        // before and after being pressed gets pressed four more times
        if (playersLabel != null)
            playersLabel.text = "Kod kopyalandi";
    }

    private void Quit()
    {
        typing = false;

        Coop.Leave();

        Refresh();
    }

    private void Dismiss()
    {
        typing = false;

        Coop.Report(CoopPhase.Offline);

        Refresh();
    }

    private void Back()
    {
        typing = false;

        gameObject.SetActive(false);
    }

    // ---- which screen is up -------------------------------------------------

    private void Refresh()
    {
        CoopPhase phase = Coop.Phase;

        bool busy = Coop.Busy;
        bool error = phase == CoopPhase.Error;
        bool hosting = Coop.IsHost && phase == CoopPhase.Waiting;
        bool playing = phase == CoopPhase.InGame;

        Switch(busyPanel, busy);
        Switch(errorPanel, error);
        Switch(hostPanel, hosting);
        Switch(joinPanel, typing && !busy && !error);
        Switch(gamePanel, playing);
        Switch(choicePanel, !busy && !error && !hosting && !playing && !typing);

        if (busyLabel != null)
            busyLabel.text = phase == CoopPhase.Joining
                ? "Odaya giriliyor..."
                : phase == CoopPhase.Hosting
                    ? "Oda kuruluyor..."
                    : "Baglaniliyor...";

        if (errorLabel != null)
            errorLabel.text = Coop.Error;

        if (codeLabel != null)
            codeLabel.text = string.IsNullOrEmpty(Coop.JoinCode) ? "..." : Coop.JoinCode;

        if (playersLabel != null && hosting)
            playersLabel.text = Coop.Players + "/2 oyuncu -- ikinci oyuncu bekleniyor";

        // Greyed rather than hidden. A missing button reads as a missing
        // feature; a dead one with the reason underneath reads as a step that
        // has not been done yet
        if (hostButton != null)
            hostButton.interactable = !busy;

        if (openJoinButton != null)
            openJoinButton.interactable = !busy;

        if (joinButton != null)
            joinButton.interactable = !busy && codeInput != null &&
                                      Looks(Coop.Tidy(codeInput.text));
    }

    private static bool Looks(string code)
    {
        return !string.IsNullOrEmpty(code) && code.Length >= 4 && code.Length <= 16;
    }

    private static void Switch(GameObject panel, bool on)
    {
        if (panel != null && panel.activeSelf != on)
            panel.SetActive(on);
    }

    private void Bind(Button button, UnityEngine.Events.UnityAction call)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(call);
        button.onClick.AddListener(call);
    }

    // The join button turns itself on as the code becomes long enough, which
    // needs somebody to notice the typing. An input field event would do it in
    // fewer frames, but it would also have to survive the field being rebuilt
    // by the setup command every time the panel is regenerated
    private void Update()
    {
        if (typing && joinButton != null && codeInput != null)
            joinButton.interactable = !Coop.Busy && Looks(Coop.Tidy(codeInput.text));
    }
}
