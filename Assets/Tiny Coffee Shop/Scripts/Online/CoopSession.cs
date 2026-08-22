// Everything in this file needs three packages that are not in the project by
// default. The define is switched on and off for you by OnlineDefines.cs the
// moment they resolve -- there is nothing to tick by hand.
//
// Cooked Fast > Online > 1 - Paketleri Kur
#if COOP_ONLINE
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Multiplayer;
using UnityEngine;

// The half of co-op that actually talks to Unity's servers.
//
// Sign in, ask for a two seat private room, hand back the code. The guest does
// the same three steps with the code typed in. Everything the menu sees goes
// through Coop, so the menu never touches a Unity Services type and keeps
// working when this file is compiled out.
public class CoopSession : MonoBehaviour, ICoopBackend
{
    // Which set of Unity Dashboard settings this build talks to. Every UGS
    // project starts with one called production; a separate development one is
    // worth making before the game is out, so test traffic and test bans do not
    // land on players
    private const string environment = "production";

    private const int seats = 2;

    private static CoopSession instance;

    private ISession session;
    private NetworkManager net;
    private bool listening;

    // Installed rather than dragged into a scene.
    //
    // Co-op has to be reachable from the menu, and the menu is in the kitchen
    // scene along with everything else -- so anything that needed placing by
    // hand would have to be placed again in every scene anybody ever adds. This
    // costs one empty GameObject in single player and nothing else: no session
    // is created until a button is pressed
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject holder = new GameObject("Coop Session");

        DontDestroyOnLoad(holder);

        instance = holder.AddComponent<CoopSession>();

        Coop.Use(instance);
    }

    // ---- the three buttons --------------------------------------------------

    public async void Host()
    {
        try
        {
            if (!await Ready())
                return;

            Coop.Report(CoopPhase.Hosting);

            SessionOptions options = new SessionOptions
            {
                MaxPlayers = seats,
                IsPrivate = true,
                Name = "Cooked Fast",
            }.WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            // The session API starts the NGO host for us as part of
            // WithRelayNetwork -- there is no StartHost call to make here, and
            // making one would try to start a second one
            Listen();

            Coop.Code(session.Code);
            Coop.Report(CoopPhase.Waiting);
            Count();
        }
        catch (Exception error)
        {
            Give("Oda kurulamadi", error);
        }
    }

    public async void Join(string code)
    {
        try
        {
            if (!await Ready())
                return;

            Coop.Report(CoopPhase.Joining);

            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            Listen();

            Coop.Code(code);
            Coop.Report(CoopPhase.InGame);
            Count();
        }
        catch (Exception error)
        {
            Give("Odaya girilemedi", error);
        }
    }

    public async void Leave()
    {
        // Read and cleared before the await, not after. Leaving is the one
        // thing a player does twice when the first press looks like it did
        // nothing, and two LeaveAsync calls on one session is an exception on
        // the way out of a room that is already gone
        ISession leaving = session;

        session = null;

        Deafen();

        Coop.Report(CoopPhase.Offline);

        try
        {
            if (leaving != null)
                await leaving.LeaveAsync();
        }
        catch (Exception error)
        {
            // Logged, not shown. The room is already behind us
            Debug.LogWarning("Coop: cikarken hata -- " + error.Message);
        }

        Stop();
    }

    // ---- getting to the point where a room can be asked for -----------------

    private async Task<bool> Ready()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Coop.Fail("Internet baglantisi yok.");

            return false;
        }

        Coop.Report(CoopPhase.SigningIn);

        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions options = new InitializationOptions();

            options.SetEnvironmentName(environment);

            await UnityServices.InitializeAsync(options);
        }

        // Anonymous: no form, no password, no e-mail. The account lives in the
        // install -- delete the app and it is gone. Fine for a co-op room code,
        // not fine for anything the player would be upset to lose, which is why
        // money and hats still live in the local save
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        // Made before the session rather than after. The session API starts NGO
        // itself the moment the room exists, and starting NGO without a
        // NetworkManager in the scene is a null reference somewhere inside the
        // package where the message will not mention Cooked Fast at all
        net = CoopBootstrap.Ensure();

        return true;
    }

    // ---- who is in the room -------------------------------------------------

    private void Listen()
    {
        if (listening || net == null)
            return;

        listening = true;

        net.OnClientConnectedCallback += Arrived;
        net.OnClientDisconnectCallback += Went;
        net.OnClientStopped += Stopped;
    }

    private void Deafen()
    {
        if (!listening || net == null)
        {
            listening = false;

            return;
        }

        listening = false;

        net.OnClientConnectedCallback -= Arrived;
        net.OnClientDisconnectCallback -= Went;
        net.OnClientStopped -= Stopped;
    }

    private void Arrived(ulong client)
    {
        Count();

        if (Coop.Phase == CoopPhase.Waiting && Coop.Players >= seats)
            Coop.Report(CoopPhase.InGame);
    }

    private void Went(ulong client)
    {
        Count();

        // The host is still hosting with an empty room; the guest has been
        // thrown all the way out. Told apart by who we are, because the same
        // callback fires on both sides for entirely different reasons
        if (Coop.IsHost)
        {
            if (Coop.Connected)
                Coop.Report(CoopPhase.Waiting);

            return;
        }

        Coop.Fail("Baglanti koptu.");
    }

    // Fires on the guest when the host closes the room, quits, or drops off the
    // network. There is no host migration in this build on purpose: picking a
    // new host means moving every cooking timer, every customer and every order
    // to another phone mid-round, and getting that wrong is worse than a clean
    // "the host left" screen
    private void Stopped(bool wasHost)
    {
        Deafen();

        session = null;

        if (wasHost)
        {
            Coop.Report(CoopPhase.Offline);

            return;
        }

        Coop.Fail("Odayi kuran oyuncu ayrildi.");
    }

    private void Count()
    {
        if (net == null)
        {
            Coop.Count(0);

            return;
        }

        // The host can see the whole room. The guest only ever sees itself and
        // the server it is talking to, so counting its connected list would
        // report one player in a room of two
        if (net.IsServer)
        {
            Coop.Count(net.ConnectedClientsIds.Count);

            return;
        }

        Coop.Count(net.IsConnectedClient ? seats : 1);
    }

    private void Stop()
    {
        if (net != null && (net.IsClient || net.IsServer))
            net.Shutdown();
    }

    // ---- when it goes wrong -------------------------------------------------

    private void Give(string what, Exception error)
    {
        // The exception goes to the console in full, because that is where it
        // is useful. The player gets a sentence
        Debug.LogException(error);

        session = null;

        Deafen();
        Stop();

        Coop.Fail(what + ".\n" + Friendly(error));
    }

    private static string Friendly(Exception error)
    {
        string text = error != null ? error.Message : "";
        string lower = text.ToLowerInvariant();

        if (lower.Contains("join code") || lower.Contains("joincode") ||
            lower.Contains("not found") || lower.Contains("notfound"))
            return "Bu kodla oda bulunamadi. Kodu bir daha kontrol et.";

        if (lower.Contains("full") || lower.Contains("capacity"))
            return "Oda dolu. Bu oyunda iki kisilik yer var.";

        if (lower.Contains("unauthor") || lower.Contains("sign in") ||
            lower.Contains("token"))
            return "Unity hesabina giris yapilamadi.";

        if (lower.Contains("project") || lower.Contains("environment"))
            return "Proje Unity Dashboard'a bagli degil ya da\n" +
                   "Authentication/Relay servisleri acik degil.";

        if (lower.Contains("timeout") || lower.Contains("timed out") ||
            lower.Contains("network") || lower.Contains("unreachable"))
            return "Sunucuya ulasilamadi. Baglantiyi kontrol et.";

        return text;
    }

    private void OnApplicationQuit()
    {
        // Left properly rather than dropped. A room that is not left sits there
        // until the service times it out, and the host's next attempt can come
        // back with the old room still holding the seat
        if (session != null)
            Leave();
    }
}
#endif
