using ExcellCore.Domain.Data;
using ExcellCore.Infrastructure.Modules;
using ExcellCore.Module.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Diagnostics;
using ExcellCore.Infrastructure.Services;

namespace ExcellCore.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExcellCore");
        Directory.CreateDirectory(dataDirectory);

        services.AddDbContextFactory<ExcellCoreContext>((serviceProvider, options) =>
        {
            var databasePath = Path.Combine(dataDirectory, "excellcore.db");
            options.UseSqlite($"Data Source={databasePath}");
            options.AddInterceptors(serviceProvider.GetRequiredService<QueryTelemetryInterceptor>());
        });

        services.TryAddSingleton<IModuleCatalog, ModuleCatalog>();
        services.TryAddSingleton<IModuleManifestProvider, FileSystemModuleManifestProvider>();
        services.TryAddSingleton<IModuleLoader, ModuleLoader>();
        services.TryAddSingleton<IModuleHost, ModuleHost>();
        services.TryAddSingleton<ISequentialGuidGenerator, SequentialGuidGenerator>();
        services.TryAddSingleton<IAgreementService, AgreementService>();
        services.TryAddSingleton<IRetailOperationsService, RetailOperationsService>();
        services.TryAddSingleton<IClinicalWorkflowService, ClinicalWorkflowService>();
        services.TryAddSingleton<ICorporatePortfolioService, CorporatePortfolioService>();
        services.TryAddSingleton<IReportingService, ReportingService>();
        services.TryAddSingleton<IReportingExportService, ReportingExportService>();
        services.TryAddSingleton<ISlaReportingService, SlaReportingService>();
        services.TryAddSingleton<ITelemetryService, TelemetryService>();
        services.TryAddSingleton<IInventoryAnalyticsService, InventoryAnalyticsService>();
        services.TryAddSingleton<ILocalizationService, LocalizationService>();
        services.TryAddSingleton<IMetadataFormService, MetadataFormService>();
        services.TryAddSingleton<ILocalizationContext, LocalizationContext>();

        services.AddMemoryCache();
        services.AddSingleton<QueryTelemetryInterceptor>();
        services.AddSingleton<PartyService>();
        services.TryAddSingleton<IPartyService>(sp => new CachedPartyService(
            sp.GetRequiredService<PartyService>(),
            sp.GetRequiredService<IMemoryCache>()));
        return services;
    }
}
