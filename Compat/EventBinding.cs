using System;

namespace Donuts.Compat;

public class EventBinding<T> where T : struct, IEvent
{
    private readonly Action _actionNoArgs;
    private readonly Action<T> _actionWithArgs;

    public EventBinding(Action callback)
    {
        _actionNoArgs = callback;
    }

    public EventBinding(Action<T> callback)
    {
        _actionWithArgs = callback;
    }

    internal void Invoke(T evt)
    {
        _actionNoArgs?.Invoke();
        _actionWithArgs?.Invoke(evt);
    }
}
