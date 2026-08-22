using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wires Assets/sesler up to the game, once, from a menu item.
//
// A handful of mp3s is not much dragging, but the dragging is not the
// reason this exists. The file names are Turkish written with COMBINING
// diacritics -- the "s" and the cedilla underneath it are two separate
// characters -- so they do not survive a round trip through a console, a shell
// or a hand typed string literal, and they will not compare equal to anything
// anybody types. Matching is done on the plain ASCII runs inside them instead,
// which is the part of the name no encoding can argue about.
public static class SoundSetup
{
    private const string folder = "Assets/sesler";

    private const string musicFolder =
        "Assets/Cyberleaf Music - The 8-bit Jukebox Lite";

    private const string lobbyMusicPath =
        "Assets/8Bit Music - 062022/7. Track 7.wav";

    private const string objectName = "SES";

    private const string lifeLostPath =
        "Assets/HintsStarsLite/Marimba 3 Notes Descend.wav";

    private const string kissPath =
        "Assets/HintsStarsLite/muah-zeta.mp3";

    private const string disappointedPath =
        "Assets/8Bit Music - 062022/hmph_iLFjXJj.mp3";

    private const string timerPath =
        "Assets/sesler/timer_F69dkQ9.mp3";

    private const string characterChangedPath =
        "Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-41.wav";

    private const string roundIntroPath =
        "Assets/HintsStarsLite/Mellow Hint 2.wav";

    // ASCII fragments, each unique across the folder.
    //
    // Chosen to contain no Turkish letter at all: "ecek alma" rather than
    // "icecek alma", "teri gelme" rather than "musteri gelme". Anything with a
    // dotted or undotted i in it would be a guess about which encoding the file
    // system handed back
    private static readonly (string fragment, SoundManager.Sound sound, string label)[] wanted =
    {
        ("klama-ses", SoundManager.Sound.Click, "tiklama"),
        ("teri gelme", SoundManager.Sound.CustomerArrives, "musteri gelmesi"),
        ("yemek-haz", SoundManager.Sound.FoodReady, "yemek hazir tiki"),
        ("ecek alma", SoundManager.Sound.DrinkTaken, "icecek alma"),
        ("teslim edilme", SoundManager.Sound.OrderDelivered, "siparis teslimi"),
        ("dono_", SoundManager.Sound.Money, "para / cash"),
        ("new-take-item", SoundManager.Sound.ItemTaken, "esya alma (icecek haric)"),
        ("new-give-item", SoundManager.Sound.ItemGiven, "esya birakma / whoosh"),
        ("new-give-item", SoundManager.Sound.OrderBubbleOpened, "siparis balonu pop"),
        ("Marimba 3 Notes Descend", SoundManager.Sound.OrderFailed, "can kaybi"),
        ("dissapointed-crowd", SoundManager.Sound.GameOver, "oyun bitti / son can"),
        ("muah-zeta", SoundManager.Sound.Kiss, "musteri chef kiss (kisik)"),
        ("hmph_iLFjXJj", SoundManager.Sound.CustomerDisappointed,
            "musteri eli bos donerken hmph"),
        ("timer_F69dkQ9", SoundManager.Sound.PatienceCountdown,
            "son 5 saniye timer sesi"),
        ("DM-CGS-41", SoundManager.Sound.CharacterChanged,
            "ana menu karakter degistirme"),
        ("Mellow Hint 2", SoundManager.Sound.RoundIntro,
            "raund giris yazisi"),
    };

    // Not in the table above because it is not a one shot -- it runs on its own
    // looping source for as long as anything is in a pan
    private const string cookingFragment = "me-sesi";

    [MenuItem("Cooked Fast/Ses/1 - Ses Sistemini Kur", priority = 800)]
    public static void Build()
    {
        List<AudioClip> clips = Clips();
        List<AudioClip> music = MusicClips();
        AudioClip lobbyMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(lobbyMusicPath);

        if (clips.Count <= 0)
        {
            EditorUtility.DisplayDialog(
                "Ses",
                folder + " altinda hic ses bulunamadi.\n\n" +
                "Klasor adi ya da konumu degistiyse SoundSetup.folder guncellenmeli.",
                "Tamam");

            return;
        }

        // Before anything is assigned. A clip left on the default streaming
        // settings costs a disk read at the moment it is asked for, which for a
        // tap sound is a tap that arrives late
        foreach (AudioClip clip in clips)
            Tune(clip);

        // Long WAV music must not be decompressed into phone memory. Stream it
        // from the build as compressed Vorbis, while preserving stereo.
        foreach (AudioClip clip in music)
            TuneMusic(clip);

        if (lobbyMusic != null)
            TuneMusic(lobbyMusic);

        SoundManager manager = Manager();

        SerializedObject serialized = new SerializedObject(manager);

        SerializedProperty entries = serialized.FindProperty("entries");

        entries.arraySize = wanted.Length;

        string report = "";
        List<string> missing = new List<string>();

        for (int i = 0; i < wanted.Length; i++)
        {
            AudioClip found = Match(clips, wanted[i].fragment);

            SerializedProperty entry = entries.GetArrayElementAtIndex(i);

            entry.FindPropertyRelative("sound").enumValueIndex = (int)wanted[i].sound;
            entry.FindPropertyRelative("clip").objectReferenceValue = found;

            // Only on a fresh slot, so re running this does not undo a volume
            // somebody balanced by ear
            if (wanted[i].sound == SoundManager.Sound.Kiss)
                entry.FindPropertyRelative("volume").floatValue = .12f;
            else if (wanted[i].sound == SoundManager.Sound.CustomerDisappointed)
                entry.FindPropertyRelative("volume").floatValue = .7f;
            else if (wanted[i].sound == SoundManager.Sound.PatienceCountdown)
                entry.FindPropertyRelative("volume").floatValue = .12f;
            else if (wanted[i].sound == SoundManager.Sound.CharacterChanged)
                entry.FindPropertyRelative("volume").floatValue = .5f;
            else if (wanted[i].sound == SoundManager.Sound.RoundIntro)
                entry.FindPropertyRelative("volume").floatValue = .55f;
            else if (entry.FindPropertyRelative("volume").floatValue <= 0f)
                entry.FindPropertyRelative("volume").floatValue = 1f;

            if (entry.FindPropertyRelative("gap").floatValue <= 0f)
                entry.FindPropertyRelative("gap").floatValue =
                    wanted[i].sound == SoundManager.Sound.CustomerDisappointed ? .2f : .04f;

            if (found == null)
                missing.Add(wanted[i].label);
            else
                report += "  " + wanted[i].label + "  <-  " + found.name + "\n";
        }

        AudioClip loop = Match(clips, cookingFragment);

        serialized.FindProperty("cookingLoop").objectReferenceValue = loop;

        if (loop != null)
            report += "  pisme donguSu  <-  " + loop.name + "\n";
        else
            missing.Add("pisme dongusu");

        SerializedProperty playlist = serialized.FindProperty("musicPlaylist");

        playlist.arraySize = music.Count;

        for (int i = 0; i < music.Count; i++)
        {
            playlist.GetArrayElementAtIndex(i).objectReferenceValue = music[i];
            report += "  muzik " + (i + 1).ToString("00") + "  <-  " + music[i].name + "\n";
        }

        SerializedProperty musicVolume = serialized.FindProperty("musicVolume");

        if (musicVolume.floatValue <= .001f)
            musicVolume.floatValue = .18f;

        if (music.Count <= 0)
            missing.Add("arka plan muzikleri (" + musicFolder + ")");

        serialized.FindProperty("lobbyMusic").objectReferenceValue = lobbyMusic;

        SerializedProperty lobbyVolume = serialized.FindProperty("lobbyMusicVolume");

        if (lobbyVolume.floatValue <= .001f)
            lobbyVolume.floatValue = .15f;

        if (lobbyMusic == null)
            missing.Add("lobi muzigi (" + lobbyMusicPath + ")");
        else
            report += "  lobi muzigi (loop)  <-  " + lobbyMusic.name + "\n";

        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Selection.activeGameObject = manager.gameObject;

        // Named individually rather than counted. "6 of 7 assigned" is a number
        // somebody then has to go and diff against the folder by hand
        List<string> spare = Spare(clips);

        string trouble = missing.Count <= 0
            ? ""
            : "\n\nBAGLANAMADI:\n  " + string.Join("\n  ", missing);

        string extra = spare.Count <= 0
            ? ""
            : "\n\nKULLANILMADI (adinda ne oldugu yazmiyor):\n  " +
              string.Join("\n  ", spare) +
              "\n\nBunu SES objesindeki bir slota elle surukleyebilirsin.";

        Debug.Log("[Ses] Kurulum bitti.\n\nBAGLANDI:\n" + report + trouble + extra,
                  manager.gameObject);

        EditorUtility.DisplayDialog(
            "Ses",
            "Sahnedeki \"" + objectName + "\" objesi kuruldu ve secildi.\n\n" +
            "BAGLANDI:\n" + report + trouble + extra +
            "\n\nSAHNE HENUZ KAYDEDILMEDI -- Ctrl+S.",
            "Tamam");
    }

    // Adds only the lobby track. Re-running the full sound setup is unnecessary
    // and could rewrite entry ordering somebody has since inspected by hand.
    [MenuItem("Cooked Fast/Ses/2 - Lobi Muzigini Bagla", priority = 801)]
    public static void WireLobbyMusic()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Lobi Muzigi",
                "Play'den cik ve tekrar dene. Oyun sirasindaki sahne degisikligi kaybolur.",
                "Tamam");
            return;
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(lobbyMusicPath);

        if (clip == null)
        {
            EditorUtility.DisplayDialog("Lobi Muzigi",
                "Dosya bulunamadi:\n" + lobbyMusicPath, "Tamam");
            return;
        }

        TuneMusic(clip);

        SoundManager manager = Manager();
        Undo.RecordObject(manager, "Wire lobby music");

        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("lobbyMusic").objectReferenceValue = clip;

        SerializedProperty volume = serialized.FindProperty("lobbyMusicVolume");

        if (volume.floatValue <= .001f)
            volume.floatValue = .15f;

        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;

        Debug.Log("[Muzik/Lobi] " + clip.name +
                  " baglandi. Ana menude loop, Play'de oyun playlisti. Ctrl+S.", manager);
        EditorUtility.DisplayDialog("Lobi Muzigi",
            clip.name + " baglandi.\n\n" +
            "Ana menu/ayarlar/karakter seciminde loop calar.\n" +
            "Play'e basinca oyun playlistine gecer.\n\nCtrl+S ile kaydet.",
            "Tamam");
    }

    [MenuItem("Cooked Fast/Ses/3 - Musteri Hmph Sesini Bagla", priority = 802)]
    public static void WireCustomerDisappointed()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Musteri Hmph",
                "Play'den cik ve tekrar dene. Oyun sirasindaki sahne degisikligi kaybolur.",
                "Tamam");
            return;
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(disappointedPath);

        if (clip == null)
        {
            EditorUtility.DisplayDialog("Musteri Hmph",
                "Dosya bulunamadi:\n" + disappointedPath, "Tamam");
            return;
        }

        Tune(clip);

        SoundManager manager = Manager();
        Undo.RecordObject(manager, "Wire customer disappointed sound");

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty entries = serialized.FindProperty("entries");
        SetEntry(entries, SoundManager.Sound.CustomerDisappointed, clip, .7f, .2f);

        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;

        Debug.Log("[Ses] Musteri 180 derece donmeye baslarken " + clip.name +
                  " calacak. Ctrl+S.", manager);
        EditorUtility.DisplayDialog("Musteri Hmph",
            clip.name + " baglandi.\n\n" +
            "Yalnizca yemek alamayan musteri NoGesture tepkisini bitirip\n" +
            "180 derece donmeye basladigi karede calar.\n\nCtrl+S ile kaydet.",
            "Tamam");
    }

    [MenuItem("Cooked Fast/Ses/4 - Son 5 Saniye Timer Sesini Bagla", priority = 803)]
    public static void WirePatienceCountdown()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Timer Sesi",
                "Play'den cik ve tekrar dene. Oyun sirasindaki sahne degisikligi kaybolur.",
                "Tamam");
            return;
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(timerPath);

        if (clip == null)
        {
            EditorUtility.DisplayDialog("Timer Sesi",
                "Dosya bulunamadi:\n" + timerPath, "Tamam");
            return;
        }

        Tune(clip);

        SoundManager manager = Manager();
        Undo.RecordObject(manager, "Wire patience countdown sound");

        SerializedObject serialized = new SerializedObject(manager);
        SetEntry(serialized.FindProperty("entries"),
            SoundManager.Sound.PatienceCountdown, clip, .12f, .04f);
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;

        Debug.Log("[Ses] Son 5 saniye timer sesi: " + clip.name + ". Ctrl+S.", manager);
        EditorUtility.DisplayDialog("Timer Sesi",
            clip.name + " baglandi.\n\n" +
            "Musteri sayaci 5, 4, 3, 2 ve 1'e gecerken birer kez calar.\n" +
            "0'da calmaz; orasi can kaybi olayina aittir.\n\nCtrl+S ile kaydet.",
            "Tamam");
    }

    [MenuItem("Cooked Fast/Ses/5 - Kiss Sesini Kis", priority = 804)]
    public static void LowerKissVolume()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Kiss Sesi",
                "Play'den cik ve tekrar dene. Oyun sirasindaki sahne degisikligi kaybolur.",
                "Tamam");
            return;
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(kissPath);

        if (clip == null)
        {
            EditorUtility.DisplayDialog("Kiss Sesi",
                "Dosya bulunamadi:\n" + kissPath, "Tamam");
            return;
        }

        Tune(clip);

        SoundManager manager = Manager();
        Undo.RecordObject(manager, "Lower kiss sound volume");

        SerializedObject serialized = new SerializedObject(manager);
        SetEntry(serialized.FindProperty("entries"),
            SoundManager.Sound.Kiss, clip, .12f, .15f);
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;

        Debug.Log("[Ses] Kiss sesi %12 seviyesine indirildi. Ctrl+S.", manager);
        EditorUtility.DisplayDialog("Kiss Sesi",
            "muah-zeta %12 seviyesine indirildi.\n\nCtrl+S ile kaydet.",
            "Tamam");
    }

    [MenuItem("Cooked Fast/Ses/6 - Karakter Degistirme Sesini Bagla", priority = 805)]
    public static void WireCharacterChanged()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Karakter Degistirme Sesi",
                "Play'den cik ve tekrar dene. Oyun sirasindaki sahne degisikligi kaybolur.",
                "Tamam");
            return;
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(characterChangedPath);

        if (clip == null)
        {
            EditorUtility.DisplayDialog("Karakter Degistirme Sesi",
                "Dosya bulunamadi:\n" + characterChangedPath, "Tamam");
            return;
        }

        Tune(clip);

        SoundManager manager = Manager();
        Undo.RecordObject(manager, "Wire character changed sound");

        SerializedObject serialized = new SerializedObject(manager);
        SetEntry(serialized.FindProperty("entries"),
            SoundManager.Sound.CharacterChanged, clip, .5f, .05f);
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;

        Debug.Log("[Ses] Ana menu karakter degistirme sesi: " + clip.name +
                  ". Ctrl+S.", manager);
        EditorUtility.DisplayDialog("Karakter Degistirme Sesi",
            clip.name + " baglandi.\n\nSag/sol ok veya kaydirma ile skin " +
            "degistiginde calar.\nIlk menu acilisinda calmaz.\n\nCtrl+S ile kaydet.",
            "Tamam");
    }

    [MenuItem("Cooked Fast/Ses/7 - Raund Giris Sesini Bagla", priority = 806)]
    public static void WireRoundIntro()
    {
        InstallRoundIntro(true);
    }

    // RoundSetup calls this too, so one level-system command leaves both the
    // data curve and its announcement sound ready. Existing sound entries are
    // preserved; only this named event is added or updated.
    public static bool InstallRoundIntro(bool showDialog)
    {
        if (EditorApplication.isPlaying)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Raund Giris Sesi",
                    "Play'den cik ve tekrar dene.", "Tamam");

            return false;
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(roundIntroPath);

        if (clip == null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Raund Giris Sesi",
                    "Dosya bulunamadi:\n" + roundIntroPath, "Tamam");

            return false;
        }

        Tune(clip);

        SoundManager manager = Manager();
        Undo.RecordObject(manager, "Wire round intro sound");

        SerializedObject serialized = new SerializedObject(manager);
        SetEntry(serialized.FindProperty("entries"),
            SoundManager.Sound.RoundIntro, clip, .55f, .5f);
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;

        Debug.Log("[Ses] Raund girisi: " + clip.name + ". Ctrl+S.", manager);

        if (showDialog)
            EditorUtility.DisplayDialog("Raund Giris Sesi",
                clip.name + " raund yazisinin ekrana girdigi kareye baglandi.\n\n" +
                "Ses seviyesi: %55\nCtrl+S ile kaydet.", "Tamam");

        return true;
    }

    private static void SetEntry(
        SerializedProperty entries, SoundManager.Sound sound, AudioClip clip,
        float volume, float gap)
    {
        int at = -1;

        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("sound")
                    .enumValueIndex != (int)sound)
                continue;

            at = i;
            break;
        }

        if (at < 0)
        {
            at = entries.arraySize;
            entries.arraySize++;
        }

        SerializedProperty entry = entries.GetArrayElementAtIndex(at);
        entry.FindPropertyRelative("sound").enumValueIndex = (int)sound;
        entry.FindPropertyRelative("clip").objectReferenceValue = clip;
        entry.FindPropertyRelative("volume").floatValue = volume;
        entry.FindPropertyRelative("gap").floatValue = gap;
    }

    // Found or made. Reused rather than replaced, so running this twice does
    // not throw away volumes somebody has since balanced
    private static SoundManager Manager()
    {
        SoundManager found = Object.FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);

        if (found != null)
            return found;

        GameObject made = new GameObject(objectName);

        Undo.RegisterCreatedObjectUndo(made, "Ses sistemi");

        return Undo.AddComponent<SoundManager>(made);
    }

    private static List<AudioClip> Clips()
    {
        List<AudioClip> found = new List<AudioClip>();

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (clip != null)
                found.Add(clip);
        }

        // These deliberately live in their purchased sound pack rather than
        // Assets/sesler. Load only the requested clips; scanning the whole pack
        // would list hundreds of unrelated sounds as unused every setup run.
        AudioClip lifeLost = AssetDatabase.LoadAssetAtPath<AudioClip>(lifeLostPath);
        AudioClip kiss = AssetDatabase.LoadAssetAtPath<AudioClip>(kissPath);
        AudioClip disappointed = AssetDatabase.LoadAssetAtPath<AudioClip>(disappointedPath);
        AudioClip characterChanged =
            AssetDatabase.LoadAssetAtPath<AudioClip>(characterChangedPath);
        AudioClip roundIntro = AssetDatabase.LoadAssetAtPath<AudioClip>(roundIntroPath);

        if (lifeLost != null && !found.Contains(lifeLost))
            found.Add(lifeLost);

        if (kiss != null && !found.Contains(kiss))
            found.Add(kiss);

        if (disappointed != null && !found.Contains(disappointed))
            found.Add(disappointed);

        if (characterChanged != null && !found.Contains(characterChanged))
            found.Add(characterChanged);

        if (roundIntro != null && !found.Contains(roundIntro))
            found.Add(roundIntro);

        return found;
    }

    private static List<AudioClip> MusicClips()
    {
        List<AudioClip> found = new List<AudioClip>();

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { musicFolder }))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                AssetDatabase.GUIDToAssetPath(guid));

            if (clip != null)
                found.Add(clip);
        }

        // AssetDatabase does not promise a stable order. File-name order is
        // deterministic across Windows, Mac and the iOS build.
        found.Sort((a, b) => string.Compare(
            AssetDatabase.GetAssetPath(a),
            AssetDatabase.GetAssetPath(b),
            System.StringComparison.OrdinalIgnoreCase));

        return found;
    }

    // Compared against the ASSET PATH rather than clip.name, because the two
    // are not the same string for an mp3 and the path is the one that keeps the
    // ASCII runs the fragments were taken from
    private static AudioClip Match(List<AudioClip> clips, string fragment)
    {
        foreach (AudioClip clip in clips)
        {
            if (AssetDatabase.GetAssetPath(clip).Contains(fragment))
                return clip;
        }

        return null;
    }

    private static List<string> Spare(List<AudioClip> clips)
    {
        List<string> left = new List<string>();

        foreach (AudioClip clip in clips)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            bool used = path.Contains(cookingFragment);

            for (int i = 0; i < wanted.Length && !used; i++)
                used = path.Contains(wanted[i].fragment);

            if (!used)
                left.Add(clip.name);
        }

        return left;
    }

    // Import settings a sound effect wants, which are not the ones an mp3
    // arrives with.
    //
    // Unity's default is to stream compressed audio off disk, which is right
    // for a three minute music track and wrong for every clip in this folder: a
    // tap noise that has to be fetched and decoded before it starts is a tap
    // noise that lands after the tap. Decompressed on load costs memory these
    // are far too short to care about
    private static void Tune(AudioClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);

        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        bool loop = path.Contains(cookingFragment);

        // The sizzle is the one long clip here, and the one that can afford to
        // be decoded as it plays because it starts on a station timer rather
        // than on a finger
        settings.loadType = loop
            ? AudioClipLoadType.CompressedInMemory
            : AudioClipLoadType.DecompressOnLoad;

        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .7f;
        settings.preloadAudioData = true;

        bool changed = importer.defaultSampleSettings.loadType != settings.loadType
                       || importer.defaultSampleSettings.compressionFormat != settings.compressionFormat
                       || !importer.forceToMono
                       || importer.loadInBackground;

        importer.defaultSampleSettings = settings;

        // Mono, because everything here is played at spatialBlend 0 into both
        // ears anyway -- a stereo copy is twice the data for no audible result
        importer.forceToMono = true;
        importer.loadInBackground = false;

        // Guarded, because reimporting seven files on every run of this command
        // is a few seconds of the editor locking up for nothing
        if (changed)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void TuneMusic(AudioClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);

        if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        bool changed = settings.loadType != AudioClipLoadType.Streaming
                       || settings.compressionFormat != AudioCompressionFormat.Vorbis
                       || Mathf.Abs(settings.quality - .55f) > .001f
                       || settings.preloadAudioData
                       || importer.forceToMono
                       || !importer.loadInBackground;

        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .55f;
        settings.preloadAudioData = false;

        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.loadInBackground = true;

        if (changed)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
