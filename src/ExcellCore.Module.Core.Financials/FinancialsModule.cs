using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Core.Financials.ViewModels;
using ExcellCore.Module.Core.Financials.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.Core.Financials;

public sealed class FinancialsModule : IModule, IModuleDescriptor
{
    public string ModuleId => "Core.Financials";
    public string DisplayName => "Financial Engine";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(FinancialsModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<FinancialsWorkspaceViewModel>();
        services.AddSingleton<FinancialsWorkspaceView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var host = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<FinancialsWorkspaceView>();
        host.RegisterView(ModuleId, view);
    }
}
