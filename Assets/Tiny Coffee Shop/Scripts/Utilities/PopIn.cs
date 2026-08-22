using UnityEngine;

// Scales something in with an overshoot, so it ARRIVES rather than appears.
//
// Every mark in this game currently snaps on: a tick goes from not drawn to
// fully drawn between two frames, and at that speed the eye registers that the
// picture is different without registering that anything happened. A thing that
// grows past its size and settles back is read as an event, and it is the
// event -- this order line is done, this pan is ready -- that the player needs
// to catch out of the corner of their eye while doing something else.
//
// DRIVEN BY OnEnable, which is the point. Both ticks in this game are built
// once and then switched on and off, so hanging the animation off "switched on"
// means the callers do not change at all and no future caller can forget. The
// icons in the order bubble are not switched -- they are darkened in place --
// so those ask for it by name through Play().
public class PopIn : MonoBehaviour
{
    [Tooltip("Pop kac saniye sursun. Kisa tut -- bu bir efekt degil, bir haber")]
    [SerializeField] private float duration = .26f;

    [Tooltip("Kendi boyutunun kac katina kadar buyuyup geri gelsin. " +
             "1 = hic tasmasin, sadece buyuyerek gelsin")]
    [SerializeField] private float overshoot = 1.35f;

    [Tooltip("Sifirdan mi baslasin. Kapali ise kendi boyutundan baslayip " +
             "sadece bir kere zipilar -- zaten gorunen bir sey icin dogrusu bu")]
    [SerializeField] private bool fromNothing = true;

    // The scale it is meant to end at, learned rather than configured.
    //
    // Learned ONCE, on the first enable, and never again: this component writes
    // localScale every frame it is running, so a second reading would be
    // reading its own output mid-animation and the mark would shrink a little
    // every time it appeared
    private Vector3 home;
    private bool learned;

    private float age;
    private bool running;

    private void OnEnable()
    {
        Play();
    }

    // Public because the bubble's food icons are never switched off -- a
    // finished order line keeps its picture and is greyed out in place, so
    // there is no enable to hang this on
    public void Play()
    {
        if (!learned)
        {
            home = transform.localScale;
            learned = true;
        }

        age = 0f;
        running = true;

        // Written now rather than waiting for the first Update, or the mark
        // draws once at full size before the animation gets its first look in
        Apply(0f);
    }

    private void OnDisable()
    {
        // Left at its proper size, not at whatever fraction it had reached.
        // Something switched off mid pop is switched on again later by a
        // different code path than the one that stopped it
        if (learned)
            transform.localScale = home;

        running = false;
    }

    private void Update()
    {
        if (!running)
            return;

        // Unscaled, because these marks report things that keep happening while
        // the game is paused for a round summary, and a pop frozen half way
        // through reads as a broken sprite
        age += Time.unscaledDeltaTime;

        float span = Mathf.Max(.01f, duration);

        if (age >= span)
        {
            transform.localScale = home;
            running = false;

            return;
        }

        Apply(age / span);
    }

    private void Apply(float t)
    {
        transform.localScale = home * Curve(t);
    }

    // Out past the target, then back.
    //
    // A sine hump added to a linear rise, rather than a spring: this is a fixed
    // length animation that must end exactly on the home scale, and a spring
    // ends when it feels like it. The hump is zero at both ends by
    // construction, so the landing is exact without a special case
    private float Curve(float t)
    {
        float rise = fromNothing ? t : 1f;

        return rise + Mathf.Sin(t * Mathf.PI) * (overshoot - 1f);
    }

    // Added if it is not already there, so a prop built by an editor command
    // last month gains the animation without being rebuilt.
    //
    // MUST be called after whatever sets the final localScale, since the first
    // enable is what learns it
    public static PopIn Ensure(GameObject host)
    {
        if (host == null)
            return null;

        PopIn found = host.GetComponent<PopIn>();

        return found != null ? found : host.AddComponent<PopIn>();
    }

    // Convenience for the call sites that have a GameObject and no interest in
    // whether it has ever been popped before
    public static void Play(GameObject host)
    {
        PopIn pop = Ensure(host);

        if (pop != null)
            pop.Play();
    }
}
