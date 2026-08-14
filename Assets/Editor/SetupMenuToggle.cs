using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// The one-time scaffolding commands -- the lesson setups, the character
// builders, the scene builder -- are done and will not be run again, but
// deleting them throws away the record of how the scene was assembled. This
// hides them behind a define instead, and puts the define on a switch so
// getting them back is one click rather than a trip through Player Settings
public static class SetupMenuToggle
{
    public const string define = "COOKED_FAST_SETUP";

    private const string menuPath = "Cooked Fast/Kurulum Komutlarini Goster";

    // Down at the bottom of the menu, away from the things used daily
    [MenuItem(menuPath, priority = 2000)]
    private static void Toggle()
    {
        SetEnabled(!IsEnabled());
    }

    [MenuItem(menuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(menuPath, IsEnabled());
        return true;
    }

    private static NamedBuildTarget Target =>
        NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

    public static bool IsEnabled()
    {
        return Read().Contains(define);
    }

    private static void SetEnabled(bool enabled)
    {
        List<string> symbols = Read();

        if (enabled == symbols.Contains(define))
            return;

        if (enabled)
            symbols.Add(define);
        else
            symbols.Remove(define);

        PlayerSettings.SetScriptingDefineSymbols(Target, symbols.ToArray());

        Debug.Log("Kurulum komutlari " + (enabled ? "acildi" : "gizlendi") +
                  ". Unity derleyince menu guncellenir.");
    }

    private static List<string> Read()
    {
        PlayerSettings.GetScriptingDefineSymbols(Target, out string[] symbols);

        return new List<string>(symbols);
    }
}
