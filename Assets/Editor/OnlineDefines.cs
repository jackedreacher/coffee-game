#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Turns the online code on when the packages are there, and off when they are
// not.
//
// The alternative was writing the co-op scripts against Netcode and Relay
// directly and telling somebody to install three packages first. That fails in
// the worst possible way: until the packages resolve, Assembly-CSharp does not
// compile, and when Assembly-CSharp does not compile NOTHING in the project
// works -- not the game, not Play mode, and not one of the forty setup commands
// under Cooked Fast, including the one that would have installed the packages.
//
// So every online file is wrapped in #if COOP_ONLINE, and this decides. With
// the packages missing the project is exactly what it was before; with them
// installed the co-op code appears by itself.
[InitializeOnLoad]
public static class OnlineDefines
{
    public const string define = "COOP_ONLINE";

    // Checked by looking for the TYPES rather than by reading the manifest.
    //
    // A package listed in the manifest is a package Unity has been asked for.
    // A type that can be found is a package that resolved, downloaded and
    // compiled -- which is the only version of "installed" that keeps the
    // define from being switched on ahead of the code it enables
    private static readonly string[] needed =
    {
        "Unity.Netcode.NetworkManager",
        "Unity.Netcode.Transports.UTP.UnityTransport",
        "Unity.Services.Core.UnityServices",
        "Unity.Services.Authentication.AuthenticationService",
        "Unity.Services.Multiplayer.MultiplayerService",
    };

    private static readonly NamedBuildTarget[] targets =
    {
        NamedBuildTarget.Standalone,
        NamedBuildTarget.Android,
        NamedBuildTarget.iOS,
    };

    static OnlineDefines()
    {
        // Not in the static constructor itself. This runs during a domain
        // reload, and touching PlayerSettings while Unity is still rebuilding
        // its own state is how an editor ends up writing ProjectSettings from
        // two threads
        EditorApplication.delayCall += Sync;
    }

    [MenuItem("Cooked Fast/Online/Online Kodunu Yeniden Tara", priority = 245)]
    public static void Rescan()
    {
        Sync();

        EditorUtility.DisplayDialog("Online",
            Ready()
                ? "Paketler bulundu. " + define + " acik -- online kodu derleniyor."
                : "Paketler bulunamadi. " + define + " kapali.\n\n" +
                  "Cooked Fast > Online > 1 - Paketleri Kur", "Tamam");
    }

    // The way out if it ever gets stuck the wrong way round: a define left on
    // after a package is removed means Assembly-CSharp cannot compile, and a
    // menu item in an assembly that cannot compile cannot be clicked. Then it
    // has to come off by hand, in
    // Project Settings > Player > Other Settings > Scripting Define Symbols
    [MenuItem("Cooked Fast/Online/Online Kodunu Zorla Kapat", priority = 246)]
    public static void ForceOff()
    {
        Write(false);

        Debug.Log("Online: " + define + " kaldirildi.");
    }

    public static bool Ready()
    {
        for (int i = 0; i < needed.Length; i++)
            if (Find(needed[i]) == null)
                return false;

        return true;
    }

    public static string Missing()
    {
        string list = "";

        for (int i = 0; i < needed.Length; i++)
            if (Find(needed[i]) == null)
                list += "  - " + needed[i] + "\n";

        return list;
    }

    private static void Sync()
    {
        Write(Ready());
    }

    private static void Write(bool wanted)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            NamedBuildTarget target = targets[i];

            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            string next = Edit(current, wanted);

            // Only when it actually changes. Writing the same value back marks
            // ProjectSettings dirty on every domain reload, which turns every
            // script save into a file the version control has to think about
            if (next != current)
                PlayerSettings.SetScriptingDefineSymbols(target, next);
        }
    }

    private static string Edit(string symbols, bool wanted)
    {
        string[] parts = (symbols ?? "").Split(new[] { ';' },
            StringSplitOptions.RemoveEmptyEntries);

        string kept = "";
        bool had = false;

        for (int i = 0; i < parts.Length; i++)
        {
            string one = parts[i].Trim();

            if (one.Length == 0)
                continue;

            if (one == define)
            {
                had = true;

                continue;
            }

            kept += kept.Length > 0 ? ";" + one : one;
        }

        if (!wanted)
            return kept;

        if (!had && kept.Length == 0)
            return define;

        return kept.Length > 0 ? kept + ";" + define : define;
    }

    private static Type Find(string name)
    {
        Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < loaded.Length; i++)
        {
            Type found = loaded[i].GetType(name, false);

            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
