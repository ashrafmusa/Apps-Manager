using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Extensions.Reporting.ViewModels;
using ExcellCore.Module.Extensions.Reporting.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExcellCore.Module.Extensions.Reporting;

public sealed class ReportingModule : IModule, IModuleDescriptor
{
    public string ModuleId => "Extensions.Reporting";
    public string DisplayName => "Reporting Hub";
    public Version Version => new(0, 1, 0, 0);
    public bool IsEnabled => true;
    public Type EntryPoint => typeof(ReportingModule);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ReportingWorkspaceViewModel>();
        services.AddSingleton<TelemetryWorkspaceViewModel>();
        services.AddSingleton<SlaWorkspaceViewModel>();
        services.AddSingleton<ReportingWorkspaceView>();
        services.AddSingleton<TelemetryWorkspaceView>();
        services.AddSingleton<SlaWorkspaceView>();
        services.AddSingleton<ReportingModuleView>();
    }

    public void Configure(IServiceProvider serviceProvider)
    {
        var host = serviceProvider.GetRequiredService<IModuleHost>();
        var view = serviceProvider.GetRequiredService<ReportingModuleView>();
        host.RegisterView(ModuleId, view);
    }
}
