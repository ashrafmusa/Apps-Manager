using System;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class ReportingServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly ReportingService _service;

    public ReportingServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _service = new ReportingService(_factory, new SequentialGuidGenerator());
    }

    [Fact]
    public async Task GetWorkspaceAsync_SeedsDashboardsAndSchedules()
    {
        var snapshot = await _service.GetWorkspaceAsync();

        Assert.Equal(3, snapshot.Dashboards.Count);
        Assert.All(snapshot.Dashboards, dashboard => Assert.True(dashboard.IsActive));
        Assert.Equal(3, snapshot.Schedules.Count);
        Assert.All(snapshot.Schedules, schedule => Assert.True(schedule.IsEnabled));

        Assert.Equal(3, snapshot.Summary.ActiveDashboards);
        Assert.Equal(3, snapshot.Summary.ScheduledExports);
        Assert.InRange(snapshot.Summary.ImminentRuns, 1, 3);
    }

    [Fact]
    public async Task GetWorkspaceAsync_DoesNotDuplicateSeedData()
    {
        await _service.GetWorkspaceAsync();
        await _service.GetWorkspaceAsync();

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var dashboardCount = await verificationContext.ReportingDashboards.CountAsync();
        var scheduleCount = await verificationContext.ReportingSchedules.CountAsync();

        Assert.Equal(3, dashboardCount);
        Assert.Equal(3, scheduleCount);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
