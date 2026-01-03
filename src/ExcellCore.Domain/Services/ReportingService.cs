using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class ReportingService : IReportingService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public ReportingService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory;
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<ReportingWorkspaceSnapshotDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        if (!await dbContext.ReportingDashboards.AsNoTracking().AnyAsync(cancellationToken))
        {
            await SeedAsync(dbContext, cancellationToken);
        }

        var nowUtc = DateTime.UtcNow;
        var dashboards = await dbContext.ReportingDashboards
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new ReportingDashboardDto(d.ReportingDashboardId, d.Name, d.Description, d.Domain, d.IsActive))
            .ToListAsync(cancellationToken);

        var schedules = await dbContext.ReportingSchedules
            .AsNoTracking()
            .OrderBy(s => s.NextRunUtc)
            .Select(s => new ReportingScheduleDto(s.ReportingScheduleId, s.Name, s.Format, s.Cadence, s.NextRunUtc, s.IsEnabled))
            .ToListAsync(cancellationToken);

        var activeDashboards = dashboards.Count(d => d.IsActive);
        var scheduledExports = schedules.Count(s => s.IsEnabled);
        var imminentRuns = schedules.Count(s => s.IsEnabled && s.NextRunUtc <= nowUtc.AddHours(6));

        var summary = new ReportingWorkspaceSummaryDto(activeDashboards, scheduledExports, imminentRuns);
        return new ReportingWorkspaceSnapshotDto(summary, dashboards, schedules);
    }

    private async Task SeedAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var dashboards = new List<ReportingDashboard>
        {
            CreateDashboard("Clinical Throughput", "Admissions, discharges, bed utilization", "Operations"),
            CreateDashboard("Retail Conversion", "Funnel metrics and promotion uplift", "Sales"),
            CreateDashboard("Revenue Integrity", "Charge capture, denials, reimbursements", "Finance")
        };

        var schedules = new List<ReportingSchedule>
        {
            CreateSchedule("Daily Cashflow", "CSV", TimeSpan.FromHours(24), nowUtc.Date.AddHours(22)),
            CreateSchedule("Clinic Census", "Excel", TimeSpan.FromHours(6), nowUtc.AddHours(4)),
            CreateSchedule("POS Performance", "Power BI", TimeSpan.FromHours(12), nowUtc.AddHours(6))
        };

        await dbContext.ReportingDashboards.AddRangeAsync(dashboards, cancellationToken);
        await dbContext.ReportingSchedules.AddRangeAsync(schedules, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private ReportingDashboard CreateDashboard(string name, string description, string domain)
    {
        return new ReportingDashboard
        {
            ReportingDashboardId = _idGenerator.Create(),
            Name = name,
            Description = description,
            Domain = domain,
            IsActive = true,
            Audit = new AuditTrail
            {
                CreatedBy = "seed",
                SourceModule = "Extensions.Reporting"
            }
        };
    }

    private ReportingSchedule CreateSchedule(string name, string format, TimeSpan cadence, DateTime nextRunUtc)
    {
        var normalizedNextRun = DateTime.SpecifyKind(nextRunUtc, DateTimeKind.Utc);
        return new ReportingSchedule
        {
            ReportingScheduleId = _idGenerator.Create(),
            Name = name,
            Format = format,
            Cadence = cadence,
            NextRunUtc = normalizedNextRun,
            IsEnabled = true,
            Audit = new AuditTrail
            {
                CreatedBy = "seed",
                SourceModule = "Extensions.Reporting"
            }
        };
    }
}
