using System.Collections.Generic;
using UnityEngine;

// Every sound effect in the game, played through one place.
//
// Reached statically and NULL SAFE by design: SoundManager.Play(...) on a scene
// with no manager in it does nothing at all. That is deliberate rather than
// lazy. The callers are gameplay code -- a cooker finishing, a customer
// arriving, a tap landing -- and none of them should carry a null check or stop
// working because somebody opened a test scene without the audio object in it.
//
// One shots go through a small pool of AudioSources rather than one source per
// call. Two customers arriving on the same frame with a single source means the
// second cuts the first off, and PlayOneShot on a shared source mixes them
// instead -- but a shared source cannot carry a per sound volume, which is the
// one thing that makes a set of clips recorded at different levels usable. The
// pool gives both.
//
// All 2D. This is a portrait phone game looking down at one room; panning a
// frying pan to the left edge of the stereo field because the oven is over
// there is effort spent making the game harder to hear.
public class SoundManager : MonoBehaviour
{
    private const string effectsPref = "CookedFast.Audio.Effects";
    private const string musicPref = "CookedFast.Audio.Music";

    // The customer-arrival mp3 contains roughly one second of silence before
    // its audible transient. The event itself is fired on the spawn frame, so
    // skip that encoded lead-in instead of moving gameplay timing earlier.
    private const float customerArrivalLeadIn = 1f;

    public static SoundManager Instance { get; private set; }

    // What can be played. Deliberately named after the EVENT and not after the
    // file, so a clip can be swapped without touching a single caller
    public enum Sound
    {
        Click,
        CustomerArrives,
        FoodReady,
        DrinkTaken,
        OrderDelivered,
        Money,

        // Anything picked up EXCEPT a drink, which the fridge already announces
        // with a sound of its own
        ItemTaken,

        // Anything put down. Deliberately not the frying pan: a pan already
        // says what happened by starting to sizzle, and a second noise on top
        // of it reads as two events
        ItemGiven,

        // The customer's order bubble beginning its entrance pop. Kept as a
        // separate event even though it currently shares ItemGiven's clip, so
        // changing either sound later does not silently change the other
        OrderBubbleOpened,

        // The customer's patience reached zero before the whole order was
        // delivered. Separate from the disappointed animation and life loss:
        // retries, normal departures and rejected taps must stay silent.
        OrderFailed,

        // Final life reaching zero. Kept apart from OrderFailed so the last
        // missed customer does not stack two failure sounds.
        GameOver,

        // Played only when the customer's authored Chef's Kiss reaction
        // actually starts.
        Kiss,

        // A timed-out customer's negative performance has ended and their
        // authored 180-degree departure turn is starting right now.
        CustomerDisappointed,

        // Shared urgency bed while at least one customer is in their final
        // five seconds.
        PatienceCountdown,

        // Main-menu wardrobe moved to the previous or next animal.
        CharacterChanged,

        // The round title begins its entrance animation.
        RoundIntro,
    }

    [System.Serializable]
    public class Entry
    {
        public Sound sound;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Ayni sesin ust uste binmesini engeller. Ayni ses bu sure " +
                 "icinde ikinci kez istenirse yok sayilir, saniye. 0 = serbest")]
        public float gap = .04f;

        [HideInInspector] public float lastPlayed = -999f;
    }

    [Header(" Sesler ")]
    [SerializeField] private Entry[] entries;

    [Header(" Pisme (dongu) ")]
    [Tooltip("Ocakta bir sey pistigi surece calan ses. Birden fazla ocak ayni " +
             "anda calisirsa yine tek sefer calar")]
    [SerializeField] private AudioClip cookingLoop;

    [Range(0f, 1f)]
    [SerializeField] private float cookingVolume = .6f;

    [Tooltip("Dongu acilip kapanirken ne kadar surede yumusasin, saniye. " +
             "Sifirdan aniden baslayan bir cizirti tik gibi duyulur")]
    [SerializeField] private float cookingFade = .25f;

    [Header(" Arka Plan Muzigi ")]
    [Tooltip("Dosya sirasiyla calar; son parca bitince ilk parcaya doner")]
    [SerializeField] private AudioClip[] musicPlaylist;

    [Range(0f, 1f)]
    [Tooltip("Efektleri bastirmamasi icin dusuk tutulur. 0 = varsayilan 0.18")]
    [SerializeField] private float musicVolume = .18f;

    [Header(" Lobi Muzigi ")]
    [Tooltip("Yalnizca ana menu, ayarlar ve karakter seciminde loop calar")]
    [SerializeField] private AudioClip lobbyMusic;

    [Range(0f, 1f)]
    [Tooltip("Lobi muziginin kendi ses seviyesi. Music ayari bununla da carpilir")]
    [SerializeField] private float lobbyMusicVolume = .15f;

    [Header(" Genel ")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    [Tooltip("Ayni anda calabilecek en fazla tek atimlik ses")]
    [SerializeField] private int voices = 8;

    [Tooltip("Acikken her calan sesi konsola yazar: hangi olay, hangi dosya. " +
             "\"Bu ses neydi\" sorusunu kulakla tartismak yerine tek satirda " +
             "cevaplar. Sadece Editor ve development build")]
    [SerializeField] private bool trace;

    private AudioSource[] pool;
    private int next;

    private AudioSource cooker;
    private AudioSource patience;
    private AudioSource music;
    private AudioSource lobby;
    private int musicIndex;
    private float effectsLevel = 1f;
    private float musicLevel = 1f;

    public static float EffectsLevel => Instance == null ?
        PlayerPrefs.GetFloat(effectsPref, 1f) : Instance.effectsLevel;

    public static float MusicLevel => Instance == null ?
        PlayerPrefs.GetFloat(musicPref, 1f) : Instance.musicLevel;

    // Newly added serialized fields on the already saved SES component arrive
    // as zero, not with their initializer. Treat zero as the quiet default so a
    // correctly wired playlist cannot silently play at no volume.
    private float MusicVolume => musicVolume > .001f ? musicVolume : .18f;
    private float LobbyMusicVolume =>
        lobbyMusicVolume > .001f ? lobbyMusicVolume : .15f;

    // The menu may wake before or after this component. Remembering the request
    // statically makes both script execution orders produce the same first
    // frame, while scenes with no menu remain normal gameplay scenes.
    private static bool lobbyRequested;
    private bool lobbyMode;

    // Counted, not a bool. Two stations cooking at once and one of them
    // finishing must not cut the sizzle off while the other is still going --
    // which is exactly what a bool does, and it is the kind of bug that only
    // shows up on a busy round
    private int cooking;
    private readonly HashSet<int> impatientCustomers = new HashSet<int>();

    private void Awake()
    {
        // Last one in wins rather than first one in, so a scene reload replaces
        // the manager instead of leaving a destroyed one behind the property
        Instance = this;

        effectsLevel = Mathf.Clamp01(PlayerPrefs.GetFloat(effectsPref, 1f));
        musicLevel = Mathf.Clamp01(PlayerPrefs.GetFloat(musicPref, 1f));

        pool = new AudioSource[Mathf.Max(1, voices)];

        for (int i = 0; i < pool.Length; i++)
            pool[i] = MakeSource("SES kanal " + (i + 1), false);

        cooker = MakeSource("SES pisme", true);
        patience = MakeSource("SES son 5 saniye", true);
        music = MakeSource("MUZIK", false);
        lobby = MakeSource("MUZIK lobi", true);
        lobbyMode = lobbyRequested;
    }

    private void Start()
    {
        ApplyMusicMode();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private AudioSource MakeSource(string name, bool loop)
    {
        GameObject host = new GameObject(name);

        host.transform.SetParent(transform, false);

        AudioSource source = host.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;

        return source;
    }

    // ---- one shots ----------------------------------------------------------

    // Static and silent when there is no manager. See the note at the top: the
    // callers are gameplay code and must not have to care
    public static void Play(Sound sound)
    {
        if (Instance != null)
            Instance.Ring(sound);
    }

    // Sounds that belong to the menu rather than to the kitchen.
    private static bool MenuSound(Sound sound)
    {
        return sound == Sound.Click || sound == Sound.CharacterChanged;
    }

    private void Ring(Sound sound)
    {
        // Home does not tear the kitchen down, it parks it behind the menu --
        // and a parked kitchen must not be audible. Time.timeScale does not
        // silence an AudioSource, so the freeze alone leaves it talking.
        if (lobbyMode && !MenuSound(sound))
            return;

        Entry entry = Find(sound);

        if (entry == null || entry.clip == null)
            return;

        // Two customers spawning on the same frame is one arrival sound, not a
        // doubled one at twice the volume -- which is what identical clips
        // starting on the same sample actually sound like
        if (entry.gap > 0f && Time.unscaledTime - entry.lastPlayed < entry.gap)
            return;

        entry.lastPlayed = Time.unscaledTime;

        AudioSource source = pool[next];

        next = (next + 1) % pool.Length;

        float leadIn = sound == Sound.CustomerArrives
            ? customerArrivalLeadIn
            : 0f;

        // PlayOneShot cannot start in the middle of a clip. Only the arrival
        // sound needs this path; every other effect keeps the normal one-shot
        // playback. The length guard prevents a replaced, shorter clip from
        // being reduced to its final sample.
        if (leadIn > 0f && entry.clip.length > leadIn + .05f)
        {
            source.Stop();
            source.clip = entry.clip;
            source.volume = entry.volume * masterVolume * effectsLevel;
            source.time = leadIn;
            source.Play();
        }
        else
        {
            // A pooled source may previously have used the trimmed path.
            // Clear its clip and volume so PlayOneShot is not multiplied by
            // the previous entry's volume a second time.
            source.Stop();
            source.clip = null;
            source.volume = 1f;
            source.PlayOneShot(entry.clip, entry.volume * masterVolume * effectsLevel);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Off by default, because it is one line per sound and a busy round
        // makes a lot of sound. On, it settles "which sound was that" in one
        // console line -- a question that is otherwise argued about by ear,
        // between two short clips that both go click
        if (trace)
            Debug.Log("[Ses] " + sound + "  ->  " + entry.clip.name +
                      (leadIn > 0f ? "  (ilk " + leadIn.ToString("0.00") + " sn atlandi)" : ""));
#endif
    }

    private Entry Find(Sound sound)
    {
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].sound == sound)
                return entries[i];
        }

        return null;
    }

    // ---- final five seconds -------------------------------------------------

    // This clip is eleven seconds long. It is therefore a small loop owned by
    // the final-five-seconds window, not five overlapping one-shots. Several
    // customers can be urgent together; the HashSet keeps one shared sound
    // alive until the last urgent customer is settled or times out.
    public static void SetPatienceCountdown(Object owner, bool on)
    {
        if (Instance != null && owner != null)
            Instance.Patience(owner.GetInstanceID(), on);
    }

    private void Patience(int owner, bool on)
    {
        if (on)
            impatientCustomers.Add(owner);
        else
            impatientCustomers.Remove(owner);

        if (impatientCustomers.Count <= 0)
        {
            if (patience != null)
                patience.Stop();

            return;
        }

        UpdatePatience();
    }

    // Also called every frame, because the menu stops this loop outright and a
    // customer can still be inside their final five seconds when play resumes.
    // Nothing else would restart it: the set has not changed, so Patience() is
    // never called again.
    private void UpdatePatience()
    {
        if (patience == null || lobbyMode || patience.isPlaying ||
            impatientCustomers.Count <= 0)
            return;

        Entry entry = Find(Sound.PatienceCountdown);

        if (entry == null || entry.clip == null)
            return;

        patience.clip = entry.clip;
        patience.loop = true;
        patience.volume = entry.volume * masterVolume * effectsLevel;
        patience.time = 0f;
        patience.Play();
    }

    // ---- the cooking loop ---------------------------------------------------

    // Called with true when something starts cooking and false when it stops,
    // and the two must be balanced. Counted rather than toggled so overlapping
    // stations do not cut each other off
    public static void Cooking(bool on)
    {
        if (Instance != null)
            Instance.Sizzle(on);
    }

    private void Sizzle(bool on)
    {
        cooking = Mathf.Max(0, cooking + (on ? 1 : -1));

        if (cookingLoop == null || cooker == null)
            return;

        if (cooking > 0 && !cooker.isPlaying && !lobbyMode)
        {
            cooker.clip = cookingLoop;
            cooker.volume = 0f;
            cooker.Play();
        }
    }

    private void Update()
    {
        UpdateCooking();
        UpdatePatience();
        UpdateMusic();
    }

    private void UpdateCooking()
    {
        if (cooker == null || cookingLoop == null)
            return;

        float wanted = cooking > 0 && !lobbyMode
            ? cookingVolume * masterVolume * effectsLevel
            : 0f;

        cooker.volume = cookingFade <= .001f
            ? wanted
            : Mathf.MoveTowards(cooker.volume, wanted, Time.unscaledDeltaTime / cookingFade);

        // Stopped rather than left running at zero, so a paused kitchen is not
        // holding a voice open for the rest of the round
        if (cooking <= 0 && cooker.isPlaying && cooker.volume <= .0001f)
            cooker.Stop();
    }

    // ---- background playlist ------------------------------------------------

    private void UpdateMusic()
    {
        if (lobbyMode)
        {
            if (music != null && music.isPlaying)
                music.Stop();

            if (lobby == null || lobbyMusic == null)
                return;

            lobby.volume = LobbyMusicVolume * masterVolume * musicLevel;

            if (!AudioListener.pause && !lobby.isPlaying)
                PlayLobbyMusic();

            return;
        }

        if (lobby != null && lobby.isPlaying)
            lobby.Stop();

        if (music == null || musicPlaylist == null || musicPlaylist.Length <= 0)
            return;

        music.volume = MusicVolume * masterVolume * musicLevel;

        // A paused listener reports no useful end-of-track event. Do not skip a
        // song merely because the game was paused and resumed.
        if (AudioListener.pause || music.isPlaying)
            return;

        PlayNextMusic();
    }

    private void ApplyMusicMode()
    {
        if (lobbyMode)
        {
            if (music != null)
                music.Stop();

            PlayLobbyMusic();
            return;
        }

        if (lobby != null)
            lobby.Stop();

        PlayNextMusic();
    }

    private void PlayLobbyMusic()
    {
        if (lobby == null || lobbyMusic == null)
            return;

        lobby.Stop();
        lobby.clip = lobbyMusic;
        lobby.loop = true;
        lobby.volume = LobbyMusicVolume * masterVolume * musicLevel;
        lobby.time = 0f;
        lobby.Play();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (trace)
            Debug.Log("[Muzik/Lobi] " + lobbyMusic.name + " (loop)", this);
#endif
    }

    private void PlayNextMusic()
    {
        if (music == null || musicPlaylist == null || musicPlaylist.Length <= 0)
            return;

        // Skip empty slots, but inspect each one at most once. A completely
        // empty list must not become an infinite loop in Awake/Update.
        for (int tried = 0; tried < musicPlaylist.Length; tried++)
        {
            AudioClip clip = musicPlaylist[musicIndex];

            musicIndex = (musicIndex + 1) % musicPlaylist.Length;

            if (clip == null)
                continue;

            music.Stop();
            music.clip = clip;
            music.volume = MusicVolume * masterVolume * musicLevel;
            music.time = 0f;
            music.Play();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (trace)
                Debug.Log("[Muzik] " + clip.name, this);
#endif
            return;
        }

        music.clip = null;
    }

    // ---- menu settings -----------------------------------------------------

    public static void SetLobby(bool on)
    {
        lobbyRequested = on;

        if (Instance == null || Instance.lobbyMode == on)
            return;

        Instance.lobbyMode = on;
        Instance.ApplyMusicMode();

        if (on)
            Instance.HushKitchen();
    }

    // One shots still in flight and the urgency loop belong to the kitchen, so
    // opening the menu has to cut them. The cooking COUNTER is deliberately
    // left alone: the sizzle fades out through UpdateCooking and comes back by
    // itself when play resumes, without anybody having to re-count the pans.
    private void HushKitchen()
    {
        if (pool != null)
        {
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null)
                    pool[i].Stop();
        }

        if (patience != null)
            patience.Stop();
    }

    public static void SetEffectsLevel(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(effectsPref, value);
        PlayerPrefs.Save();

        if (Instance != null)
        {
            Instance.effectsLevel = value;

            // The cooking loop is continuous, so it has to respond now rather
            // than only when the next one-shot sound happens.
            if (Instance.cooker != null && Instance.cooking > 0)
                Instance.cooker.volume = Instance.cookingVolume *
                    Instance.masterVolume * value;

            Entry timer = Instance.Find(Sound.PatienceCountdown);

            if (Instance.patience != null && timer != null)
                Instance.patience.volume = timer.volume *
                    Instance.masterVolume * value;
        }
    }

    public static void SetMusicLevel(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(musicPref, value);
        PlayerPrefs.Save();

        if (Instance == null)
            return;

        Instance.musicLevel = value;

        if (Instance.music != null)
            Instance.music.volume = Instance.MusicVolume *
                Instance.masterVolume * value;

        if (Instance.lobby != null)
            Instance.lobby.volume = Instance.LobbyMusicVolume *
                Instance.masterVolume * value;
    }
}
