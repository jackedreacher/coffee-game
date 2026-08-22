using System.Collections.Generic;
using TMPro;
using UnityEngine;

// What the customer walked in for, floating over their head.
//
// Built out of world objects rather than a world space Canvas, and that is a
// decision the oven made for us: a canvas needs a render mode, a scale, a draw
// order and a rect, every one of them is a way to come out invisible, and none
// of it can be checked without running the game. A sprite, a mesh and a
// TextMeshPro are all just things in the scene -- they show up in the scene
// view the moment they exist.
//
// The clock is the interesting part. It is not there to punish slowness on its
// own; it is what turns serving into a choice about order. Two customers, one
// nearly out of patience, is a question worth asking the player
public class CustomerOrder : MonoBehaviour
{
    private const float celebrationSunburstBoost = 1.55f;
    private const float celebrationMoneyBoost = 1.5f;
    public enum Mood
    {
        Mutlu,
        Idare,
        Kizgin,
    }

    [Header(" Elements ")]
    [Tooltip("Balonun tamami. Siparis yokken kapatilir")]
    [SerializeField] private GameObject bubbleRoot;

    [Tooltip("Arkadaki kart. Siparis bitince kapanir, sahne emojiye kalir")]
    [SerializeField] private GameObject card;

    [Tooltip("Istenen yemegin kucuk modelinin konacagi yer")]
    [SerializeField] private Transform iconAnchor;

    [Tooltip("Kac adet istedigi -- x2 gibi")]
    [SerializeField] private TextMeshPro countText;

    // A number, not a bar and not a ring.
    //
    // A drained ring says "some of the time is gone", which is a thing you have
    // to look at and compare. A digit says how long you have, and the colour
    // says how you should feel about it -- the same answer at a glance from the
    // far side of the screen, which is where the player usually is
    [Tooltip("Emojinin etrafinda saat yonunde bosalan halka")]
    [SerializeField] private RadialTimer timerRing;

    [Tooltip("Son saniyelerde beliren rakam")]
    [SerializeField] private TextMeshPro countdownText;

    [Tooltip("Rakamin arkasindaki daire. Rengi yesilden kirmiziya doner")]
    [SerializeField] private SpriteRenderer countdownDisc;

    [Tooltip("Her yeni geri sayim rakaminin pop suresi")]
    [SerializeField] private float countdownPopTime = .22f;

    [Tooltip("Geri sayim rakaminin pop sirasinda ulasacagi boyut")]
    [SerializeField] private float countdownPopScale = 1.5f;

    [SerializeField] private SpriteRenderer emoji;

    [Tooltip("Emojis 45 paketinin ortak hareket yuvasi. Varsa eski SpriteRenderer yerine kullanilir")]
    [SerializeField] private Transform animatedEmojiRoot;
    [SerializeField] private GameObject animatedHappyEmoji;
    [SerializeField] private GameObject animatedNeutralEmoji;
    [SerializeField] private GameObject animatedAngryEmoji;

    [Header(" Emojiler ")]
    [SerializeField] private Sprite happySprite;
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite angrySprite;

    [Header(" Sabir ")]
    [Tooltip("Musterinin bastan sona sabri, saniye. 0 = varsayilan 45")]
    [SerializeField] private float patience = 45f;

    // The ring carries the whole wait; the digits are the alarm at the end of
    // it. One is something you glance at, the other is something you react to,
    // and a number that has been ticking for forty seconds is neither
    [Tooltip("Son kac saniyede rakam belirsin. 0 = varsayilan 5")]
    [SerializeField] private float countdownSeconds = 5f;

    // The last beat of the countdown, and the only one that was missing.
    //
    // Ceil holds each digit for the whole second it stands for -- 1 is on
    // screen from one second left all the way down -- so zero was reached on
    // the single frame the clock expired, which is the same frame the customer
    // left. The number went 5 4 3 2 1 and then they were gone. This is zero
    // getting a moment of its own before that happens
    [Tooltip("Sayac sifiri gosterip kac saniye bekler, sonra musteri gider. " +
             "0 = varsayilan 0.5")]
    [SerializeField] private float zeroHold = .5f;

    [Tooltip("Bol bol vakit varken")]
    [SerializeField] private Color calmColour = new Color(.29f, .78f, .35f);

    [Tooltip("Azalirken")]
    [SerializeField] private Color warnColour = new Color(.98f, .76f, .16f);

    [Tooltip("Bitmek uzereyken")]
    [SerializeField] private Color panicColour = new Color(.90f, .24f, .21f);

    [Tooltip("Sabrin bu oraninin ustunde servis edilirse MUTLU")]
    [Range(.1f, 1f)][SerializeField] private float happyAbove = .6f;

    [Tooltip("Bu oranin altinda KIZGIN, arasi idare eder")]
    [Range(0f, .9f)][SerializeField] private float angryBelow = .25f;

    [Header(" Puan ")]
    [Tooltip("Mutlu musteriden kazanc kac katina cikar")]
    [SerializeField] private float happyReward = 2f;

    [Tooltip("Kizgin musteriden kazanc kac katina duser")]
    [SerializeField] private float angryReward = .5f;

    // The moment the order is finished. Everything the card was for is answered
    // by then -- what they wanted, how many, how long they had -- so the card
    // goes and what is left is the face they are leaving with, which is the only
    // part the player learns anything from
    [Header(" Kutlama ")]
    [Tooltip("Emojinin arkasinda donen isin")]
    [SerializeField] private Transform sunburst;

    [Tooltip("Kazanilan para -- $10")]
    [SerializeField] private TextMeshPro earningsText;

    [Tooltip("Verilen urunun uzerine konacak tik")]
    [SerializeField] private Sprite readyIcon;

    // The customer who has not decided.
    //
    // Their order row names no food, which every serving path already reads as
    // "anything will do" -- but a bubble with nothing in it is a bubble that
    // looks broken. This is what goes there instead, and it says the same thing
    // to the player that the empty row says to the code
    [Tooltip("Ne istedigini bilmeyen musterinin balonundaki isaret")]
    [SerializeField] private Sprite undecidedIcon;

    [Tooltip("O isaretin rengi")]
    [SerializeField] private Color undecidedTint = new Color(.99f, .78f, .21f);

    [Tooltip("Tikin rengi")]
    [SerializeField] private Color readyTint = new Color(.24f, .78f, .35f);

    // The tick was landing on a burger and disappearing into it: green on
    // browns and yellows at the size of a thumbnail is one shape, not two.
    // Darkening the food underneath turns it into a silhouette, and a silhouette
    // is a background rather than a competing picture
    // Faded, not blacked out.
    //
    // Darkening it was the wrong instinct: a dark shape behind a tick is still
    // a shape competing for the same glance, and at thumbnail size the two read
    // as one object. Washing it out drops its contrast instead, which is what
    // actually lets the tick sit on top -- and leaves the food recognisable,
    // which matters, because the player still has to see what was ordered
    [Tooltip("Verilmis urunun rengi. Alfa sadece cizilmis ikonlarda etkili -- " +
             "model ikonlarin materyali saydam degil")]
    [SerializeField] private Color servedTint = new Color(.66f, .66f, .70f, .48f);

    [Tooltip("Emoji kac katina buyusun. 0 = varsayilan 1.7")]
    [SerializeField] private float celebrationScale = 1.7f;

    [Tooltip("Buyume ve kucultme suresi, saniye. 0 = varsayilan 0.25")]
    [SerializeField] private float celebrationTime = .25f;

    [Tooltip("Kutlama ve sunburst bastan sona kac saniye durur. 0 = varsayilan 2")]
    [SerializeField] private float celebrationHold = 2f;

    [Tooltip("Isinin saniyedeki donusu, derece. 0 = donmez, sadece pop yapar")]
    [SerializeField] private float sunburstSpin;

    [Tooltip("Kazanc yazisi, kutlama basladiktan kac saniye sonra sayaca dogru kalkar. " +
             "0 = varsayilan 0.5. Kutlamanin kendisinden kisa olmali, yoksa balon once kapanir")]
    [SerializeField] private float moneyDelay = .5f;

    [Tooltip("Yazinin sayaca varis suresi, saniye. 0 = varsayilan 0.55")]
    [SerializeField] private float moneyFlightTime = .55f;

    [Header(" Acilis / Kapanis ")]
    [Tooltip("Balonun acilis pop suresi, saniye. 0 = varsayilan 0.25")]
    [SerializeField] private float appearTime = .25f;

    [Tooltip("Emoji ve isin, balondan kac saniye sonra patlar. 0 = varsayilan 0.12")]
    [SerializeField] private float popDelay = .12f;

    [Tooltip("Kaybolurken sonme suresi, saniye. 0 = varsayilan 0.3")]
    [SerializeField] private float fadeTime = .3f;

    // The mood changing is the one thing on this card that happens without the
    // player doing anything, which is exactly why it needs announcing. A sprite
    // quietly swapping for another sprite is a change nobody sees happen -- they
    // only ever notice that it already has
    [Header(" Emoji Degisimi ")]
    [Tooltip("Degisince kac katina buyusun. 0 = varsayilan 1.35")]
    [SerializeField] private float moodPopScale = 1.35f;

    [Tooltip("Sallanma suresi, saniye. 0 = varsayilan 0.45")]
    [SerializeField] private float moodShakeTime = .45f;

    [Tooltip("Saga sola kac derece. 0 = varsayilan 18")]
    [SerializeField] private float moodShakeAngle = 18f;

    [Tooltip("Saniyede kac gidip gelme. 0 = varsayilan 3.5")]
    [SerializeField] private float moodShakeSpeed = 3.5f;

    // Sentinels, for the same reason every other number in this project has
    // them: these fields land on prefabs that already exist, and a serialised
    // zero is indistinguishable from a field nobody filled in
    // Handed in per customer where there is one, so a queue is not three
    // identical clocks started at different times. Falls back to the prefab's
    // own value, which is what a scene with nobody setting it still gets
    private float given;

    public void SetPatience(float seconds)
    {
        given = seconds;
    }

    private float Patience => given > .01f ? given : patience > .01f ? patience : 45f;
    private float HappyReward => happyReward > .01f ? happyReward : 2f;
    private float AngryReward => angryReward > .01f ? angryReward : .5f;
    private float CelebrationScale => celebrationScale > .01f ? celebrationScale : 1.7f;
    private float CelebrationTime => celebrationTime > .01f ? celebrationTime : .25f;
    private float CelebrationHold => celebrationHold > .01f ? celebrationHold : 2f;
    private float AppearTime => appearTime > .01f ? appearTime : .25f;
    private float PopDelay => popDelay > .001f ? popDelay : .12f;
    private float FadeTime => fadeTime > .01f ? fadeTime : .3f;
    private float MoodPopScale => moodPopScale > 1.001f ? moodPopScale : 1.35f;
    private float MoodShakeTime => moodShakeTime > .01f ? moodShakeTime : .45f;
    private float MoodShakeAngle => moodShakeAngle > .01f ? moodShakeAngle : 18f;
    private float MoodShakeSpeed => moodShakeSpeed > .01f ? moodShakeSpeed : 3.5f;

    // Never later than the celebration itself, or the bubble closes on a label
    // that was supposed to leave it
    private float MoneyDelay => Mathf.Min(moneyDelay > .01f ? moneyDelay : .5f, CelebrationHold * .5f);
    private float MoneyFlightTime => moneyFlightTime > .01f ? moneyFlightTime : .55f;

    private float left;
    private bool running;
    private bool countdownSoundActive;
    private int lastCountdownDigit = int.MinValue;
    private float countdownPopAge = -1f;
    // One row per thing ordered. Row zero IS the authored anchor and label --
    // the ones sized and placed on the prefab -- and the rest are copies of
    // them. Building all of them from scratch would mean the card's own layout
    // lived in code, and then the prefab would be a lie
    private readonly List<Transform> rowAnchors = new List<Transform>();
    private readonly List<TextMeshPro> rowCounts = new List<TextMeshPro>();
    private readonly List<GameObject> rowIcons = new List<GameObject>();
    private readonly List<GameObject> rowTicks = new List<GameObject>();

    // Reading materials instantiates them. Doing that on every serve would
    // leave a copy behind per item handed over, so the darkening happens once
    // per row and this is what remembers that it has
    private readonly List<bool> rowDarkened = new List<bool>();

    // Where row zero sits when nothing has been moved. Captured once, because
    // every rebuild offsets from it and offsetting from an offset walks the
    // card off the side after three customers
    private Vector3 anchorHome;
    private Vector3 anchorHomeScale = Vector3.one;
    private Vector3 countHome;
    private Vector3 countHomeScale = Vector3.one;
    private bool homesCaptured;

    // The count rides on the picture rather than standing next to it. Scaled
    // with the row AND cut down again on top of that: at full size the number
    // competes with the food for the same glance, and the food is the thing
    // being read -- the number only qualifies it
    [Tooltip("Adet yazisinin boyu, satirin boyuna gore. 0 = varsayilan 0.65")]
    [SerializeField] private float countScale = .65f;

    // How far the number sits from the food it belongs to, as a fraction of
    // where the prefab put it. Below one pulls it in against the picture, which
    // is what makes it read as that food's number rather than as a number
    [Tooltip("Sayinin yemege yakinligi. 1 = prefabtaki yer, kucuk = daha yapisik. " +
             "0 = varsayilan 0.62")]
    [SerializeField] private float countHug = .62f;

    private float CountScale => countScale > .01f ? countScale : .65f;
    private float CountHug => countHug > .01f ? countHug : .62f;

    private bool celebrating;
    private float celebrationAge;

    // What this sale is worth, held only for as long as it takes the label to
    // reach the counter. Cleared the moment it is handed over so it can never
    // be paid twice
    private int owed;
    private bool moneySent;

    // Where the bubble is nailed. Still a child of the customer -- it belongs to
    // them and dies with them -- but its world position is overwritten every
    // frame instead of inherited
    private bool pinned;
    private bool detached;
    private Vector3 pinPosition;
    private Vector3 sunburstBaseScale = Vector3.one;
    private Vector3 earningsBaseScale = Vector3.one;
    private Vector3 homePosition;

    // How much higher this card floats than the one in front of it.
    //
    // Two customers standing apart on the floor are not two cards apart on the
    // SCREEN: the camera is isometric, so the card belonging to the row behind
    // projects straight onto the card in front of it however far back it stands.
    // Lifting each row clears them past each other in the one dimension the
    // camera does not flatten
    private float lift;
    private float displayScale = 1f;

    public void SetLift(float amount)
    {
        lift = amount;
    }

    public void SetDisplayScale(float amount)
    {
        displayScale = Mathf.Clamp(amount, .5f, 1f);
    }

    // Negative means "not running". A separate bool for each would be two
    // things to keep in step, and the age is what the animation reads anyway
    private float appearAge = -1f;
    private float fadeAge = -1f;
    private float shakeAge = -1f;

    // Nullable so the first face of a visit is not a change from something.
    // Opening the bubble with the emoji already shaking would announce a
    // transition that never happened
    private Mood? shown;

    // Captured rather than assumed, because the card is authored slightly
    // see-through and the fade has to end at nothing from THERE. Multiplying a
    // stored alpha keeps that; writing an alpha would snap the card to solid on
    // the way out, which is the opposite of fading
    private SpriteRenderer[] sprites;
    private float[] spriteAlpha;
    private TextMeshPro[] labels;
    private float[] labelAlpha;

    // Read off the prefab rather than assumed to be one. The emoji is scaled at
    // build time to fit the card, so growing it means growing THAT
    private Vector3 emojiBaseScale = Vector3.one;
    private Vector3 countdownBaseScale = Vector3.one;

    private Transform EmojiTransform => animatedEmojiRoot != null
        ? animatedEmojiRoot
        : emoji != null ? emoji.transform : null;

    // Frozen at the moment of the last handover. Reading the live clock when the
    // score is worked out would give whatever it had drained to by the time the
    // reward code ran, which is a different number from the one the player was
    // looking at when they earned it
    private Mood settled = Mood.Mutlu;
    private bool hasSettled;

    private float CountdownSeconds => countdownSeconds > .01f ? countdownSeconds : 5f;
    private float ZeroHold => zeroHold > .001f ? zeroHold : .5f;
    private float CountdownPopTime => countdownPopTime > .01f ? countdownPopTime : .22f;
    private float CountdownPopScale => countdownPopScale > 1.001f ? countdownPopScale : 1.5f;

    public float Remaining01 => Patience <= 0f ? 0f : Mathf.Clamp01(left / Patience);

    // Waited the whole way and got nothing. Read by the queue, which is the only
    // thing that knows where the door is and who to bill for it.
    //
    // Past zero, not at it. The clock keeps running below zero for the length
    // of the hold so the badge has somewhere to sit while it reads 0
    public bool RanOut => running && left <= -ZeroHold;

    // For the one line printed when somebody walks out. "A customer left" says
    // nothing about whether the clock was fair; the clock it was given and the
    // time it actually ran say everything.
    //
    // Capped at the patience itself: the hold is the badge being read, not the
    // customer waiting longer, and a line saying 20 of 19.5 reads as a bug
    public float PatienceGiven => Patience;
    public float Waited => Mathf.Clamp(Patience - left, 0f, Patience);

    public Mood CurrentMood
    {
        get
        {
            float fraction = Remaining01;

            if (fraction >= happyAbove)
                return Mood.Mutlu;

            return fraction <= angryBelow ? Mood.Kizgin : Mood.Idare;
        }
    }

    public Mood SettledMood => hasSettled ? settled : CurrentMood;

    private Color MoodColour
    {
        get
        {
            switch (CurrentMood)
            {
                case Mood.Mutlu: return calmColour;
                case Mood.Kizgin: return panicColour;
                default: return warnColour;
            }
        }
    }

    public float RewardMultiplier
    {
        get
        {
            switch (SettledMood)
            {
                case Mood.Mutlu: return HappyReward;
                case Mood.Kizgin: return AngryReward;
                default: return 1f;
            }
        }
    }

    private void Awake()
    {
        Transform emojiTransform = EmojiTransform;

        if (emojiTransform != null)
            emojiBaseScale = emojiTransform.localScale;

        if (sunburst != null)
            sunburstBaseScale = sunburst.localScale;

        if (earningsText != null)
            earningsBaseScale = earningsText.transform.localScale;

        if (countdownText != null)
            countdownBaseScale = countdownText.transform.localScale;

        // Where the bubble sits over this customer's head, read while it is
        // still a child. Once detached its local position is a world position,
        // and there would be nothing left to put it back to
        if (bubbleRoot != null)
            homePosition = bubbleRoot.transform.localPosition;

        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);
    }

    public void Show(OrderLine[] order)
    {
        if (bubbleRoot == null)
            return;

        bubbleRoot.SetActive(true);

        // Customers come out of a pool, so this is the second life of a bubble
        // that may have finished the first one mid-celebration. Undone here
        // rather than at the end of the celebration, which never runs to an end
        // -- the customer is destroyed on the way out
        ResetBubble();

        left = Patience;
        StopCountdownSound();
        running = true;
        hasSettled = false;

        BuildRows(order);
        Apply();

        // Before Pin, which nails the card to wherever it is standing. Set from
        // home rather than added to the current position, so a pooled bubble
        // does not climb one lift higher every time it is reused
        bubbleRoot.transform.localPosition = homePosition + Vector3.up * lift;

        Pin();

        // After BuildIcon, because the food's picture is made at runtime and is
        // not in the prefab to be found any earlier
        CaptureRenderers();

        // Opened from nothing. Set AFTER Pin, which detaches the bubble and
        // clears its scale on the way past
        appearAge = 0f;
        fadeAge = -1f;

        // This is the exact beginning of the order bubble's entrance, not the
        // earlier frame where the customer prefab was spawned. The sound and
        // the visible pop therefore land together once per order.
        SoundManager.Play(SoundManager.Sound.OrderBubbleOpened);

        bubbleRoot.transform.localScale = Vector3.zero;

        Transform emojiTransform = EmojiTransform;

        if (emojiTransform != null)
            emojiTransform.localScale = Vector3.zero;

        SetAlpha(1f);
    }

    // Nailed to the spot rather than carried on their head.
    //
    // A bubble parented to a walking character inherits everything the walk
    // does: the bob of the step, the lean into a turn, the slide as the queue
    // shuffles up. Three of them drifting past each other is not readable, and
    // the card is information -- it has to hold still long enough to be read.
    //
    // Re-pinned rather than pinned once, because the queue moves people forward
    // and the bubble should follow them THERE, just not on the way
    public void Pin()
    {
        if (bubbleRoot == null)
            return;

        pinPosition = bubbleRoot.transform.position;
        pinned = true;

        Detach();
    }

    // Lifted out of the customer's hierarchy the first time it opens.
    //
    // The position was already being overwritten every frame, so the parent was
    // contributing nothing but its rotation and its scale -- and a non-uniform
    // scale anywhere above a child SHEARS it. That is what made a rectangle
    // render as a parallelogram, and no amount of straightening the card itself
    // would have found it. Detached, the bubble owns its own transform outright
    private void Detach()
    {
        if (detached || bubbleRoot == null)
            return;

        detached = true;

        bubbleRoot.transform.SetParent(null, true);

        // Whatever the parent's scale was is now baked into this. Cleared, so
        // every customer's bubble is the one size it was authored at
        bubbleRoot.transform.localScale = Vector3.one * displayScale;
    }

    // It is nobody's child now, so nothing else will clean it up
    private void OnDestroy()
    {
        StopCountdownSound();

        if (detached && bubbleRoot != null)
            Destroy(bubbleRoot);

        // The graceful way out is Hide, which pays on its way. This is the
        // other one: a customer who reaches the door and is destroyed while the
        // number is still sitting on their bubble. The sale happened either
        // way. Guarded on the manager so a scene being torn down at the end of
        // play does not try to build a counter to pay into
        if (moneySent || owed <= 0 || CurrencyManager.instance == null)
            return;

        moneySent = true;
        MoneyCounter.Instance.Deposit(owed);
        owed = 0;
    }

    // A pooled customer switched off would otherwise leave their card hanging
    // over an empty spot
    private void OnDisable()
    {
        StopCountdownSound();

        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);
    }

    // LateUpdate, so it overwrites whatever the navigation agent and the
    // animator did this frame rather than being overwritten by them
    private void LateUpdate()
    {
        if (pinned && bubbleRoot != null)
            bubbleRoot.transform.position = pinPosition;
    }

    // Not called Reset. That is a MonoBehaviour message the editor fires when
    // the component is added, and taking the name would have quietly bound this
    // to a menu item nobody meant to connect it to
    private void ResetBubble()
    {
        celebrating = false;
        celebrationAge = 0f;

        // Anything still owed was paid by Hide on the way out. Cleared rather
        // than carried, so a reused bubble cannot pay the last order again
        owed = 0;
        moneySent = false;

        Switch(card, true);
        SwitchRows(true);
        Switch(timerRing, true);
        Switch(countdownText, true);
        Switch(countdownDisc, true);

        Switch(sunburst, false);
        Switch(earningsText, false);

        shown = null;
        shakeAge = -1f;
        lastCountdownDigit = int.MinValue;
        countdownPopAge = -1f;

        if (countdownText != null)
            countdownText.transform.localScale = countdownBaseScale;

        Transform emojiTransform = EmojiTransform;

        ResetAnimatedMood();

        if (emojiTransform != null)
        {
            emojiTransform.localScale = emojiBaseScale;
            emojiTransform.localRotation = Quaternion.identity;
        }

        if (sunburst != null)
            sunburst.localScale = sunburstBaseScale;

        if (earningsText != null)
            earningsText.transform.localScale = earningsBaseScale;
    }

    private static void Switch(Component target, bool on)
    {
        if (target != null)
            target.gameObject.SetActive(on);
    }

    private static void Switch(GameObject target, bool on)
    {
        if (target != null)
            target.SetActive(on);
    }

    // Always shown, including x1. Hiding it for a single item meant the number
    // appeared and disappeared depending on the order, so its absence had to be
    // read as "one" -- which is a thing you can only know by having been told.
    //
    // A filled row keeps its picture and gets a tick over it.
    //
    // It used to disappear. That left a card showing only what was still owed,
    // which is correct and reads as the order having changed -- the player
    // hands over a burger and the burger is simply gone from the card. Ticking
    // it says the same thing about what is left while also saying that the
    // thing they just did was the right one
    public void SetCounts(int[] left)
    {
        for (int i = 0; i < rowAnchors.Count; i++)
        {
            int value = left != null && i < left.Length ? left[i] : 0;
            bool owing = value > 0;

            if (i < rowCounts.Count && rowCounts[i] != null)
            {
                // The number goes; a count of nothing is not a count.
                //
                // A row that never had a number keeps not having one: BuildRows
                // switched it off because the row names no food, and turning it
                // back on here would put a count beside the question mark the
                // moment anything else on the card changed
                bool numbered = rowCounts[i].gameObject.activeSelf || owing;

                rowCounts[i].gameObject.SetActive(owing && numbered);

                if (owing && numbered)
                    rowCounts[i].text = value + "x";
            }

            if (i < rowTicks.Count && rowTicks[i] != null)
                rowTicks[i].SetActive(!owing);

            // Once, on the frame the row is finished. The colour is absolute so
            // doing it twice would look the same -- it is the material copies
            // that would pile up
            if (!owing && i < rowDarkened.Count && !rowDarkened[i])
            {
                rowDarkened[i] = true;

                Darken(i < rowIcons.Count ? rowIcons[i] : null,
                    i < rowTicks.Count ? rowTicks[i] : null);

                // The food pops on the same frame the tick lands on it. The
                // tick animates itself, because it is switched on and PopIn
                // hangs off that -- the picture underneath is never switched,
                // it is greyed out where it stands, so it has to be asked
                if (i < rowIcons.Count && rowIcons[i] != null)
                    PopIn.Play(rowIcons[i]);
            }

            if (rowAnchors[i] != null)
                rowAnchors[i].gameObject.SetActive(true);
        }
    }

    // Sprites carry their colour on the renderer, models on their materials,
    // and this bubble draws food both ways. The material path is the same one
    // burning uses: an instanced copy, and whichever colour property the shader
    // actually declares -- writing one the shader does not have is a silent
    // no-op, which is how a tint goes missing without an error
    // The tick is handed in so it can be stepped over.
    //
    // Today it cannot be reached from here anyway: BuildRows makes the tick a
    // SIBLING of the picture, both parented to the row's anchor, and the walks
    // below only ever go downwards from the icon. So this is a guard against a
    // change nobody has made yet -- and it is worth the four lines, because the
    // failure it prevents is silent and specific. Parent the tick under the
    // picture for any reason at all and it starts being greyed out along with
    // the dinner, which is exactly backwards: grey is the thing the tick exists
    // to contradict. Its edge is a child of the tick, so it is covered too
    private void Darken(GameObject icon, GameObject tick)
    {
        if (icon == null)
            return;

        foreach (SpriteRenderer flat in icon.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (BelongsToTick(flat.transform, tick))
                continue;

            Color faded = servedTint;

            // Multiplied into whatever the sprite already had rather than
            // replacing it. A picture drawn at half alpha does not become more
            // solid by being marked done
            faded.a = flat.color.a * servedTint.a;
            flat.color = faded;
        }

        foreach (Renderer solid in icon.GetComponentsInChildren<Renderer>(true))
        {
            if (solid is SpriteRenderer || solid is ParticleSystemRenderer)
                continue;

            if (BelongsToTick(solid.transform, tick))
                continue;

            Material[] materials = solid.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                if (material.HasProperty(baseColourId))
                    material.SetColor(baseColourId, servedTint);

                if (material.HasProperty(colourId))
                    material.SetColor(colourId, servedTint);

                if (material.HasProperty(tintId))
                    material.SetColor(tintId, servedTint);
            }

            solid.materials = materials;
        }
    }

    // IsChildOf counts the transform itself as one of its own children, so this
    // catches the tick as well as its edge
    private static bool BelongsToTick(Transform what, GameObject tick)
    {
        return tick != null && what != null && what.IsChildOf(tick.transform);
    }

    private static readonly int baseColourId = Shader.PropertyToID("_BaseColor");
    private static readonly int colourId = Shader.PropertyToID("_Color");
    private static readonly int tintId = Shader.PropertyToID("_Tint");

    // Lays the order out across the card and puts a picture in each row.
    //
    // Two rows are drawn smaller and pushed apart from the middle, so a pair
    // sits where a single one did rather than growing out past the card
    private void BuildRows(OrderLine[] order)
    {
        CaptureHomes();
        ClearRows();

        int rows = order == null ? 0 : order.Length;

        if (rows <= 0)
            return;

        // Two is still full size.
        //
        // A pair used to be dropped to three quarters and it did not need to
        // be. The card is 1.75 wide, an icon is .595, and two of them a step
        // apart span about 1.4 of it -- so the shrink was buying margin that
        // was already sitting there, and paying for it in the one thing the
        // picture is for. A customer's order has to be readable in the half
        // second they are glanced at, and a food shrunk to three quarters and
        // then seen from across an isometric kitchen is a coloured blob.
        //
        // Three rows genuinely do overflow, so three is where shrinking starts
        float shrink = rows <= 2 ? 1f : .62f;

        // Centre to centre. A third of an icon is left between the two, which
        // is a gap at this size -- 1.5 was set when the pictures were small
        // enough not to care, and at full size it pushed the pair out to the
        // card's edges
        float step = anchorHomeScale.x * shrink * 1.35f;

        // Every anchor cloned BEFORE any picture goes into one.
        //
        // Building row by row cloned row two off an anchor that row one had
        // already put its food into, so row two arrived wearing row one's
        // dinner with its own laid over the top. The template has to still be
        // empty when it is copied, and the only way to be sure of that is to do
        // all the copying first
        // Refused as a whole rather than row by row. Skipping a row midway
        // would leave rowAnchors shorter than the order, and every list after
        // it addressed by the same index -- which is the wrong picture on the
        // wrong row, the exact thing this method is being fixed for
        if (iconAnchor == null)
            return;

        for (int i = 0; i < rows; i++)
        {
            Transform anchor = i == 0 ? iconAnchor : CopyOf(iconAnchor);
            TextMeshPro label = i == 0 ? countText : CopyOf(countText);

            // Centred on the card: two rows straddle the middle, three put one
            // in it. The same arithmetic for any number of them
            float offset = (i - (rows - 1) * .5f) * step;

            anchor.localPosition = anchorHome + Vector3.right * offset;
            anchor.localScale = anchorHomeScale * shrink;
            anchor.gameObject.SetActive(true);

            if (label != null)
            {
                // Measured from the anchor, not from the card. The prefab puts
                // the number down and to the right of the food; keeping that
                // relationship means scaling the GAP with the row, and the gap
                // was the one thing left at full size -- which is why the
                // number ended up floating away from the picture it counts
                Vector3 gap = (countHome - anchorHome) * (shrink * CountHug);

                label.transform.localPosition = anchorHome + Vector3.right * offset + gap;
                label.transform.localScale = countHomeScale * (shrink * CountScale);
                label.text = Mathf.Max(1, order[i].count) + "x";

                // No number on a row that names no food. "1x" beside a question
                // mark is a quantity of nothing in particular, and the whole
                // point of that row is that the quantity is not the question
                label.gameObject.SetActive(order[i].food != null);
            }

            rowAnchors.Add(anchor);
            rowCounts.Add(label);
            rowDarkened.Add(false);
        }

        // Second pass. Nothing above this line adds a child to an anchor, so
        // every clone above was taken from an empty one
        for (int i = 0; i < rowAnchors.Count; i++)
        {
            rowIcons.Add(BuildIcon(order[i].food, rowAnchors[i]));
            rowTicks.Add(BuildTick(rowAnchors[i]));
        }
    }

    // The bubble's stack, from the front: countdown 130, disc 125, emoji 120,
    // ring 119, earnings 118, TICK 117, its white edge 116, count 115, food
    // 110, sunburst 105, card 90.
    //
    // The tick moved up a step so its own edge had somewhere to go that was not
    // on top of the count. Both stay under the earnings, which has to stay
    // legible over everything else on the card
    private const int tickOrder = 117;
    private const int tickBorderOrder = 116;

    // Built with the row and switched off, not made at the moment it is needed.
    // The moment it is needed is the frame a plate changes hands, and building
    // a sprite in that frame is a hitch exactly where the game is asking to
    // feel responsive
    private GameObject BuildTick(Transform anchor)
    {
        if (readyIcon == null || anchor == null)
            return null;

        GameObject tick = new GameObject("Tick");

        tick.transform.SetParent(anchor, false);

        // Well clear of the food, not a hair in front of it.
        //
        // "Between two transparent renderers the order decides, not the
        // distance" was true and was only half the picture. Half the foods in
        // this game are drawn as sprites, and for those the sorting order below
        // settles everything. The other half are MODELS -- opaque geometry,
        // with real depth to them. Sorting order does not arbitrate between an
        // opaque mesh and a sprite at all: the mesh draws first and writes the
        // depth buffer, and a sprite sitting behind that plane fails the test
        // however high its order is. At -.06 the tick was inside the dinner.
        //
        // FitIcon sizes every food to at most one anchor unit across, so half
        // of one is the furthest forward any of them can reach, and .6 clears
        // that with room to spare. It costs nothing on screen either: the
        // kitchen camera is orthographic, so travelling along its axis changes
        // depth without changing position or size
        tick.transform.localPosition = new Vector3(0f, 0f, -.6f);
        tick.transform.localRotation = Quaternion.identity;

        SpriteRenderer mark = tick.AddComponent<SpriteRenderer>();

        mark.sprite = readyIcon;
        mark.color = readyTint;
        mark.sortingOrder = tickOrder;

        float own = mark.bounds.size.y;

        if (own > .0001f)
            tick.transform.localScale = Vector3.one * (anchor.lossyScale.y * .85f / own);

        // The white edge, built after the scale so it inherits it.
        //
        // White is written here rather than exposed as a field on purpose. A
        // Color added to a component that is already saved inside a prefab
        // deserialises to (0,0,0,0) -- transparent black -- so every customer
        // that already exists would come back with an invisible border, and the
        // bug would look exactly like the feature having never worked
        SpriteOutline.Build(tick, readyIcon, tickBorderOrder);

        // After the scale and the edge, and before the first SetActive(false).
        //
        // Order matters twice over: PopIn learns its home scale on its first
        // enable, so it has to be added once the tick is the size it will stay,
        // and the edge has to exist first or it would be built at whatever
        // fraction of the scale the animation happened to be showing
        PopIn.Ensure(tick);

        tick.SetActive(false);

        return tick;
    }

    private void CaptureHomes()
    {
        if (homesCaptured)
            return;

        homesCaptured = true;

        if (iconAnchor != null)
        {
            anchorHome = iconAnchor.localPosition;
            anchorHomeScale = iconAnchor.localScale;
        }

        if (countText != null)
        {
            countHome = countText.transform.localPosition;
            countHomeScale = countText.transform.localScale;
        }
    }

    // Row zero is kept and put back where it started. Only the copies are
    // destroyed -- taking the authored one out would empty the prefab
    private void ClearRows()
    {
        // Unparented BEFORE being destroyed, and that is the whole bug.
        //
        // Destroy is deferred to the end of the frame, so a marked object is
        // still a child right now -- and the very next thing this does is clone
        // the anchor for row two. The clone came with a copy of the icon that
        // was on its way out, so the second row wore the last customer's food
        // and its own on top of it. Unparenting takes effect immediately
        for (int i = 0; i < rowIcons.Count; i++)
        {
            if (rowIcons[i] == null)
                continue;

            rowIcons[i].transform.SetParent(null, false);
            Destroy(rowIcons[i]);
        }

        for (int i = 1; i < rowAnchors.Count; i++)
        {
            if (rowAnchors[i] != null)
                Destroy(rowAnchors[i].gameObject);
        }

        for (int i = 1; i < rowCounts.Count; i++)
        {
            if (rowCounts[i] != null)
                Destroy(rowCounts[i].gameObject);
        }

        // Ticks go with their icons: both are children of the anchor, and row
        // zero's anchor survives, so its children have to be cleared by hand
        // Same treatment, same reason: a tick still parented to the anchor gets
        // cloned along with it
        for (int i = 0; i < rowTicks.Count; i++)
        {
            if (rowTicks[i] == null)
                continue;

            rowTicks[i].transform.SetParent(null, false);
            Destroy(rowTicks[i]);
        }

        rowIcons.Clear();
        rowAnchors.Clear();
        rowCounts.Clear();
        rowTicks.Clear();
        rowDarkened.Clear();

        if (iconAnchor != null)
        {
            // Anything still under row zero that this did not put there. A
            // build cut short leaves children nobody is tracking, and from then
            // on every clone carries them into every row
            for (int i = iconAnchor.childCount - 1; i >= 0; i--)
            {
                Transform stray = iconAnchor.GetChild(i);

                stray.SetParent(null, false);
                Destroy(stray.gameObject);
            }

            iconAnchor.localPosition = anchorHome;
            iconAnchor.localScale = anchorHomeScale;
        }

        if (countText != null)
        {
            countText.transform.localPosition = countHome;
            countText.transform.localScale = countHomeScale;
        }
    }

    private T CopyOf<T>(T source) where T : Component
    {
        return source == null ? null : Instantiate(source, source.transform.parent);
    }

    // Every row at once. The celebration hides the order and the reset brings
    // it back, and both used to know about exactly one anchor and one label
    private void SwitchRows(bool on)
    {
        for (int i = 0; i < rowAnchors.Count; i++)
        {
            if (rowAnchors[i] != null)
                rowAnchors[i].gameObject.SetActive(on);
        }

        for (int i = 0; i < rowCounts.Count; i++)
        {
            if (rowCounts[i] != null)
                rowCounts[i].gameObject.SetActive(on);
        }
    }

    // Stops the clock where it is and remembers the mood. Called on the last
    // item rather than on every one: a customer who waited is a customer who
    // waited, whatever happened after the plate landed
    public void Settle()
    {
        settled = CurrentMood;
        hasSettled = true;
        running = false;
        StopCountdownSound();
    }

    // The order is finished and paid for. Everything the card was asking has
    // been answered by now, so it goes, and the face they are leaving with is
    // what stays -- blown up, on a spinning burst, with what it earned under it.
    //
    // The number comes from outside because the bubble has no way to know it:
    // the mood is worked out here, but base revenue and the player's own upgrade
    // multiplier both live at the till
    // Answers whether it took the job. A bubble that cannot celebrate cannot
    // carry the money either, and the till has to know that so the sale is not
    // dropped between the two of them
    public bool Celebrate(int earnings)
    {
        if (bubbleRoot == null || celebrating)
            return false;

        // A handover that somehow skipped Settle would score off a clock still
        // running, and the emoji would keep sinking while they walk out
        if (!hasSettled)
            Settle();

        celebrating = true;
        celebrationAge = 0f;
        running = false;

        // Held, not banked. The player reads the number here and it lands in
        // the counter there, and the money moving when the number moves is the
        // whole point -- paying now would put it in before anything left
        owed = earnings;
        moneySent = false;

        Switch(card, false);
        SwitchRows(false);
        // The ring is not a child of the card on every customer prefab. Turning
        // the card off therefore left the actual RadialTimer alive behind the
        // celebration face; as the emoji shrank, the old patience bar was
        // revealed. Disable the component's whole GameObject explicitly and
        // only restore it when ResetBubble opens a new order.
        Switch(timerRing, false);
        Switch(countdownText, false);
        Switch(countdownDisc, false);

        // Opened at nothing, so the first frame of the pop is a pop and not a
        // full sized burst appearing and then growing
        if (sunburst != null)
        {
            sunburst.localScale = Vector3.zero;
            sunburst.gameObject.SetActive(true);
        }

        if (earningsText != null)
        {
            earningsText.gameObject.SetActive(true);
            earningsText.text = "$" + earnings;
            earningsText.transform.localScale =
                earningsBaseScale * celebrationMoneyBoost;
        }

        // The face already reacted once while the customer was waiting. The
        // completed order is a new event, so give the currently visible emoji
        // exactly one fresh cycle as the sunburst opens behind it.
        ReplayAnimatedEmoji(animatedEmojiRoot != null
            ? animatedEmojiRoot.gameObject
            : null);

        return true;
    }

    // Shrinks and fades rather than blinking out. Whatever ends it -- the
    // celebration finishing, the customer giving up and leaving -- it is the
    // end of something the player was reading, and things that were being read
    // should be seen to go
    public void Hide()
    {
        running = false;
        celebrating = false;
        StopCountdownSound();

        // Whatever brought the bubble down early -- a customer sent home, a
        // scene torn out from under it -- the sale still happened. Paid without
        // the flight rather than not paid
        if (!moneySent && owed > 0)
        {
            moneySent = true;
            MoneyCounter.Instance.Deposit(owed);
            owed = 0;
        }

        if (bubbleRoot == null || !bubbleRoot.activeSelf)
        {
            pinned = false;
            return;
        }

        if (fadeAge < 0f)
            fadeAge = 0f;
    }

    private void Update()
    {
        if (fadeAge >= 0f)
        {
            Fading();
            return;
        }

        if (appearAge >= 0f)
            Appearing();

        if (celebrating)
        {
            Celebration();
            return;
        }

        if (!running)
            return;

        // Allowed below zero now, and stopped at the bottom of the hold rather
        // than at zero. Clamping here was what made the badge unable to show a
        // zero: the value it would have printed never existed for a whole frame
        left = Mathf.Max(-ZeroHold, left - Time.deltaTime);

        UpdateCountdownSound();
        Apply();
        AnimateCountdownPop();
        StopAnimatedEmojiAfterOnePlay();

        // After Apply, which is what notices the change and starts it. Skipped
        // while the bubble is still opening -- two things driving one scale in
        // the same frame is a fight, and the arrival should win it
        if (shakeAge >= 0f && appearAge < 0f)
            Shake();
    }

    private void UpdateCountdownSound()
    {
        bool wanted = left > 0f && left <= CountdownSeconds;

        if (wanted == countdownSoundActive)
            return;

        countdownSoundActive = wanted;
        SoundManager.SetPatienceCountdown(this, wanted);
    }

    private void StopCountdownSound()
    {
        if (!countdownSoundActive)
            return;

        countdownSoundActive = false;
        SoundManager.SetPatienceCountdown(this, false);
    }

    private void AnimateCountdownPop()
    {
        if (countdownText == null || countdownPopAge < 0f)
            return;

        countdownPopAge += Time.deltaTime;

        float t = Mathf.Clamp01(countdownPopAge / CountdownPopTime);
        float pulse = Mathf.Sin(t * Mathf.PI);

        countdownText.transform.localScale = countdownBaseScale *
            (1f + (CountdownPopScale - 1f) * pulse);

        if (t < 1f)
            return;

        countdownPopAge = -1f;
        countdownText.transform.localScale = countdownBaseScale;
    }

    // The purchased emoji clips are authored as loops. A mood is a reaction,
    // not a permanent fidget: play its first cycle, hold its final pose and
    // only restart when the mood genuinely changes.
    private void StopAnimatedEmojiAfterOnePlay()
    {
        if (animatedEmojiRoot == null)
            return;

        Animator[] animators = animatedEmojiRoot.GetComponentsInChildren<Animator>(false);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator face = animators[i];

            if (face == null || face.speed <= 0f || face.layerCount <= 0)
                continue;

            AnimatorStateInfo state = face.GetCurrentAnimatorStateInfo(0);

            if (state.normalizedTime < .985f)
                continue;

            face.Play(state.fullPathHash, 0, .999f);
            face.Update(0f);
            face.speed = 0f;
        }
    }

    // The card lands, then the face. Two things popping on the same frame read
    // as one thing; a beat between them is what makes it an arrival
    private void Appearing()
    {
        appearAge += Time.deltaTime;

        float span = AppearTime;

        bubbleRoot.transform.localScale = Vector3.one * displayScale *
            Overshoot(Mathf.Clamp01(appearAge / span));

        Transform emojiTransform = EmojiTransform;

        if (emojiTransform != null)
        {
            float face = Mathf.Clamp01((appearAge - PopDelay) / span);

            emojiTransform.localScale = emojiBaseScale * Overshoot(face);
        }

        if (appearAge < span + PopDelay)
            return;

        appearAge = -1f;

        bubbleRoot.transform.localScale = Vector3.one * displayScale;

        if (emojiTransform != null)
            emojiTransform.localScale = emojiBaseScale;
    }

    private void Fading()
    {
        fadeAge += Time.deltaTime;

        float t = Mathf.Clamp01(fadeAge / FadeTime);

        // Shrinking as well as fading, because the ring is built from opaque
        // quads and there is no alpha on them to turn down. Scale is the one
        // handle every kind of renderer answers to
        bubbleRoot.transform.localScale = Vector3.one * displayScale * (1f - t);

        SetAlpha(1f - t);

        if (t < 1f)
            return;

        fadeAge = -1f;
        pinned = false;

        bubbleRoot.SetActive(false);

        SetAlpha(1f);

        Reattach();
    }

    // Put back under the customer once it is off screen.
    //
    // Customers come from a pool, so one that is switched off rather than
    // destroyed never runs OnDestroy -- and a detached bubble has no parent to
    // be switched off with. They were piling up at the root of the scene, one
    // per customer ever spawned, invisible and doing nothing but being there
    private void Reattach()
    {
        if (!detached || bubbleRoot == null)
            return;

        detached = false;

        Transform bubble = bubbleRoot.transform;

        bubble.SetParent(transform, false);

        // Back to the pose read off the prefab. SetParent keeps whatever local
        // values it had, and after a detach those are world coordinates
        bubble.localPosition = homePosition;
        bubble.localRotation = Quaternion.identity;
        bubble.localScale = Vector3.one;
    }

    private void CaptureRenderers()
    {
        sprites = bubbleRoot.GetComponentsInChildren<SpriteRenderer>(true);
        spriteAlpha = new float[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
            spriteAlpha[i] = sprites[i].color.a;

        labels = bubbleRoot.GetComponentsInChildren<TextMeshPro>(true);
        labelAlpha = new float[labels.Length];

        for (int i = 0; i < labels.Length; i++)
            labelAlpha[i] = labels[i].color.a;
    }

    private void SetAlpha(float factor)
    {
        if (sprites != null)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                    continue;

                Color colour = sprites[i].color;

                colour.a = spriteAlpha[i] * factor;

                sprites[i].color = colour;
            }
        }

        if (labels == null)
            return;

        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;

            Color colour = labels[i].color;

            colour.a = labelAlpha[i] * factor;

            labels[i].color = colour;
        }
    }

    // Out and back rather than out and stay.
    //
    // Three beats: it pops open, it holds long enough to be read, and it goes
    // back to the size it was before disappearing. The emoji ending at its
    // normal size is what makes it a reaction rather than a state -- something
    // happened, and then it was over
    private void Celebration()
    {
        celebrationAge += Time.deltaTime;
        StopAnimatedEmojiAfterOnePlay();

        float hold = CelebrationHold;

        // Never more than a fifth of the whole thing at each end, so a short
        // hold cannot end up with no still moment in the middle of it
        float ramp = Mathf.Min(CelebrationTime, hold * .2f);

        float t;

        if (celebrationAge <= ramp)
        {
            t = Overshoot(celebrationAge / ramp);
        }
        else if (celebrationAge < hold - ramp)
        {
            t = 1f;
        }
        else if (celebrationAge < hold)
        {
            t = 1f - Mathf.Clamp01((celebrationAge - (hold - ramp)) / ramp);
        }
        else
        {
            Hide();
            return;
        }

        Transform emojiTransform = EmojiTransform;

        if (emojiTransform != null)
            emojiTransform.localScale = emojiBaseScale * (1f + (CelebrationScale - 1f) * t);

        // Long enough after the pop to have been read, early enough that the
        // burst is still up behind it. The number has to be seen leaving --
        // if the bubble closes first there is nothing to watch go anywhere
        if (!moneySent && celebrationAge >= MoneyDelay)
            SendMoney();

        if (sunburst == null)
            return;

        // A beat behind the face, same as the card and the face on the way in.
        // Held at nothing until the emoji has started, then on the same curve
        sunburst.localScale = celebrationAge < PopDelay
            ? Vector3.zero
            : sunburstBaseScale * (celebrationSunburstBoost * t);

        if (Mathf.Abs(sunburstSpin) > .001f)
            sunburst.Rotate(0f, 0f, sunburstSpin * Time.deltaTime, Space.Self);
    }

    // The number leaves the bubble and goes to the counter.
    //
    // A copy flies and the original switches off in the same frame, which reads
    // as the label itself lifting off. Sending the real one would take it out
    // of the bubble -- the card is built once per customer prefab, and the
    // label has to still be there for the next order this customer takes.
    //
    // The copy is made by the counter, as UI on a canvas above the whole
    // interface. This label is a world object and a world object cannot be
    // drawn over an overlay canvas at any sorting order there is
    private void SendMoney()
    {
        moneySent = true;

        int amount = owed;
        owed = 0;

        if (amount <= 0)
            return;

        // No sound here either. This is the number LAUNCHING; the money is not
        // in hand until it lands, a second later, and a till noise at take-off
        // announces a payment that has not happened yet. MoneyCounter.Deposit
        // is where the money actually arrives, and that is where it sounds
        //
        // Deposits on its own if the copy could not be made, so a sale is never
        // lost to a missing label
        MoneyCounter.Instance.FlyText(earningsText, amount, MoneyFlightTime, .06f);

        Switch(earningsText, false);
    }

    // Swells and rocks, then settles.
    //
    // Both halves fade out together on the same 1-to-0 term, so it lands back
    // at exactly the size and the angle it started at. An animation that
    // finishes near its start leaves a face very slightly crooked, and that
    // never corrects itself -- it only accumulates on the next change
    private void Shake()
    {
        Transform emojiTransform = EmojiTransform;

        if (emojiTransform == null)
        {
            shakeAge = -1f;
            return;
        }

        shakeAge += Time.deltaTime;

        float t = Mathf.Clamp01(shakeAge / MoodShakeTime);

        if (t >= 1f)
        {
            shakeAge = -1f;

            emojiTransform.localRotation = Quaternion.identity;
            emojiTransform.localScale = emojiBaseScale;

            return;
        }

        float dying = 1f - t;

        float wobble = Mathf.Sin(shakeAge * MoodShakeSpeed * Mathf.PI * 2f) *
                       MoodShakeAngle * dying;

        emojiTransform.localRotation = Quaternion.Euler(0f, 0f, wobble);

        // Up and back down on one hump, so there is no seam where a two stage
        // tween would hand over
        emojiTransform.localScale =
            emojiBaseScale * (1f + (MoodPopScale - 1f) * Mathf.Sin(t * Mathf.PI));
    }

    // Goes past the target size and settles back into it. A straight lerp
    // arrives at the right number and reads as a resize; the overshoot is the
    // part that reads as a reaction to something
    private static float Overshoot(float t)
    {
        const float back = 1.70158f;

        float past = t - 1f;

        return 1f + (back + 1f) * past * past * past + back * past * past;
    }

    private void Apply()
    {
        float fraction = Remaining01;

        // Green, amber, red -- read off the SAME thresholds the emoji uses.
        //
        // Two sets of numbers for the same question is two ways to disagree,
        // and a customer scowling next to a green counter is a bug nobody can
        // report because both halves look right on their own
        Color mood = MoodColour;

        if (timerRing != null)
        {
            timerRing.SetFill(fraction);
            timerRing.SetColour(mood);
        }

        // Real seconds now, and only at the end. The ring says how much of the
        // wait is left; this says how long you have, which is a different
        // question and only worth asking when the answer is small
        bool closing = left <= CountdownSeconds;

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(closing);

            // Zero included. Ceil holds each digit for the second it stands
            // for, so 1 is on screen through the whole last second and 0 only
            // arrives once the clock has passed it -- which is exactly what the
            // hold below zero is for. Clamped at the bottom so the badge never
            // shows a negative
            if (closing)
            {
                int digit = Mathf.Max(0, Mathf.CeilToInt(left));

                if (digit != lastCountdownDigit)
                {
                    lastCountdownDigit = digit;
                    countdownPopAge = 0f;
                    countdownText.transform.localScale = countdownBaseScale;
                }

                countdownText.text = digit.ToString();
            }
            else
            {
                lastCountdownDigit = int.MinValue;
                countdownPopAge = -1f;
                countdownText.transform.localScale = countdownBaseScale;
            }
        }

        if (countdownDisc != null)
        {
            countdownDisc.gameObject.SetActive(closing);
            countdownDisc.color = mood;
        }

        if (animatedEmojiRoot == null && emoji == null)
            return;

        Mood now = CurrentMood;

        if (animatedEmojiRoot != null)
        {
            SetAnimatedMood(now);
        }
        else
        {
            switch (now)
            {
                case Mood.Mutlu:
                    if (happySprite != null) emoji.sprite = happySprite;
                    break;

                case Mood.Kizgin:
                    if (angrySprite != null) emoji.sprite = angrySprite;
                    break;

                default:
                    if (neutralSprite != null) emoji.sprite = neutralSprite;
                    break;
            }
        }

        // Only a real change starts it, and only after the first one has been
        // seen. shown is null on the frame the bubble opens, so arriving with a
        // face is not the same event as changing to one
        if (shown.HasValue && shown.Value != now)
            shakeAge = 0f;

        shown = now;
    }

    private void SetAnimatedMood(Mood mood)
    {
        GameObject selected;

        switch (mood)
        {
            case Mood.Mutlu: selected = animatedHappyEmoji; break;
            case Mood.Kizgin: selected = animatedAngryEmoji; break;
            default: selected = animatedNeutralEmoji; break;
        }

        bool changed = !shown.HasValue || shown.Value != mood ||
                       selected == null || !selected.activeSelf;

        Switch(animatedHappyEmoji, selected == animatedHappyEmoji);
        Switch(animatedNeutralEmoji, selected == animatedNeutralEmoji);
        Switch(animatedAngryEmoji, selected == animatedAngryEmoji);

        if (!changed || selected == null)
            return;

        ReplayAnimatedEmoji(selected);
    }

    private static void ReplayAnimatedEmoji(GameObject root)
    {
        if (root == null)
            return;

        Animator[] animators = root.GetComponentsInChildren<Animator>(false);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator face = animators[i];

            if (face == null || face.runtimeAnimatorController == null)
                continue;

            face.Rebind();
            face.speed = 1f;
            face.Play(0, 0, 0f);
            face.Update(0f);
        }
    }

    private void ResetAnimatedMood()
    {
        Switch(animatedHappyEmoji, false);
        Switch(animatedNeutralEmoji, false);
        Switch(animatedAngryEmoji, false);
    }

    // The food's own model, shrunk. There are no 2D icons for these foods and
    // drawing some would be a second thing to keep in step with the prefabs --
    // this way a new food shows up in the bubble the day it is made
    // Takes the anchor it is building into rather than reading the field: with
    // two rows there are two anchors, and only one of them is the field
    private GameObject BuildIcon(SpawnableFood wanted, Transform iconAnchor)
    {
        GameObject icon;

        if (iconAnchor == null)
            return null;

        if (wanted == null)
        {
            // Not a fault any more. A row with no food named is somebody who
            // walked in without deciding, and the mark is what that looks like
            if (undecidedIcon != null)
                return Flat(undecidedIcon, undecidedTint, iconAnchor);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Only once there is nothing at all to draw. The bubble opens
            // either way, so this shows as a card with a count and a hole where
            // the food goes -- which reads as a broken bubble rather than as an
            // unwired counter
            Debug.LogWarning(name + ": siparis yok ve isaretsiz balon.\n" +
                             "  Customer Order > Undecided Icon bos, ve\n" +
                             "  FoodServingCustomerManager > Possible Orders da bos olabilir.\n" +
                             "  Cooked Fast > Musteri > Siparis Balonunu Kur", this);
#endif
            return null;
        }

        // A flat picture when the food has one, its own model when it does not.
        // The sprite is the better of the two on a card that always faces the
        // camera -- a model has to be lit, and lit by whatever happens to be
        // shining on the queue
        if (wanted.Icon != null)
            return Flat(wanted.Icon, Color.white, iconAnchor);

        icon = Instantiate(wanted.gameObject, iconAnchor);

        icon.transform.localPosition = Vector3.zero;
        icon.transform.localRotation = Quaternion.identity;

        // It is a picture of food, not food. Left as it comes it is a live
        // SpawnableFood that stations and the plateau would try to reason about
        foreach (MonoBehaviour behaviour in icon.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(behaviour);

        foreach (Collider collider in icon.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        foreach (Rigidbody body in icon.GetComponentsInChildren<Rigidbody>(true))
            Destroy(body);

        FitIcon(icon, iconAnchor);

        return icon;
    }

    // A drawn picture in the row, whatever it is a picture of. The food's own
    // icon and the undecided mark are the same job -- one sprite, fitted to the
    // anchor -- and having written it twice is how they drifted apart
    private GameObject Flat(Sprite picture, Color tint, Transform iconAnchor)
    {
        GameObject icon = new GameObject("Icon");

        icon.transform.SetParent(iconAnchor, false);
        icon.transform.localPosition = Vector3.zero;
        icon.transform.localRotation = Quaternion.identity;

        SpriteRenderer flat = icon.AddComponent<SpriteRenderer>();

        flat.sprite = picture;
        flat.color = tint;
        flat.sortingOrder = 110;

        // The WIDER of the two, not the height.
        //
        // Scaling a wide picture by its height leaves it wide: a fries box
        // fitted to the anchor's height came out half again as broad as the
        // anchor, and with two rows on the card that is a picture reaching into
        // its neighbour. The drink was not inside the burger, it was behind it
        float own = Mathf.Max(flat.bounds.size.x, flat.bounds.size.y);

        if (own > .0001f)
            icon.transform.localScale = Vector3.one * (iconAnchor.lossyScale.y / own);

        return icon;
    }

    // Scaled to the anchor's own size, so how big the picture is stays a thing
    // decided by dragging the anchor rather than by a number in here
    private void FitIcon(GameObject icon, Transform iconAnchor)
    {
        Bounds bounds = default;

        bool any = false;

        foreach (Renderer renderer in icon.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        if (!any)
            return;

        float biggest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        if (biggest <= .0001f)
            return;

        float wanted = iconAnchor.lossyScale.x;

        icon.transform.localScale *= wanted / biggest;

        // Re-measured and re-centred: a food prefab's pivot is wherever it sat
        // on a plate, which is not the middle of its own mesh
        Bounds after = new Bounds(icon.transform.position, Vector3.zero);

        bool second = false;

        foreach (Renderer renderer in icon.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
                continue;

            if (!second)
            {
                after = renderer.bounds;
                second = true;
                continue;
            }

            after.Encapsulate(renderer.bounds);
        }

        if (second)
            icon.transform.position += iconAnchor.position - after.center;
    }
}
