using System;
using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Extensions.Sync.ViewModels;
using ExcellCore.Module.Extensions.Sync.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.Extensions.Sync;

public sealed class SyncModule : IModule, IModuleDescriptor
{
    public string ModuleId => "Extensions.Sync";
    public string DisplayName => "Sync Center";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(SyncModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Services.IConflictResolverService, Services.ConflictResolverService>();
        services.AddSingleton<Services.IDeltaSyncProvider, Services.DeltaSyncProvider>();
        services.AddSingleton<Services.ISyncTransportAdapter>(sp =>
            new Services.JsonSyncTransportAdapter(
                sp.GetRequiredService<Services.IDeltaSyncProvider>(),
                "SiteA",
                Environment.MachineName));
        services.AddSingleton<SyncWorkspaceViewModel>();
        services.AddSingleton<SyncWorkspaceView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var host = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<SyncWorkspaceView>();
        host.RegisterView(ModuleId, view);
    }
}
