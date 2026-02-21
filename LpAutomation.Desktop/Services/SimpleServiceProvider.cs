using System;
using System.Collections.Generic;

namespace LpAutomation.Desktop.Services;

public sealed class SimpleServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<Type, object> _singletons = new();

    public void AddSingleton<T>(T instance) where T : notnull
        => _singletons[typeof(T)] = instance;

    public void AddTransient<T>(Func<T> factory) where T : notnull
        => _factories[typeof(T)] = () => factory();

    public object? GetService(Type serviceType)
    {
        if (_singletons.TryGetValue(serviceType, out var singleton))
            return singleton;

        if (_factories.TryGetValue(serviceType, out var factory))
            return factory();

        return null; // IServiceProvider convention
    }

    public T Get<T>() where T : notnull
    {
        var obj = GetService(typeof(T));
        if (obj is null)
            throw new InvalidOperationException($"Service not registered: {typeof(T).FullName}");
        return (T)obj;
    }

    public bool TryGet<T>(out T? value) where T : class
    {
        value = GetService(typeof(T)) as T;
        return value is not null;
    }
}
