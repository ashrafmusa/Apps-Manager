using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Core.Inventory.ViewModels;
using ExcellCore.Module.Core.Inventory.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.Core.Inventory;

public sealed class InventoryModule : IModule, IModuleDescriptor
{
    public string ModuleId => "Core.Inventory";
    public string DisplayName => "Inventory Kernel";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(InventoryModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<InventoryWorkspaceViewModel>();
        services.AddSingleton<InventoryWorkspaceView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var host = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<InventoryWorkspaceView>();
        host.RegisterView(ModuleId, view);
    }
}
