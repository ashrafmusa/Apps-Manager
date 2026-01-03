using System.Collections.Concurrent;
using ExcellCore.Module.Abstractions;

namespace ExcellCore.Infrastructure.Modules;

public sealed class ModuleHost : IModuleHost
{
    private readonly ConcurrentDictionary<string, object> _views = new();

    public event EventHandler<ModuleViewRegisteredEventArgs>? ModuleViewRegistered;

    public IReadOnlyDictionary<string, object> Views => _views;

    public void RegisterView(string moduleId, object view)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            throw new ArgumentException("Module id is required", nameof(moduleId));
        }
        if (view is null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        _views[moduleId] = view;
        ModuleViewRegistered?.Invoke(this, new ModuleViewRegisteredEventArgs(moduleId, view));
    }

    public bool TryGetView(string moduleId, out object? view)
    {
        if (moduleId is null)
        {
            view = null;
            return false;
        }

        var success = _views.TryGetValue(moduleId, out var storedView);
        view = storedView;
        return success;
    }
}
