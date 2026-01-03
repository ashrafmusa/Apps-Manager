using System;
using System.Linq;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class RetailOperationsServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly RetailOperationsService _service;

    public RetailOperationsServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _service = new RetailOperationsService(_factory, new SequentialGuidGenerator());
    }

    [Fact]
    public async Task GetDashboardAsync_SeedsDataAndReturnsSummary()
    {
        var dashboard = await _service.GetDashboardAsync();

        Assert.Equal(4, dashboard.Tickets.Count);
        Assert.Equal(3, dashboard.Promotions.Count);
        Assert.Equal(582.65m, dashboard.Summary.DailySales);
        Assert.Equal(2, dashboard.Summary.OpenOrders);
        Assert.Equal(2, dashboard.Summary.LoyaltyEnrollments);
        Assert.Contains(dashboard.Promotions, promotion => promotion.Name.Equals("In-Store spotlight", StringComparison.OrdinalIgnoreCase));
        Assert.All(dashboard.Tickets, ticket => Assert.False(string.IsNullOrWhiteSpace(ticket.TicketNumber)));
    }

    [Fact]
    public async Task GetDashboardAsync_DoesNotDuplicateSeedData()
    {
        await _service.GetDashboardAsync();
        await _service.GetDashboardAsync();

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var transactionCount = await verificationContext.RetailTransactions.CountAsync();
        var ticketCount = await verificationContext.Tickets.CountAsync();

        Assert.Equal(4, transactionCount);
        Assert.Equal(4, ticketCount);
    }

    [Fact]
    public async Task SuspendAsync_AddsSuspendedCart()
    {
        var dashboard = await _service.GetDashboardAsync();
        var initial = dashboard.Suspended.Count;

        var result = await _service.SuspendAsync(new SuspendTransactionRequest("In-Store", 64.75m, null, "Test suspend", null));

        Assert.Equal("Suspended", result.Status);

        var refreshed = await _service.GetDashboardAsync();
        Assert.Equal(initial + 1, refreshed.Suspended.Count);
    }

    [Fact]
    public async Task ResumeAsync_UpdatesStatusAndTimestamp()
    {
        var dashboard = await _service.GetDashboardAsync();
        var target = dashboard.Suspended.FirstOrDefault(s => s.ResumedOnUtc is null || s.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(target);

        var resumed = await _service.ResumeAsync(new ResumeTransactionRequest(target!.RetailSuspendedTransactionId));

        Assert.NotNull(resumed);
        Assert.Equal("Resumed", resumed!.Status);
        Assert.NotNull(resumed.ResumedOnUtc);
    }

    [Fact]
    public async Task RecordReturnAsync_AddsReturnEntry()
    {
        var before = await _service.GetDashboardAsync();
        var result = await _service.RecordReturnAsync(new RecordReturnRequest("POS-RET-TEST", "In-Store", 19.99m, "Test return"));

        Assert.Equal("Pending", result.Status);

        var after = await _service.GetDashboardAsync();
        Assert.Equal(before.Returns.Count + 1, after.Returns.Count);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
