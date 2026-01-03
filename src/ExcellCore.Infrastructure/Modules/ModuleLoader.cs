using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ExcellCore.Module.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExcellCore.Infrastructure.Modules;

public sealed class ModuleLoader : IModuleLoader
{
    private readonly IModuleCatalog _catalog;
    private readonly IModuleManifestProvider _manifestProvider;
    private readonly ILogger<ModuleLoader> _logger;

    public ModuleLoader(IModuleCatalog catalog, IModuleManifestProvider manifestProvider, ILogger<ModuleLoader> logger)
    {
        _catalog = catalog;
        _manifestProvider = manifestProvider;
        _logger = logger;
    }

    public async Task LoadModulesAsync(IServiceCollection services, CancellationToken cancellationToken = default)
    {
        Trace("LoadModulesAsync start");
        foreach (var manifest in _manifestProvider.DiscoverManifests())
        {
            Trace($"Discovered manifest {manifest.ModuleId} at {manifest.AssemblyPath}");
            if (!manifest.Enabled)
            {
                _logger.LogInformation("Module {ModuleId} is disabled", manifest.ModuleId);
                continue;
            }

            Assembly assembly;
            try
            {
                Trace($"Loading assembly {manifest.AssemblyPath}");
                assembly = Assembly.LoadFrom(manifest.AssemblyPath);
                Trace($"Loaded assembly {assembly.FullName}");
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Module {ModuleId} assembly not found at {AssemblyPath}", manifest.ModuleId, manifest.AssemblyPath);
                Trace($"Assembly not found for {manifest.ModuleId}: {manifest.AssemblyPath}");
                continue;
            }
            catch (BadImageFormatException ex)
            {
                _logger.LogError(ex, "Module {ModuleId} assembly at {AssemblyPath} is invalid", manifest.ModuleId, manifest.AssemblyPath);
                Trace($"Invalid assembly for {manifest.ModuleId}: {manifest.AssemblyPath}");
                continue;
            }
            var moduleType = assembly.GetTypes().FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            if (moduleType is null)
            {
                _logger.LogWarning("Module {ModuleId} does not expose an IModule entry point", manifest.ModuleId);
                Trace($"No module entry point found in {manifest.ModuleId}");
                continue;
            }

            var descriptor = new ReflectionModuleDescriptor(manifest, moduleType);
            _catalog.RegisterModule(descriptor);
            Trace($"Registered module {manifest.ModuleId}");

            if (Activator.CreateInstance(moduleType) is IModule moduleInstance)
            {
                Trace($"Configuring services for {manifest.ModuleId}");
                moduleInstance.ConfigureServices(services);
                Trace($"Configured services for {manifest.ModuleId}");
            }
        }

        Trace("LoadModulesAsync complete");
        await Task.CompletedTask;
    }

    public async Task ActivateModulesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Trace("ActivateModulesAsync start");
        foreach (var descriptor in _catalog.Modules)
        {
            if (Activator.CreateInstance(descriptor.EntryPoint) is not IModule moduleInstance)
            {
                _logger.LogWarning("Module {ModuleId} could not be created", descriptor.ModuleId);
                continue;
            }

            moduleInstance.Configure(serviceProvider);
            Trace($"Activated module {descriptor.ModuleId}");
        }

        Trace("ActivateModulesAsync complete");
        await Task.CompletedTask;
    }

    private void Trace(string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExcellCore");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "module-loader.log");
            File.AppendAllText(path, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore tracing failures.
        }
    }

    private sealed class ReflectionModuleDescriptor : IModuleDescriptor
    {
        public ReflectionModuleDescriptor(ModuleManifest manifest, Type entryPoint)
        {
            ModuleId = manifest.ModuleId;
            DisplayName = manifest.DisplayName;
            EntryPoint = entryPoint;
            Version = entryPoint.Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
            IsEnabled = manifest.Enabled;
        }

        public string ModuleId { get; }
        public string DisplayName { get; }
        public Version Version { get; }
        public bool IsEnabled { get; }
        public Type EntryPoint { get; }
    }
}
