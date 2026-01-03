using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ExcellCore.Infrastructure;
using ExcellCore.Module.Abstractions;
using ExcellCore.Shell.ViewModels;
using ExcellCore.Shell.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExcellCore.Infrastructure.Modules;
using ExcellCore.Domain.Data;
using Microsoft.EntityFrameworkCore;
using ExcellCore.Domain.Events;
using ExcellCore.Shell.Services;

namespace ExcellCore.Shell;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private AgreementEscalationMonitor? _escalationMonitor;
    private TelemetryAggregationWorker? _telemetryWorker;
    private InventoryAnomalyWorker? _inventoryAnomalyWorker;
    private readonly string _logFilePath;

    public App()
    {
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExcellCore");
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, "shell.log");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            LogInfo("Startup begin");
            var services = new ServiceCollection();
            await ComposeServicesAsync(services, LogInfo);
            LogInfo("Services composed");
            _serviceProvider = services.BuildServiceProvider();

            _escalationMonitor = _serviceProvider.GetRequiredService<AgreementEscalationMonitor>();
            _escalationMonitor.Start();

            _telemetryWorker = _serviceProvider.GetRequiredService<TelemetryAggregationWorker>();
            _telemetryWorker.Start();

            _inventoryAnomalyWorker = _serviceProvider.GetRequiredService<InventoryAnomalyWorker>();
            _inventoryAnomalyWorker.Start();

            await ActivateModulesAsync();
            LogInfo("Modules activated");

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            LogInfo("Main window shown");
        }
        catch (Exception ex)
        {
            LogException("Startup failure", ex);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _escalationMonitor?.Dispose();
        _telemetryWorker?.Dispose();
        _inventoryAnomalyWorker?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static async Task ComposeServicesAsync(IServiceCollection services, Action<string> logInfo)
    {
        services.AddLogging(builder => builder.AddConsole());
        var moduleCatalog = new ModuleCatalog();
        var moduleHost = new ModuleHost();

        logInfo?.Invoke("Compose: registering infrastructure");
        services.AddSingleton<IModuleCatalog>(moduleCatalog);
        services.AddSingleton<IModuleHost>(moduleHost);

        services.AddInfrastructure();
        services.AddSingleton<INotificationCenter, NotificationCenter>();
        services.AddSingleton<IAppEventBus, ShellEventBus>();
        services.AddSingleton<IAppEventHandler<ApprovalEscalatedEvent>, ApprovalEscalatedNotificationHandler>();
        services.AddSingleton<IAppEventHandler<ApprovalReminderEvent>, ApprovalReminderNotificationHandler>();
        services.AddSingleton<AgreementEscalationMonitor>();
        services.AddSingleton<TelemetryAggregationWorker>();
        services.AddSingleton<InventoryAnomalyWorker>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        logInfo?.Invoke("Compose: building temp provider");
        var tempProvider = services.BuildServiceProvider();
        logInfo?.Invoke("Compose: temp provider built");
        var moduleLoader = tempProvider.GetRequiredService<IModuleLoader>();
        logInfo?.Invoke("Compose: module loader resolved");
        logInfo?.Invoke("Compose: loading manifests");
        await moduleLoader.LoadModulesAsync(services);

        var contextFactory = tempProvider.GetRequiredService<IDbContextFactory<ExcellCoreContext>>();
        logInfo?.Invoke("Compose: creating DbContext");
        await using var dbContext = await contextFactory.CreateDbContextAsync();
        logInfo?.Invoke("Compose: ensuring migrations");
        await dbContext.EnsureDatabaseMigratedAsync(CancellationToken.None);
        tempProvider.Dispose();
        logInfo?.Invoke("Compose: complete");
    }

    private async Task ActivateModulesAsync()
    {
        if (_serviceProvider is null) return;
        var moduleLoader = _serviceProvider.GetRequiredService<IModuleLoader>();
        await moduleLoader.ActivateModulesAsync(_serviceProvider);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("Dispatcher unhandled exception", e.Exception);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("AppDomain unhandled exception", ex);
        }
    }

    private void LogException(string message, Exception exception)
    {
        try
        {
            File.AppendAllText(_logFilePath, $"[{DateTime.UtcNow:O}] {message}:{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Swallow logging errors to avoid recursive failures.
        }
    }

    private void LogInfo(string message)
    {
        try
        {
            File.AppendAllText(_logFilePath, $"[{DateTime.UtcNow:O}] INFO: {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures for non-critical info traces.
        }
    }
}
