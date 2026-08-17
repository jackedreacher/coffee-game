using System;
using UnityEngine;

// How many customers may walk out before the shift is over.
//
// Kept away from the queue that reports the losses and away from the hearts that
// draw them, because there is more than one queue and there may be more than one
// place the count is shown. Both talk to this, and this talks to neither
public class Lives : MonoBehaviour
{
    public static Lives Instance { get; private set; }

    [Header(" Settings ")]
    [Tooltip("Kac musteri kacabilir. 0 = varsayilan 3")]
    [SerializeField] private int maxLives = 3;

    // The same reason every other number in this project has one: this
    // component lands in a scene that is already saved, and a serialised zero
    // is a shift that is over before it starts
    public int Max => maxLives > 0 ? maxLives : 3;

    private int left = -1;

    public int Left => left < 0 ? Max : left;

    // Fires on every change including the first, so anything drawing it can
    // subscribe and then ask, rather than needing to be told to start
    public event Action Changed;

    // Once, when the last one goes. Nothing listens yet -- the game over screen
    // is a separate decision -- but the moment has to be somewhere findable
    public event Action Emptied;

    private void Awake()
    {
        Instance = this;
        left = Max;
    }

    private void Start()
    {
        Changed?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Lose()
    {
        if (Left <= 0)
            return;

        left = Left - 1;

        Changed?.Invoke();

        if (left > 0)
            return;

        Debug.Log("[Can] son can gitti -- " + Max + " musteri kacti", this);

        Emptied?.Invoke();
    }

    public void Refill()
    {
        left = Max;

        Changed?.Invoke();
    }
}
