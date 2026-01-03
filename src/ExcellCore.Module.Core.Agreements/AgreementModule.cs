using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Core.Agreements.ViewModels;
using ExcellCore.Module.Core.Agreements.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.Core.Agreements;

public sealed class AgreementModule : IModule, IModuleDescriptor
{
    public string ModuleId => "Core.Agreements";
    public string DisplayName => "Agreement Engine";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(AgreementModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AgreementWorkspaceViewModel>();
        services.AddSingleton<AgreementWorkspaceView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var moduleHost = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<AgreementWorkspaceView>();
        moduleHost.RegisterView(ModuleId, view);
    }
}
