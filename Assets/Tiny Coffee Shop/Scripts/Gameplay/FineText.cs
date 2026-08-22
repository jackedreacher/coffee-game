using TMPro;
using UnityEngine;

// The money a shot costs, shown the way money is always shown here.
//
// A thin wrapper rather than an effect of its own. It used to be one -- a world
// space label that floated up and faded -- and that was the problem: a penalty
// drawn by its own machinery, moving its own way, reads as a different kind of
// event from a payment. It is the same event with the sign flipped, so it flies
// the same path into the same counter and the counter punches the same way.
// Only the colour and the minus say which direction it went.
//
// Static because there is nothing to keep: MoneyCounter owns the flight, and it
// has to, since whoever was shot is destroyed a couple of seconds later and a
// coroutine on a destroyed object stops exactly where it stands.
public static class FineText
{
    public static void Show(Vector3 where, int amount, TMP_FontAsset font)
    {
        if (amount <= 0)
            return;

        if (MoneyCounter.Instance == null)
        {
            // Still charged, just not shown. Losing the animation is a shame;
            // losing the penalty is a bug.
            if (CurrencyManager.instance != null)
                CurrencyManager.instance.UseCurrency(amount);

            Debug.LogWarning("[Kovboy] sahnede MoneyCounter yok -- ceza " +
                             "kesildi ama yazi ucmadi.");
            return;
        }

        MoneyCounter.Instance.FlyFine(where, amount, font);
    }
}
