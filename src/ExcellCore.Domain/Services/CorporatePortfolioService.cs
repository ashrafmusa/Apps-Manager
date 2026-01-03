using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class CorporatePortfolioService : ICorporatePortfolioService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public CorporatePortfolioService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory;
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<CorporateDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var contracts = await dbContext.CorporateContracts
            .AsNoTracking()
            .OrderBy(c => c.RenewalDate)
            .ToListAsync(cancellationToken);

        var contractDtos = contracts
            .Select(c => new CorporateContractDto(
                c.CorporateContractId,
                c.ContractCode,
                c.CustomerName,
                c.ContractValue,
                c.RenewalDate,
                c.Category,
                c.Program,
                c.AllocationRatio,
                c.AllocationStatus))
            .ToList();

        var allocationDtos = contracts
            .Where(c => !string.IsNullOrWhiteSpace(c.Program))
            .GroupBy(c => c.Program.Trim())
            .Select(group =>
            {
                var latest = group.OrderByDescending(c => c.RenewalDate).First();
                var averageRatio = group.Average(c => c.AllocationRatio);
                return new CorporateAllocationDto(
                    group.Key,
                    decimal.Round(averageRatio, 2, MidpointRounding.AwayFromZero),
                    latest.AllocationStatus);
            })
            .OrderByDescending(a => a.AllocationRatio)
            .ToList();

        var annualizedRevenue = contractDtos.Sum(c => c.ContractValue);
        var renewalsDueThreshold = DateTime.UtcNow.Date.AddDays(60);
        var renewalsDue = contracts.Count(c => c.RenewalDate.Date <= renewalsDueThreshold);
        var allocationRisks = allocationDtos.Count(a => !string.Equals(a.Status, "On track", StringComparison.OrdinalIgnoreCase));

        var summary = new CorporateSummaryDto(annualizedRevenue, renewalsDue, allocationRisks);
        return new CorporateDashboardDto(contractDtos, allocationDtos, summary);
    }

    private async Task EnsureSeedDataAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.CorporateContracts.AsNoTracking().AnyAsync(cancellationToken))
        {
            return;
        }

        var today = DateTime.UtcNow.Date;

        var contracts = new List<CorporateContract>
        {
            CreateContract("CORP-2024-018", "Evergreen Holdings", 1_200_000m, today.AddDays(30), "Renewal", "Analytics squad", 0.82m, "On track"),
            CreateContract("CORP-2024-022", "Summit Infrastructure", 780_500m, today.AddDays(45), "Expansion", "Field services", 1.14m, "Over allocation"),
            CreateContract("CORP-2025-004", "Blue Horizon Pharma", 2_450_000m, today.AddDays(75), "New", "Clinical liaison", 0.94m, "Watch")
        };

        await dbContext.CorporateContracts.AddRangeAsync(contracts, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private CorporateContract CreateContract(
        string contractCode,
        string customerName,
        decimal contractValue,
        DateTime renewalDate,
        string category,
        string program,
        decimal allocationRatio,
        string allocationStatus)
    {
        return new CorporateContract
        {
            CorporateContractId = _idGenerator.Create(),
            ContractCode = contractCode,
            CustomerName = customerName,
            ContractValue = contractValue,
            RenewalDate = renewalDate,
            Category = category,
            Program = program,
            AllocationRatio = allocationRatio,
            AllocationStatus = allocationStatus,
            Audit = new AuditTrail
            {
                CreatedBy = "seed",
                SourceModule = "IS.Corporate"
            }
        };
    }
}
