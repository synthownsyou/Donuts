using System;
using System.Collections.Generic;

namespace Donuts.Compat;

public class DiContainer
{
    private readonly Dictionary<(Type, string), object> _services = new();

    public void Register<T>(T service, string key = null) where T : class
    {
        _services[(typeof(T), key)] = service;
    }

    public T Resolve<T>(string key = null) where T : class
    {
        if (_services.TryGetValue((typeof(T), key), out var service))
        {
            return (T)service;
        }
        throw new InvalidOperationException($"Service {typeof(T).Name} (key={key}) not registered.");
    }

    public bool TryResolve<T>(out T service, string key = null) where T : class
    {
        if (_services.TryGetValue((typeof(T), key), out var obj))
        {
            service = (T)obj;
            return true;
        }
        service = default;
        return false;
    }
}
