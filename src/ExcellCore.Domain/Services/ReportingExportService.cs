using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class ReportingExportService : IReportingExportService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public ReportingExportService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<ReportingExportResultDto> GenerateExportAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        if (!await dbContext.ReportingDashboards.AsNoTracking().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            await SeedAsync(dbContext, cancellationToken).ConfigureAwait(false);
        }

        var schedule = await dbContext.ReportingSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ReportingScheduleId == scheduleId, cancellationToken)
            .ConfigureAwait(false);

        if (schedule is null)
        {
            throw new InvalidOperationException($"Reporting schedule {scheduleId} was not found.");
        }

        var dashboards = await dbContext.ReportingDashboards
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var generatedOnUtc = DateTime.UtcNow;
        var content = BuildCsv(schedule, dashboards, generatedOnUtc);
        var fileName = FormattableString.Invariant($"{Sanitize(schedule.Name)}-{generatedOnUtc:yyyyMMddHHmmss}.csv");
        var contentType = ResolveContentType(schedule.Format);

        return new ReportingExportResultDto(schedule.ReportingScheduleId, fileName, contentType, content, generatedOnUtc);
    }

    private byte[] BuildCsv(ReportingSchedule schedule, IReadOnlyCollection<ReportingDashboard> dashboards, DateTime generatedOnUtc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Dashboard,Domain,Schedule,GeneratedOnUtc");

        foreach (var dashboard in dashboards)
        {
            sb.AppendLine(FormattableString.Invariant($"{dashboard.Name},{dashboard.Domain},{schedule.Name},{generatedOnUtc:O}"));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string ResolveContentType(string format)
    {
        return format?.Equals("Excel", StringComparison.OrdinalIgnoreCase) is true
            ? "application/vnd.ms-excel"
            : "text/csv";
    }

    private static string Sanitize(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "export" : cleaned;
    }

    private async Task SeedAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var dashboards = new[]
        {
            CreateDashboard("Clinical Throughput", "Admissions, discharges, bed utilization", "Operations"),
            CreateDashboard("Retail Conversion", "Funnel metrics and promotion uplift", "Sales"),
            CreateDashboard("Revenue Integrity", "Charge capture, denials, reimbursements", "Finance")
        };

        var schedules = new[]
        {
            CreateSchedule("Daily Cashflow", "CSV", TimeSpan.FromHours(24), nowUtc.Date.AddHours(22)),
            CreateSchedule("Clinic Census", "Excel", TimeSpan.FromHours(6), nowUtc.AddHours(4)),
            CreateSchedule("POS Performance", "Power BI", TimeSpan.FromHours(12), nowUtc.AddHours(6))
        };

        await dbContext.ReportingDashboards.AddRangeAsync(dashboards, cancellationToken).ConfigureAwait(false);
        await dbContext.ReportingSchedules.AddRangeAsync(schedules, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
                CreatedBy = "export-seed",
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
                CreatedBy = "export-seed",
                SourceModule = "Extensions.Reporting"
            }
        };
    }
}
