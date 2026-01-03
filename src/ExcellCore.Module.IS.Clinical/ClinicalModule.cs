using ExcellCore.Module.Abstractions;
using ExcellCore.Module.IS.Clinical.ViewModels;
using ExcellCore.Module.IS.Clinical.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.IS.Clinical;

public sealed class ClinicalModule : IModule, IModuleDescriptor
{
    public string ModuleId => "IS.Clinical";
    public string DisplayName => "Clinical Workflows";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(ClinicalModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ClinicalWorkspaceViewModel>();
        services.AddSingleton<ClinicalWorkspaceView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var host = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<ClinicalWorkspaceView>();
        host.RegisterView(ModuleId, view);
    }
}
