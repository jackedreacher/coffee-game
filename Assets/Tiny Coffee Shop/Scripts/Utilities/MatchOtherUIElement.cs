using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MatchOtherUIElement : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private RectTransform target;
    private RectTransform rt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        yield return null;

        rt = GetComponent<RectTransform>();

        // Position it first
        rt.transform.position = target.transform.position;

        // Apply the width & height
        rt.sizeDelta = target.sizeDelta;
    }
}
