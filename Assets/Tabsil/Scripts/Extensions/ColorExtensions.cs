using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ColorExtensions
{
    public static Color With(this Color color, float? r = null, float? g = null, float? b = null, float? a = null)
    {
        color.r = r == null ? color.r : (float)r;
        color.g = g == null ? color.g : (float)g;
        color.b = b == null ? color.b : (float)b;
        color.a = a == null ? color.a : (float)a;

        return color;
    }
}
