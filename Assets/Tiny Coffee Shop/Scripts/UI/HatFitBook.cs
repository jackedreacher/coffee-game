using System;
using System.Collections.Generic;
using UnityEngine;

// Per-animal hat placement, kept beside the localisation table rather than on
// the prefabs.
//
// One global proportion cannot fit all fifteen. Every measurement available at
// runtime describes the OUTLINE of the animal, and the outline is horns on a
// bull and ears on a cat -- so anything read off it puts the hat above them.
// Reading the skull itself would need the mesh, and the DGN models import with
// Read/Write off, so the vertices cannot be asked where the head ends. The
// skeleton cannot answer either: all fifteen share one rig with identical bone
// offsets, so it says the same thing about a bull as about a pug while their
// skulls sit at genuinely different heights.
//
// What is left is a few numbers per animal, dialled once by eye through
// Cooked Fast > Sapka > Sapka Ince Ayar. An animal with no entry falls back to
// PlayerHatFitter's defaults, so the table only has to hold the exceptions.
public static class HatFitBook
{
    [Serializable]
    public class Entry
    {
        public string animal;

        // All three are multiples of the feet-to-head-bone distance, so they
        // mean the same thing whatever the animal is scaled to.
        public float crown;
        public float width;

        // Along the head bone's own forward. Positive pushes the hat towards
        // the snout. Zero is a real value here, not an empty field, so this one
        // gets no sentinel -- an entry that exists is believed.
        public float forward;
    }

    [Serializable]
    private class Book
    {
        public Entry[] entries;
    }

    public const string ResourcePath = "Hats/hat_fit";
    public const string AssetPath = "Assets/Resources/Hats/hat_fit.json";

    private static Dictionary<string, Entry> loaded;

    public static bool TryGet(string animal, out float crown, out float width,
        out float forward)
    {
        crown = PlayerHatFitter.DefaultCrown;
        width = PlayerHatFitter.DefaultHeadWidth;
        forward = PlayerHatFitter.DefaultForward;

        if (string.IsNullOrWhiteSpace(animal))
            return false;

        Load();

        if (!loaded.TryGetValue(Key(animal), out Entry entry) || entry == null)
            return false;

        // No sentinels here any more. There used to be a "zero means nobody
        // filled this field in" rule, but the tuner's fields have no floor now
        // -- zero and negative are values somebody can genuinely dial -- and a
        // sentinel would quietly throw such a value away and hand back the
        // default, which reads as the tuner ignoring the slider. Every entry in
        // the file is written by Set, which always fills all three, so an entry
        // that exists is believed exactly as it stands.
        crown = entry.crown;
        width = entry.width;
        forward = entry.forward;

        return true;
    }

    // Live edit, without writing the file. The tuner pushes every slider move
    // through here so the change is on screen before anything is saved.
    public static void Set(string animal, float crown, float width, float forward)
    {
        if (string.IsNullOrWhiteSpace(animal))
            return;

        Load();

        string key = Key(animal);

        if (!loaded.TryGetValue(key, out Entry entry) || entry == null)
        {
            entry = new Entry { animal = animal.Trim() };
            loaded[key] = entry;
        }

        entry.crown = crown;
        entry.width = width;
        entry.forward = forward;
    }

    public static void Forget(string animal)
    {
        if (string.IsNullOrWhiteSpace(animal))
            return;

        Load();
        loaded.Remove(Key(animal));
    }

    public static List<Entry> All()
    {
        Load();
        return new List<Entry>(loaded.Values);
    }

    public static string ToJson()
    {
        List<Entry> entries = All();
        entries.Sort((a, b) => string.CompareOrdinal(a.animal, b.animal));
        return JsonUtility.ToJson(new Book { entries = entries.ToArray() }, true);
    }

    // Drops the cache so the next read comes off disk. For when the json is
    // edited by hand; the tuner does not need it, because saving writes exactly
    // what the cache already holds.
    public static void Reload()
    {
        loaded = null;
    }

    private static string Key(string animal)
    {
        return animal == null ? string.Empty : animal.Trim().ToLowerInvariant();
    }

    private static void Load()
    {
        if (loaded != null)
            return;

        loaded = new Dictionary<string, Entry>();

        TextAsset file = Resources.Load<TextAsset>(ResourcePath);

        if (file == null || string.IsNullOrWhiteSpace(file.text))
            return;

        Book book = JsonUtility.FromJson<Book>(file.text);

        if (book == null || book.entries == null)
            return;

        for (int i = 0; i < book.entries.Length; i++)
        {
            Entry entry = book.entries[i];

            if (entry != null && !string.IsNullOrWhiteSpace(entry.animal))
                loaded[Key(entry.animal)] = entry;
        }
    }
}
