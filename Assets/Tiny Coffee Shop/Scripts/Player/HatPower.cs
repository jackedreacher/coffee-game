using TMPro;
using UnityEngine;

// What a hat DOES, as opposed to what it looks like.
//
// Keyed on the hat prefab's own name rather than on its index in the wardrobe
// or on the label the player reads. The index moves the moment a hat is added
// in the middle of the list, and the label is translated -- neither survives
// being the thing a power is looked up by. The prefab name does not change
// without somebody renaming an asset on purpose.
public enum HatPower
{
    None,

    // Cowboy hat: draws a revolver, fires across the kitchen. What the shot
    // does is decided by what it hits -- see IShootable.
    Revolver,
}

// One clip swapped for another while a hat is worn.
//
// Both halves are here because an AnimatorOverrideController is keyed on the
// clip being REPLACED, and only the controller knows which clip a given state
// is carrying. The state name is the half a person can hold in their head --
// "Walk" -- and the clip behind it is looked up from that by the setup command,
// so a controller rebuilt tomorrow cannot leave this quietly overriding a clip
// nothing plays any more.
[System.Serializable]
public class StanceSwap
{
    [Tooltip("Player controller'indaki state adi. Ornek: Idle, Walk, Greet_Start")]
    public string state;

    [Tooltip("Sapka takiliyken o state'te oynayacak klip. Bos = degismesin.")]
    public AnimationClip clip;

    [Tooltip("Yerine gececegi klip. Kurulum komutu state adindan bulup " +
             "doldurur -- elle dokunma.")]
    public AnimationClip replaces;
}

public static class HatPowerBook
{
    // Matched as a case-insensitive fragment, so "m_CowboyHat01" and a renamed
    // "Cowboy Hat (Gold)" both answer the same. Adding a power is a line here
    // plus the code that carries it out; there is no data file, because a power
    // that nothing implements is not a number somebody can tune, it is a bug.
    private static readonly (string fragment, HatPower power)[] table =
    {
        ("cowboy", HatPower.Revolver),
    };

    public static HatPower For(string hatKey)
    {
        if (string.IsNullOrWhiteSpace(hatKey))
            return HatPower.None;

        string key = hatKey.ToLowerInvariant();

        for (int i = 0; i < table.Length; i++)
            if (key.Contains(table[i].fragment))
                return table[i].power;

        return HatPower.None;
    }
}

// The props a power needs, in one asset the runtime can load without anything
// being dragged into the scene.
//
// The revolver lives in a third-party folder that is not under Resources, and
// the player prefab has no field to hold it -- so the alternatives were moving
// somebody else's asset or adding a serialized field to a component already
// saved in the scene. This is neither: Cooked Fast > Sapka > Sapka Yeteneklerini
// Kur writes the asset, and the runtime asks for it by path.
public class HatPowerCatalogue : ScriptableObject
{
    public const string ResourcePath = "Hats/Hat Powers";
    public const string AssetPath = "Assets/Resources/Hats/Hat Powers.asset";

    [Tooltip("Kovboy sapkasinin cektigi silah. Dead West paketindeki Revovler.")]
    public GameObject revolverPrefab;

    [Tooltip("Namludan cikan parlama. Bos birakilabilir.")]
    public GameObject muzzleFlash;

    [Tooltip("Atis sesi. Bos birakilabilir -- sessiz ates eder.")]
    public AudioClip gunshot;

    [Header(" Sapka takiliyken durus ")]
    // A hat that changes how the character STANDS, not just what it can do.
    //
    // Swapped through an AnimatorOverrideController, which replaces one clip
    // with another and leaves the state machine exactly as it was -- no second
    // Idle state, no branch in PlayerAnimator, nothing to keep in step.
    //
    // A LIST rather than the one idle it started as. The cowboy hat turned out
    // to be a costume rather than a trick: standing, walking and greeting are
    // all different while it is on, and every one of them is the same swap made
    // once more. A list is also the difference between adding a fourth and
    // editing three files.
    [Tooltip("Sapka takiliyken hangi state'te ne oynayacagi. Kurulum komutu " +
             "doldurur, sonra elle degistirilebilir.")]
    public StanceSwap[] stance;

    [Header(" Silahin elde durusu ")]

    // The barrel is lined up with the target automatically -- whichever of
    // the model's own axes is longest is taken to be the barrel, which is
    // true of every gun and needs nothing typed. What cannot be worked out
    // from a bounding box is which END of that axis the muzzle is, or which
    // way up the grip hangs. That is what this is for: it is applied ON TOP
    // of the automatic alignment, so zero means "the guess was right".
    [Tooltip("Silah ters/yan duruyorsa buradan cevir. Otomatik hizalamanin " +
             "USTUNE eklenir. 0,180,0 = namluyu ters cevirir. " +
             "0,0,180 = kabzayi yukari alir.")]
    public Vector3 gunTurn;

    // 2.5 rather than the 0.55 this started at.
    //
    // The share is measured against the rig height, which is a number in
    // the MODEL ROOT's units, while the scale that comes out of it is
    // applied down in the hand -- so the two do not cancel and the share
    // reads much larger than "half the animal" would suggest. What it is
    // set to here is simply what looked right on screen, which is the only
    // authority a number like this has.
    [Tooltip("Silahin boyu. Buyut/kucult, gozle bak. 0 = varsayilan 2.5")]
    public float gunSize;

    [Tooltip("Silahin arka ucu yumruktan bu kadar onde durur, birim. " +
             "Govdenin icinde kaliyorsa buyut. 0 = varsayilan 0.12")]
    public float gunReach;

    // No sentinels on these two: zero is where they belong, not an empty
    // field. They exist to move the gun OFF that spot.
    [Tooltip("Yukari / asagi kaydirma, birim. 0 = elin hizasinda.")]
    public float gunLift;

    [Tooltip("Saga / sola kaydirma, birim. 0 = elin uzerinde.")]
    public float gunSide;

    [Header(" Zamanlama ")]
    // When in the clip the shot goes off.
    //
    // Not something this can work out: an animation file records poses, not
    // what they mean, so nothing in Quickdraw says "the hammer falls here".
    // Dialled by watching it once. Everything the bang does -- flash, tracer,
    // the station reacting -- happens at this moment and nowhere else.
    [Tooltip("Klip basladiktan kac saniye sonra ates cikar. " +
             "0 = varsayilan 0.45")]
    public float fireAt;

    [Tooltip("Atistan sonra silah kac saniye elde kalir. 0 = varsayilan 0.45")]
    public float holdAfter;

    // Playing the clip faster rather than re-authoring it.
    //
    // Everything timed off the clip is divided by this too, so the shot still
    // lands on the same FRAME of the draw -- it simply gets there sooner. What
    // is NOT divided is the gap between the two shots and the shake, because
    // those are choices about how the gun behaves, not places in an animation.
    [Tooltip("Cekme animasyonu kac kat hizli oynar. 0 = varsayilan 1.7")]
    public float clipSpeed;

    // The second shot in a row, and every one after it.
    //
    // Not a separate animation, the same one played faster. The first shot is
    // the character deciding to draw; a shot fired while the gun is already up
    // and the arm is already moving is not that, and playing it at full length
    // makes the follow-up look slower than the thing it followed.
    [Tooltip("Ard arda gelen atis kac kat hizli oynar. Ilkinin yarisi kadar " +
             "surmesi icin 2. 0 = varsayilan 2")]
    public float followSpeed;

    [Header(" Tabagi firlatmak ")]

    // How far the tray goes up, measured on the SCREEN rather than in the
    // world -- see RevolverPower.Toss for why those are not the same thing on
    // a camera that looks steeply down.
    [Tooltip("Tabak ne kadar yukari firlar. EKRANA gore yukari, dunyaya gore " +
             "degil. 0 = varsayilan 1.6")]
    public float tossLift;

    [Tooltip("Havadayken kac tam takla atar. 0 = varsayilan 1")]
    public float tossSpins;

    [Header(" Musteriyi vurmak ")]
    [Tooltip("Vurulan musterinin kac paraya mal oldugu. 0 = varsayilan 25")]
    public int customerFine;

    [Tooltip("Ceset yerde kac saniye kalir. 0 = varsayilan 2")]
    public float bodySeconds;

    // The font the fine is written in.
    //
    // Held here because a runtime script cannot go looking through the project
    // for a font, and the thing that draws the number is created from nothing
    // the moment somebody is shot -- there is no prefab in the scene with a
    // field to drop this into. The setup command fills it.
    [Tooltip("Ceza yazisinin fontu. Kurulum komutu doldurur.")]
    public TMP_FontAsset fineFont;

    // Same reason as the font: the thing that shows this is spawned onto
    // whoever happened to be standing there, and there is no prefab field
    // anywhere in the scene to drop it into.
    [Tooltip("Vurulan musterinin uzerinde cikan emoji. Kurulum komutu doldurur.")]
    public GameObject deadEmoji;

    // The pose the revolver ships with.
    //
    // Not a guess and not a tidy zero: these are the numbers somebody sat in
    // the scene view and dialled until the gun looked held. A default that
    // produces a working revolver is worth more than a default that produces a
    // clean-looking asset file, and anyone starting from scratch should start
    // from a gun that is already in the fist.
    public static readonly Vector3 BaseTurn =
        new Vector3(61.6973f, 197.0313f, 174.7698f);

    public const float BaseSize = 1.7728f;
    public const float BaseReach = .33f;
    public const float BaseLift = 1.08f;
    public const float BaseSide = .01f;

    // Put back to the shipped pose. Reach, lift and side are ordinary numbers
    // whose zero means zero, so they cannot carry a "unset" default the way the
    // others do -- somebody has to write them, and this is that somebody.
    public void UseBasePlacement()
    {
        gunTurn = BaseTurn;
        gunSize = BaseSize;
        gunReach = BaseReach;
        gunLift = BaseLift;
        gunSide = BaseSide;
    }

    [Header(" Kursun ")]
    // Which way the revolver points while it is out. OFF is how it ships.
    //
    // Off, the gun keeps exactly the angle gunTurn puts it at in the fist and
    // never moves again. That angle was dialled in by hand against the model,
    // and it is the only thing that makes the revolver look held rather than
    // stuck to a wrist.
    //
    // On, the barrel is turned at whatever is being shot -- which means the
    // aimed rotation REPLACES gunTurn outright, because the two are answers to
    // the same question. The streak then leaves the muzzle properly, and the
    // gun sits in the hand at whatever angle aiming happens to need, which is
    // usually not one anybody would choose. Tried, and it looked worse than the
    // crooked streak it fixed -- kept because it is one tick away if a future
    // hand pose makes it work.
    [Tooltip("DENEYSEL. Acarsan namlu hedefe doner ama 'Cevir' degerleri " +
             "gecersiz olur -- silahin eldeki durusu bozulur.")]
    public bool aimAtTarget;

    // Correction for the aimed pose, and it has to be its own field.
    //
    // gunTurn cannot do this job: those angles orient the gun in the HAND, and
    // the aimed rotation replaces the hand's angle outright rather than adding
    // to it. Feeding gunTurn back in would swing the barrel away from the
    // target by exactly the amount that was dialled in to seat the grip.
    //
    // Zero is the normal answer. Y = 180 is the one that matters: the barrel
    // axis is found by measuring the model, and measuring cannot tell which END
    // of that axis the muzzle is -- so if the gun aims backwards, that is the
    // flip. Z = 180 turns the grip over.
    [Tooltip("Nisan duzeltmesi. Silah ters yone ates ediyorsa Y = 180.")]
    public Vector3 aimTurn;

    [Tooltip("Kursun izinin kalinligi. 0 = varsayilan 0.045")]
    public float tracerWidth;

    [Tooltip("Kursun izi kac saniye gorunur. 0 = varsayilan 0.07")]
    public float tracerSeconds;

    // Where the shot comes FROM, measured off the player.
    //
    // Off the character rather than off the revolver, and that is the whole
    // point of it. The gun's place in the hand is something that gets nudged by
    // hand until it looks right -- and while it was the source of the bullet,
    // every one of those nudges moved the shot as well. Fixing the grip broke
    // the aim. They are unrelated now: put the revolver wherever it looks held
    // and the bullet still leaves from the same place it always did.
    [Tooltip("Kursun oyuncunun ayagindan kac birim yukaridan cikar. " +
             "0 = varsayilan 1")]
    public float muzzleHeight;

    [Tooltip("Kursun oyuncunun kac birim onunden baslar. 0 = varsayilan 0.6")]
    public float muzzleAhead;

    public float GunSize => gunSize > .01f ? gunSize : BaseSize;
    // Zero really is zero here: the gun sits where the hand is, and this is
    // trim for nudging it into the fist rather than a distance to hold it
    // away at.
    // 0.45 because Quickdraw is 17 frames at 30 fps -- 0.57 seconds. The old
    // 0.55 put the shot on the last frame of the clip, after the draw was over
    // rather than at the end of it.
    public float FireAt => fireAt > .01f ? fireAt : .45f;
    public float HoldAfter => holdAfter > .01f ? holdAfter : .45f;
    public float ClipSpeed => clipSpeed > .01f ? clipSpeed : 1.7f;
    public float FollowSpeed => followSpeed > .01f ? followSpeed : 2f;
    public float TossLift => tossLift > .01f ? tossLift : 1.6f;
    public float TossSpins => tossSpins > .01f ? tossSpins : 1f;
    public float TracerWidth => tracerWidth > .0001f ? tracerWidth : .045f;
    public float TracerSeconds => tracerSeconds > .001f ? tracerSeconds : .07f;
    public float MuzzleHeight => muzzleHeight > .001f ? muzzleHeight : 1f;
    public float MuzzleAhead => muzzleAhead > .001f ? muzzleAhead : .6f;
    public int CustomerFine => customerFine > 0 ? customerFine : 25;
    public float BodySeconds => bodySeconds > .01f ? bodySeconds : 2f;
    // The whole sequence, draw to holster. Reporting only -- nothing waits for
    // it. There is no range and no reload: the gun fires at any distance, as
    // fast as it can be drawn, and the draw is the only limit left.
    public float ShotSeconds => FireAt / ClipSpeed + HoldAfter;

    // Bumped whenever the meaning of the placement fields changes.
    //
    // They are trim: numbers dialled by eye against a particular way of
    // holding the gun. Change that way of holding it and every one of them
    // is a correction for a fault that no longer exists -- which is worse
    // than no correction at all. The setup command clears them once when it
    // sees an older stamp, and never again, so tuning done AFTER the change
    // survives every later run.
    //
    // 2: the gun rides in the hand and takes the forearm's direction.
    //    Before this it was aimed by the body and pushed out in front of
    //    the fist, and the trim was fighting that.
    // 3: nothing new is cleared. Version 3 exists to record that gunSize
    //    was briefly on the reset list and should not be: it is not trim
    //    correcting a fault, it is the size somebody chose by looking at
    //    it, and clearing it threw away the one number that was right.
    public const int PlacementVersion = 3;

    [HideInInspector] public int placementVersion;

    private static HatPowerCatalogue loaded;
    private static bool looked;

    public static HatPowerCatalogue Get()
    {
        // Cached including the miss. Resources.Load on a path that does not
        // exist is not free, and this is asked on every tap.
        if (looked)
            return loaded;

        looked = true;
        loaded = Resources.Load<HatPowerCatalogue>(ResourcePath);

        if (loaded == null)
            Debug.LogWarning("[Sapka Yetenek] " + AssetPath + " yok. " +
                             "Cooked Fast > Sapka > Sapka Yeteneklerini Kur calistir.");

        return loaded;
    }

    // For the editor tool, which has just written a new one.
    public static void Forget()
    {
        looked = false;
        loaded = null;
    }
}
