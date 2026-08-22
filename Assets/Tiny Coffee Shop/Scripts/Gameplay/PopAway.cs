using TMPro;
using UnityEngine;

// Lying there, then gone.
//
// Attached to the body itself, so it dies with whatever it was attached to and
// there is nothing to clean up if the round ends first.
public class PopAway : MonoBehaviour
{
    private const float pop = .22f;

    private float wait;
    private float age;
    private Vector3 size;

    public static void After(GameObject body, float seconds)
    {
        if (body == null)
            return;

        PopAway away = body.AddComponent<PopAway>();

        away.wait = seconds;
        away.size = body.transform.localScale;
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (age < wait)
            return;

        float t = (age - wait) / pop;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Swells, then goes. A body that only shrinks reads as sinking into the
        // floor; the puff out first is what makes it read as popping.
        float swell = t < .35f
            ? 1f + t / .35f * .25f
            : Mathf.Lerp(1.25f, 0f, (t - .35f) / .65f);

        transform.localScale = size * swell;
    }
}
