using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Core.Identity.ViewModels;
using ExcellCore.Module.Core.Identity.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.Core.Identity;

public sealed class IdentityModule : IModule, IModuleDescriptor
{
    public string ModuleId => "Core.Identity";
    public string DisplayName => "Master Identity Registry";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(IdentityModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IdentityDashboardViewModel>();
        services.AddSingleton<IdentityDashboardView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var moduleHost = serviceProvider.GetRequiredService<IModuleHost>();
        var dashboardView = serviceProvider.GetRequiredService<IdentityDashboardView>();
        moduleHost.RegisterView(ModuleId, dashboardView);
    }
}
