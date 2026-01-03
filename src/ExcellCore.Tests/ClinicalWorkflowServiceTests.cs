using System;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class ClinicalWorkflowServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly ClinicalWorkflowService _service;

    public ClinicalWorkflowServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _service = new ClinicalWorkflowService(_factory, new SequentialGuidGenerator());
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsOrderAndSchedule()
    {
        var request = new CreateMedicationOrderRequest(
            null,
            "Test Patient",
            "TestMed",
            "10 mg",
            "PO",
            "BID",
            "Dr. Test",
            DateTime.UtcNow.AddHours(1),
            null,
            "Active",
            "Notes",
            true);

        var created = await _service.CreateOrderAsync(request);
        var dashboard = await _service.GetDashboardAsync();

        Assert.Contains(dashboard.Orders, o => o.MedicationOrderId == created.MedicationOrderId);
        Assert.True(dashboard.Summary.PendingAdministrations >= 1);
    }

    [Fact]
    public async Task RecordDispenseAsync_AddsDispenseEvent()
    {
        var dashboard = await _service.GetDashboardAsync();
        var order = dashboard.Orders[0];

        await _service.RecordDispenseAsync(new RecordDispenseRequest(order.MedicationOrderId, "TEST-ITEM", 1, "unit"));

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var dispenseCount = await verificationContext.DispenseEvents.CountAsync();

        Assert.True(dispenseCount > 0);
    }

    [Fact]
    public async Task CompleteNextAdministrationAsync_MarksNextAsCompleted()
    {
        var dashboard = await _service.GetDashboardAsync();
        var order = dashboard.Orders[0];

        var completed = await _service.CompleteNextAdministrationAsync(order.MedicationOrderId, "RN Test");

        Assert.NotNull(completed);
        Assert.Equal("Completed", completed!.Status);
        Assert.NotNull(completed.AdministeredOnUtc);

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var stored = await verificationContext.MedicationAdministrations
            .FirstAsync(a => a.MedicationAdministrationId == completed.MedicationAdministrationId);

        Assert.NotNull(stored.AdministeredOnUtc);
        Assert.Equal("Completed", stored.Status);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
