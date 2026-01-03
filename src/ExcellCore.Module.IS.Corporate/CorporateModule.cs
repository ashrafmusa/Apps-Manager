using ExcellCore.Module.Abstractions;
using ExcellCore.Module.IS.Corporate.ViewModels;
using ExcellCore.Module.IS.Corporate.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.IS.Corporate;

public sealed class CorporateModule : IModule, IModuleDescriptor
{
    public string ModuleId => "IS.Corporate";
    public string DisplayName => "Corporate Operations";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(CorporateModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<CorporateWorkspaceViewModel>();
        services.AddSingleton<CorporateWorkspaceView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var host = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<CorporateWorkspaceView>();
        host.RegisterView(ModuleId, view);
    }
}
