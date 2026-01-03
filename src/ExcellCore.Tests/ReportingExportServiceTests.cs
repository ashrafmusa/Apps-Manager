using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Xunit;

namespace ExcellCore.Tests;

public sealed class ReportingExportServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly ReportingExportService _exportService;
    private readonly ReportingService _reportingService;

    public ReportingExportServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        var guidGenerator = new SequentialGuidGenerator();
        _exportService = new ReportingExportService(_factory, guidGenerator);
        _reportingService = new ReportingService(_factory, guidGenerator);
    }

    [Fact]
    public async Task GenerateExportAsync_ReturnsCsvContent()
    {
        var snapshot = await _reportingService.GetWorkspaceAsync();
        var targetSchedule = snapshot.Schedules.First(s => s.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase));
        var scheduleId = targetSchedule.ReportingScheduleId;

        var result = await _exportService.GenerateExportAsync(scheduleId);

        Assert.Equal(scheduleId, result.ReportingScheduleId);
        Assert.Equal("text/csv", result.ContentType);
        Assert.NotEmpty(result.Content);

        var csv = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Dashboard,Domain,Schedule", csv);
        Assert.Contains(targetSchedule.Name, csv);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
