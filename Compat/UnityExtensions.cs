using UnityEngine;

namespace Donuts.Compat;

public static class UnityExtensions
{
    public static T OrNull<T>(this T obj) where T : Object
    {
        return obj != null ? obj : null;
    }
}
