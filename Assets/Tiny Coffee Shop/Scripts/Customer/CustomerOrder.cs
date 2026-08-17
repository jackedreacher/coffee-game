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

    [SerializeField] private SpriteRenderer emoji;

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

    [Tooltip("Emoji kac katina buyusun. 0 = varsayilan 1.7")]
    [SerializeField] private float celebrationScale = 1.7f;

    [Tooltip("Buyume ve kucultme suresi, saniye. 0 = varsayilan 0.25")]
    [SerializeField] private float celebrationTime = .25f;

    [Tooltip("Kutlama bastan sona kac saniye durur. 0 = varsayilan 2")]
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
    private GameObject icon;

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
    private Vector3 homePosition;

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

    // Frozen at the moment of the last handover. Reading the live clock when the
    // score is worked out would give whatever it had drained to by the time the
    // reward code ran, which is a different number from the one the player was
    // looking at when they earned it
    private Mood settled = Mood.Mutlu;
    private bool hasSettled;

    private float CountdownSeconds => countdownSeconds > .01f ? countdownSeconds : 5f;
    private float ZeroHold => zeroHold > .001f ? zeroHold : .5f;

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
        if (emoji != null)
            emojiBaseScale = emoji.transform.localScale;

        if (sunburst != null)
            sunburstBaseScale = sunburst.localScale;

        // Where the bubble sits over this customer's head, read while it is
        // still a child. Once detached its local position is a world position,
        // and there would be nothing left to put it back to
        if (bubbleRoot != null)
            homePosition = bubbleRoot.transform.localPosition;

        if (bubbleRoot != null)
            bubbleRoot.SetActive(false);
    }

    public void Show(SpawnableFood wanted, int count)
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
        running = true;
        hasSettled = false;

        SetCount(count);
        BuildIcon(wanted);
        Apply();

        Pin();

        // After BuildIcon, because the food's picture is made at runtime and is
        // not in the prefab to be found any earlier
        CaptureRenderers();

        // Opened from nothing. Set AFTER Pin, which detaches the bubble and
        // clears its scale on the way past
        appearAge = 0f;
        fadeAge = -1f;

        bubbleRoot.transform.localScale = Vector3.zero;

        if (emoji != null)
            emoji.transform.localScale = Vector3.zero;

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
        bubbleRoot.transform.localScale = Vector3.one;
    }

    // It is nobody's child now, so nothing else will clean it up
    private void OnDestroy()
    {
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
        Switch(iconAnchor, true);
        Switch(countText, true);
        Switch(countdownText, true);
        Switch(countdownDisc, true);

        Switch(sunburst, false);
        Switch(earningsText, false);

        shown = null;
        shakeAge = -1f;

        if (emoji != null)
        {
            emoji.transform.localScale = emojiBaseScale;
            emoji.transform.localRotation = Quaternion.identity;
        }

        if (sunburst != null)
            sunburst.localScale = sunburstBaseScale;
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
    // read as "one" -- which is a thing you can only know by having been told
    public void SetCount(int count)
    {
        if (countText != null)
            countText.text = Mathf.Max(1, count) + "x";
    }

    // Stops the clock where it is and remembers the mood. Called on the last
    // item rather than on every one: a customer who waited is a customer who
    // waited, whatever happened after the plate landed
    public void Settle()
    {
        settled = CurrentMood;
        hasSettled = true;
        running = false;
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
        Switch(iconAnchor, false);
        Switch(countText, false);
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
        }

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

        Apply();

        // After Apply, which is what notices the change and starts it. Skipped
        // while the bubble is still opening -- two things driving one scale in
        // the same frame is a fight, and the arrival should win it
        if (shakeAge >= 0f && appearAge < 0f)
            Shake();
    }

    // The card lands, then the face. Two things popping on the same frame read
    // as one thing; a beat between them is what makes it an arrival
    private void Appearing()
    {
        appearAge += Time.deltaTime;

        float span = AppearTime;

        bubbleRoot.transform.localScale = Vector3.one * Overshoot(Mathf.Clamp01(appearAge / span));

        if (emoji != null)
        {
            float face = Mathf.Clamp01((appearAge - PopDelay) / span);

            emoji.transform.localScale = emojiBaseScale * Overshoot(face);
        }

        if (appearAge < span + PopDelay)
            return;

        appearAge = -1f;

        bubbleRoot.transform.localScale = Vector3.one;

        if (emoji != null)
            emoji.transform.localScale = emojiBaseScale;
    }

    private void Fading()
    {
        fadeAge += Time.deltaTime;

        float t = Mathf.Clamp01(fadeAge / FadeTime);

        // Shrinking as well as fading, because the ring is built from opaque
        // quads and there is no alpha on them to turn down. Scale is the one
        // handle every kind of renderer answers to
        bubbleRoot.transform.localScale = Vector3.one * (1f - t);

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

        if (emoji != null)
            emoji.transform.localScale = emojiBaseScale * (1f + (CelebrationScale - 1f) * t);

        // Long enough after the pop to have been read, early enough that the
        // burst is still up behind it. The number has to be seen leaving --
        // if the bubble closes first there is nothing to watch go anywhere
        if (!moneySent && celebrationAge >= MoneyDelay)
            SendMoney();

        if (sunburst == null)
            return;

        // A beat behind the face, same as the card and the face on the way in.
        // Held at nothing until the emoji has started, then on the same curve
        sunburst.localScale = celebrationAge < PopDelay ? Vector3.zero : sunburstBaseScale * t;

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
        if (emoji == null)
        {
            shakeAge = -1f;
            return;
        }

        shakeAge += Time.deltaTime;

        float t = Mathf.Clamp01(shakeAge / MoodShakeTime);

        if (t >= 1f)
        {
            shakeAge = -1f;

            emoji.transform.localRotation = Quaternion.identity;
            emoji.transform.localScale = emojiBaseScale;

            return;
        }

        float dying = 1f - t;

        float wobble = Mathf.Sin(shakeAge * MoodShakeSpeed * Mathf.PI * 2f) *
                       MoodShakeAngle * dying;

        emoji.transform.localRotation = Quaternion.Euler(0f, 0f, wobble);

        // Up and back down on one hump, so there is no seam where a two stage
        // tween would hand over
        emoji.transform.localScale =
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
                countdownText.text = Mathf.Max(0, Mathf.CeilToInt(left)).ToString();
        }

        if (countdownDisc != null)
        {
            countdownDisc.gameObject.SetActive(closing);
            countdownDisc.color = mood;
        }

        if (emoji == null)
            return;

        Mood now = CurrentMood;

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

        // Only a real change starts it, and only after the first one has been
        // seen. shown is null on the frame the bubble opens, so arriving with a
        // face is not the same event as changing to one
        if (shown.HasValue && shown.Value != now)
            shakeAge = 0f;

        shown = now;
    }

    // The food's own model, shrunk. There are no 2D icons for these foods and
    // drawing some would be a second thing to keep in step with the prefabs --
    // this way a new food shows up in the bubble the day it is made
    private void BuildIcon(SpawnableFood wanted)
    {
        if (icon != null)
        {
            Destroy(icon);
            icon = null;
        }

        if (wanted == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // The bubble opens either way, so a missing order shows as a card
            // with a count and a hole where the food goes -- which reads as a
            // broken bubble rather than as an unwired counter
            Debug.LogWarning(name + ": siparis yok, balonda yemek gorunmez.\n" +
                             "  FoodServingCustomerManager > Possible Orders bos, " +
                             "ya da OrderCounter onu doldurmuyor.", this);
#endif
            return;
        }

        if (iconAnchor == null)
            return;

        // A flat picture when the food has one, its own model when it does not.
        // The sprite is the better of the two on a card that always faces the
        // camera -- a model has to be lit, and lit by whatever happens to be
        // shining on the queue
        if (wanted.Icon != null)
        {
            icon = new GameObject("Icon");

            icon.transform.SetParent(iconAnchor, false);
            icon.transform.localPosition = Vector3.zero;
            icon.transform.localRotation = Quaternion.identity;

            SpriteRenderer flat = icon.AddComponent<SpriteRenderer>();

            flat.sprite = wanted.Icon;
            flat.sortingOrder = 110;

            float own = flat.bounds.size.y;

            if (own > .0001f)
                icon.transform.localScale = Vector3.one * (iconAnchor.lossyScale.y / own);

            return;
        }

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

        FitIcon();
    }

    // Scaled to the anchor's own size, so how big the picture is stays a thing
    // decided by dragging the anchor rather than by a number in here
    private void FitIcon()
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
