using System;
using UnityEngine;

// Where the two players are with each other, and nothing at all about how they
// got there.
//
// Split from the code that actually dials out on purpose. That code cannot
// exist until three Unity packages are installed, and a menu whose buttons will
// not compile until then is a menu that cannot be built, tested or even opened
// -- one missing package would take the whole project down with it, editor
// tools and all.
//
// So this half always compiles, knows nothing about Relay, and answers "online
// is not installed" honestly. The half that does the talking registers itself
// here when it is there.
public enum CoopPhase
{
    // Single player. Not an error -- this is the state the game ships in
    Offline,

    SigningIn,
    Hosting,
    Joining,

    // Session is up, the other player has not arrived yet
    Waiting,

    // Both of them, in the kitchen
    InGame,

    Error,
}

// What a working online layer has to be able to do. Three verbs, because the
// menu only has three buttons
public interface ICoopBackend
{
    void Host();
    void Join(string code);
    void Leave();
}

public static class Coop
{
    public static CoopPhase Phase { get; private set; } = CoopPhase.Offline;

    // What the guest types in. Shown as it came back from the service, in
    // capitals, because that is how it will be read out loud over a phone
    public static string JoinCode { get; private set; } = "";

    // Turkish, and meant for a player rather than for a log. The exception
    // text goes to the console; this is what goes on screen
    public static string Error { get; private set; } = "";

    public static int Players { get; private set; }
    public static bool IsHost { get; private set; }

    // The other player, as the menu needs to draw them: are they there, and
    // which animal did they pick.
    //
    // An index rather than a name or a prefab. Both builds carry the same
    // wardrobe list in the same order, so the index IS the character -- and it
    // costs four bytes instead of a string that would have to be spelled the
    // same way on two phones to mean the same thing
    public static bool MateHere { get; private set; }
    public static int MateSkin { get; private set; } = -1;

    // Whether the packages are in and the online code compiled. False means
    // every button below will politely refuse
    public static bool Installed => backend != null;

    // Halfway through something. The buttons go dead while this is true so a
    // second press cannot start a second session
    public static bool Busy => Phase == CoopPhase.SigningIn ||
                               Phase == CoopPhase.Hosting ||
                               Phase == CoopPhase.Joining;

    public static bool Connected => Phase == CoopPhase.Waiting ||
                                    Phase == CoopPhase.InGame;

    // One event for everything rather than one per field. Every listener is a
    // panel that redraws itself completely anyway
    public static event Action Changed;

    private static ICoopBackend backend;

    // Statics survive a play session and, with domain reload switched off, the
    // NEXT one too -- so a session that ended in an error would still be in
    // that error the next time Play is pressed, and the code from the last
    // round would still be on screen
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Phase = CoopPhase.Offline;
        JoinCode = "";
        Error = "";
        Players = 0;
        IsHost = false;
        MateHere = false;
        MateSkin = -1;
        backend = null;
        Changed = null;
    }

    public static void Use(ICoopBackend online)
    {
        backend = online;
    }

    // ---- what the buttons call ---------------------------------------------

    public static void Host()
    {
        if (Busy)
            return;

        if (backend == null)
        {
            Fail("Online paketleri kurulu degil.\n" +
                 "Cooked Fast > Online > 1 - Paketleri Kur");

            return;
        }

        IsHost = true;
        backend.Host();
    }

    public static void Join(string code)
    {
        if (Busy)
            return;

        if (backend == null)
        {
            Fail("Online paketleri kurulu degil.\n" +
                 "Cooked Fast > Online > 1 - Paketleri Kur");

            return;
        }

        // Trimmed and capitalised before it goes anywhere. A code read off
        // another phone arrives with a space on the end about half the time,
        // and the service would rather reject it than tidy it up
        string tidy = Tidy(code);

        if (tidy.Length < 4)
        {
            Fail("Kod cok kisa. Odayi kuran oyuncunun ekranindaki kodu yaz.");

            return;
        }

        IsHost = false;
        backend.Join(tidy);
    }

    public static void Leave()
    {
        if (backend == null)
        {
            Report(CoopPhase.Offline);

            return;
        }

        backend.Leave();
    }

    public static string Tidy(string code)
    {
        if (string.IsNullOrEmpty(code))
            return "";

        return code.Trim().Replace(" ", "").ToUpperInvariant();
    }

    // ---- what the online layer calls ---------------------------------------

    public static void Report(CoopPhase phase)
    {
        if (Phase == phase)
            return;

        Phase = phase;

        // Cleared on the way OUT of an error rather than on the way in, so the
        // message survives long enough to be read. Going offline is the player
        // pressing something, and by then they have read it
        if (phase != CoopPhase.Error)
            Error = "";

        if (phase == CoopPhase.Offline)
        {
            JoinCode = "";
            Players = 0;
            IsHost = false;
            MateHere = false;
            MateSkin = -1;
        }

        Announce();
    }

    public static void Code(string code)
    {
        JoinCode = Tidy(code);

        Announce();
    }

    public static void Count(int players)
    {
        if (Players == players)
            return;

        Players = players;

        Announce();
    }

    // Called by the badge on the other player's object, which is the only
    // thing that knows both that somebody is there and what they chose. Not
    // called on connect: being connected and having a character are two
    // different moments, a frame or two apart
    public static void Mate(int skin)
    {
        if (MateHere && MateSkin == skin)
            return;

        MateHere = true;
        MateSkin = skin;

        Announce();
    }

    public static void MateGone()
    {
        if (!MateHere)
            return;

        MateHere = false;
        MateSkin = -1;

        Announce();
    }

    public static void Fail(string message)
    {
        Error = string.IsNullOrEmpty(message) ? "Bilinmeyen hata" : message;
        Phase = CoopPhase.Error;

        Announce();
    }

    // Guarded because a panel that throws inside a redraw would stop every
    // other panel further down the list from ever hearing about this change --
    // one broken label and the menu freezes instead of showing the error
    private static void Announce()
    {
        Action listeners = Changed;

        if (listeners == null)
            return;

        try
        {
            listeners();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
    }
}
