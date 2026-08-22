using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One JSON-backed language source for both the old Unity UI Text controls and
// TextMesh Pro. Existing scene/prefab files do not need a component added to
// every label: the menu asks this class to refresh all live UI periodically.
public static class GameLocalization
{
    public const string LanguagePreference = "CookedFast.Settings.Language";
    private const string resourcePath = "Localization/localization";

    [Serializable]
    private sealed class Database
    {
        public Language[] languages = Array.Empty<Language>();
        public Entry[] entries = Array.Empty<Entry>();
    }

    [Serializable]
    private sealed class Language
    {
        public string code = string.Empty;
        public string name = string.Empty;
    }

    [Serializable]
    private sealed class Entry
    {
        public string key = string.Empty;
        public string source = string.Empty;
        public string[] values = Array.Empty<string>();
    }

    private sealed class SeenText
    {
        public string source;
        public string applied;
    }

    private static readonly Dictionary<string, Entry> byKey =
        new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Entry> byVisibleText =
        new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, SeenText> seen =
        new Dictionary<int, SeenText>();

    private static Database database;
    private static string[] languageNames = { "English", "Türkçe" };
    private static bool initialized;
    private static int currentIndex;

    public static event Action LanguageChanged;

    public static IReadOnlyList<string> LanguageNames
    {
        get
        {
            Initialize();
            return languageNames;
        }
    }

    public static int CurrentIndex
    {
        get
        {
            Initialize();
            return currentIndex;
        }
    }

    public static string CurrentCode
    {
        get
        {
            Initialize();
            return database != null && database.languages != null &&
                   currentIndex < database.languages.Length
                ? database.languages[currentIndex].code
                : "en";
        }
    }

    public static void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        TextAsset asset = Resources.Load<TextAsset>(resourcePath);

        if (asset == null)
        {
            Debug.LogError("[Dil] Resources/" + resourcePath +
                           ".json bulunamadi. Ingilizce kullaniliyor.");
            currentIndex = 0;
            return;
        }

        try
        {
            database = JsonUtility.FromJson<Database>(asset.text);
        }
        catch (Exception exception)
        {
            Debug.LogError("[Dil] localization.json okunamadi: " +
                           exception.Message);
            database = null;
        }

        if (database == null || database.languages == null ||
            database.languages.Length <= 0)
        {
            Debug.LogError("[Dil] localization.json icinde dil listesi yok.");
            currentIndex = 0;
            return;
        }

        languageNames = new string[database.languages.Length];
        for (int i = 0; i < database.languages.Length; i++)
            languageNames[i] = database.languages[i].name;

        byKey.Clear();
        byVisibleText.Clear();

        if (database.entries != null)
        {
            foreach (Entry entry in database.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                byKey[entry.key.Trim()] = entry;
                AddVisible(entry.source, entry);

                if (entry.values == null)
                    continue;

                foreach (string value in entry.values)
                    AddVisible(value, entry);
            }
        }

        currentIndex = Mathf.Clamp(PlayerPrefs.GetInt(LanguagePreference, 0),
            0, languageNames.Length - 1);
    }

    public static void SetLanguage(int index)
    {
        Initialize();
        int safe = Mathf.Clamp(index, 0, languageNames.Length - 1);

        currentIndex = safe;
        PlayerPrefs.SetInt(LanguagePreference, currentIndex);
        PlayerPrefs.Save();

        LanguageChanged?.Invoke();
        RefreshAll();
        Debug.Log("[Dil] " + languageNames[currentIndex] + " secildi.");
    }

    public static string Get(string key, string fallback = null)
    {
        Initialize();

        if (string.IsNullOrWhiteSpace(key) || !byKey.TryGetValue(key, out Entry entry))
            return fallback ?? key ?? string.Empty;

        return Value(entry, fallback ?? entry.source ?? key);
    }

    public static string Format(string key, string fallback, params object[] arguments)
    {
        string format = Get(key, fallback);

        try
        {
            return string.Format(format, arguments);
        }
        catch (FormatException)
        {
            return string.Format(fallback, arguments);
        }
    }

    // Translates all existing labels, including inactive menu panels and UI
    // instantiated later by stations/customers. Unknown text is left intact.
    public static void RefreshAll()
    {
        Initialize();

        foreach (Text label in UnityEngine.Object.FindObjectsByType<Text>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label != null && label.GetComponentInParent<Dropdown>(true) == null)
                Apply(label.GetInstanceID(), label.text, value => label.text = value);
        }

        foreach (TMP_Text label in UnityEngine.Object.FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label != null && label.GetComponentInParent<Dropdown>(true) == null)
                Apply(label.GetInstanceID(), label.text, value => label.text = value);
        }
    }

    private static void Apply(int id, string visible, Action<string> assign)
    {
        visible = visible ?? string.Empty;

        if (!seen.TryGetValue(id, out SeenText state))
        {
            state = new SeenText { source = visible, applied = visible };
            seen.Add(id, state);
        }
        else if (!string.Equals(visible, state.applied, StringComparison.Ordinal))
        {
            // A gameplay script changed the value since the last scan. Treat
            // that new value as the source instead of restoring stale text.
            state.source = visible;
        }

        string translated = TranslateVisible(state.source);
        if (!string.Equals(visible, translated, StringComparison.Ordinal))
            assign(translated);

        state.applied = translated;
    }

    private static string TranslateVisible(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return source;

        string clean = source.Trim();
        if (!byVisibleText.TryGetValue(clean, out Entry entry))
            return source;

        return Value(entry, source);
    }

    private static string Value(Entry entry, string fallback)
    {
        if (entry.values == null || currentIndex < 0 ||
            currentIndex >= entry.values.Length ||
            string.IsNullOrEmpty(entry.values[currentIndex]))
            return fallback;

        return entry.values[currentIndex];
    }

    private static void AddVisible(string value, Entry entry)
    {
        if (!string.IsNullOrWhiteSpace(value))
            byVisibleText[value.Trim()] = entry;
    }
}
