using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Donuts.Compat;

public class DiContainer
{
    private readonly Dictionary<(Type, string), object> _services = new();

    public void Register<T>(T service, string key = null) where T : class
    {
        _services[(typeof(T), key)] = service;
    }

    public void AddSingleton<TInterface, TImpl>(string key = null)
        where TInterface : class
        where TImpl : class, TInterface
    {
        var instance = CreateInstance(typeof(TImpl));
        _services[(typeof(TInterface), key)] = instance;
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

    private object CreateInstance(Type type)
    {
        ConstructorInfo[] ctors = type.GetConstructors();
        ConstructorInfo ctor = ctors.OrderByDescending(c => c.GetParameters().Length).First();
        ParameterInfo[] parameters = ctor.GetParameters();

        if (parameters.Length == 0)
            return Activator.CreateInstance(type);

        var args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type paramType = parameters[i].ParameterType;
            var lookupKey = (paramType, (string)null);
            if (_services.TryGetValue(lookupKey, out var resolved))
            {
                args[i] = resolved;
            }
            else
            {
                foreach (var kvp in _services)
                {
                    if (paramType.IsAssignableFrom(kvp.Key.Item1.GetType()) ||
                        paramType.IsInstanceOfType(kvp.Value))
                    {
                        args[i] = kvp.Value;
                        break;
                    }
                }
            }
        }

        return ctor.Invoke(args);
    }
}
