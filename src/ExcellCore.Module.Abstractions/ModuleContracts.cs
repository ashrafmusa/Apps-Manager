using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.Abstractions;

public interface IModule
{
    void ConfigureServices(IServiceCollection services);
    void Configure(IServiceProvider serviceProvider);
}

public interface IModuleDescriptor
{
    string ModuleId { get; }
    string DisplayName { get; }
    Version Version { get; }
    bool IsEnabled { get; }
    Type EntryPoint { get; }
}

public interface IModuleCatalog
{
    IReadOnlyCollection<IModuleDescriptor> Modules { get; }
    void RegisterModule(IModuleDescriptor descriptor);
}

public interface IModuleLoader
{
    Task LoadModulesAsync(IServiceCollection services, CancellationToken cancellationToken = default);
    Task ActivateModulesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}

public interface IModuleManifestProvider
{
    IEnumerable<ModuleManifest> DiscoverManifests();
}

public sealed record ModuleManifest(string ModuleId, string DisplayName, string AssemblyPath, bool Enabled);

public interface IModuleHost
{
    event EventHandler<ModuleViewRegisteredEventArgs>? ModuleViewRegistered;
    IReadOnlyDictionary<string, object> Views { get; }
    void RegisterView(string moduleId, object view);
    bool TryGetView(string moduleId, out object? view);
}

public sealed class ModuleViewRegisteredEventArgs : EventArgs
{
    public ModuleViewRegisteredEventArgs(string moduleId, object view)
    {
        ModuleId = moduleId;
        View = view;
    }

    public string ModuleId { get; }
    public object View { get; }
}
