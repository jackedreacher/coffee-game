using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TransformExtensions
{    
    /// <summary>
    /// Destroys all of the transform's children
    /// </summary>
    /// <param name="transform">The parent</param>
    public static void Clear(this Transform transform)
    {
        while (transform.childCount > 0)
        {
            Transform child = transform.transform.GetChild(0);
            child.SetParent(null);

            if (Application.isPlaying)
                Object.Destroy(child.gameObject);
            else
                Object.DestroyImmediate(child.gameObject);
        }
    }

    public static Transform GetRandomChild(this Transform transform)
    {
        if (transform.childCount <= 0)
            return null;

        return transform.GetChild(Random.Range(0, transform.childCount));
    }
}
