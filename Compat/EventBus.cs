using System;
using System.Collections.Generic;

namespace Donuts.Compat;

public static class EventBus
{
    private static readonly Dictionary<Type, List<object>> s_bindings = new();

    public static void Register<T>(EventBinding<T> binding) where T : struct, IEvent
    {
        var type = typeof(T);
        if (!s_bindings.TryGetValue(type, out var list))
        {
            list = new List<object>();
            s_bindings[type] = list;
        }
        list.Add(binding);
    }

    public static void Deregister<T>(EventBinding<T> binding) where T : struct, IEvent
    {
        if (s_bindings.TryGetValue(typeof(T), out var list))
        {
            list.Remove(binding);
        }
    }

    public static void Raise<T>(T evt) where T : struct, IEvent
    {
        if (!s_bindings.TryGetValue(typeof(T), out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            ((EventBinding<T>)list[i]).Invoke(evt);
        }
    }

    public static void Clear()
    {
        s_bindings.Clear();
    }
}
