using System;
using System.Linq;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class CorporatePortfolioServiceTests : IDisposable
{
    private readonly TestSqliteContextFactory _factory;
    private readonly CorporatePortfolioService _service;

    public CorporatePortfolioServiceTests()
    {
        _factory = new TestSqliteContextFactory();
        _service = new CorporatePortfolioService(_factory, new SequentialGuidGenerator());
    }

    [Fact]
    public async Task GetDashboardAsync_SeedsContractsAndReturnsSummary()
    {
        var dashboard = await _service.GetDashboardAsync();

        Assert.Equal(3, dashboard.Contracts.Count);
        Assert.Equal(3, dashboard.Allocations.Count);
        Assert.Equal(4_430_500m, dashboard.Summary.AnnualizedRevenue);
        Assert.Equal(2, dashboard.Summary.RenewalsDue);
        Assert.Equal(2, dashboard.Summary.AllocationRisks);
        Assert.Equal("Field services", dashboard.Allocations.First().Program);
        Assert.Contains(dashboard.Contracts, contract => contract.ContractCode == "CORP-2024-018");
    }

    [Fact]
    public async Task GetDashboardAsync_DoesNotDuplicateSeedData()
    {
        await _service.GetDashboardAsync();
        await _service.GetDashboardAsync();

        await using var verificationContext = await _factory.CreateDbContextAsync();
        var contractCount = await verificationContext.CorporateContracts.CountAsync();

        Assert.Equal(3, contractCount);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
