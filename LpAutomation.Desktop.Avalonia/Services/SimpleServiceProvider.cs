using System;
using System.Collections.Generic;

namespace LpAutomation.Desktop.Avalonia.Services;

public sealed class SimpleServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _singletons = new();
    private readonly Dictionary<Type, Func<object>> _transients = new();

    public void AddSingleton<T>(T instance) where T : notnull
        => _singletons[typeof(T)] = instance;

    public void AddTransient<T>(Func<T> factory) where T : notnull
        => _transients[typeof(T)] = () => factory();

    public T Get<T>() where T : notnull
    {
        var type = typeof(T);

        if (_singletons.TryGetValue(type, out var singleton))
            return (T)singleton;

        if (_transients.TryGetValue(type, out var transientFactory))
            return (T)transientFactory();

        throw new InvalidOperationException($"Service not registered: {type.FullName}");
    }

    public object? GetService(Type serviceType)
    {
        if (_singletons.TryGetValue(serviceType, out var singleton))
            return singleton;

        if (_transients.TryGetValue(serviceType, out var transientFactory))
            return transientFactory();

        return null;
    }
}
