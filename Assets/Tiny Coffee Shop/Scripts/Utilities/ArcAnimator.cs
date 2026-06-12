using System;
using System.Collections;
using UnityEngine;

public class ArcAnimator : MonoBehaviour
{
    public static ArcAnimator Instance;

    private void Awake()
    {
        Instance = this;
    }

    public static void Animate(Transform t, Transform target, float duration, float delay, float height, Action<GameObject> completeCallback)
    {
        Instance.AnimateInternal(t, target, duration, delay, height, completeCallback);
    }

    private void AnimateInternal(Transform t, Transform target, float duration, float delay, float height, Action<GameObject> completeCallback)
    {
        StartCoroutine(MoveAlongArc(t, target, duration, delay, height, completeCallback));
    }

    private IEnumerator MoveAlongArc(Transform t, Transform target, float duration, float delay, float height, Action<GameObject> completeCallback)
    {
        yield return new WaitForSeconds(delay);

        if (t == null)
        {
            completeCallback?.Invoke(null);
            yield break;
        }

        float timer = 0;
        Vector3 startPosition = t.position;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;

            float percent = timer / duration;
            Vector3 pos = Vector3.Lerp(startPosition, target.position, percent);
            pos.y += Mathf.Sin(percent * Mathf.PI) * height;
            t.position = pos;
        }

        t.position = target.position;
        completeCallback?.Invoke(t.gameObject);
    }
}
