using ExcellCore.Module.Abstractions;
using ExcellCore.Module.IS.Retail.ViewModels;
using ExcellCore.Module.IS.Retail.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.IS.Retail;

public sealed class RetailModule : IModule, IModuleDescriptor
{
    public string ModuleId => "IS.Retail";
    public string DisplayName => "Retail Suite";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(RetailModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<RetailWorkspaceViewModel>();
        services.AddSingleton<RetailWorkspaceView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var host = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<RetailWorkspaceView>();
        host.RegisterView(ModuleId, view);
    }
}
