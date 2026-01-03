using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class RetailOperationsService : IRetailOperationsService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public RetailOperationsService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory;
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<RetailDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var startOfDayUtc = utcNow.Date;

        var ticketEntities = await dbContext.Tickets
            .Include(t => t.RetailTransaction)
            .AsNoTracking()
            .OrderByDescending(t => t.RaisedOnUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var tickets = ticketEntities
            .Select(t => new RetailTicketDto(
                t.TicketId,
                string.IsNullOrWhiteSpace(t.TicketNumber)
                    ? t.RetailTransaction?.TicketNumber ?? FormattableString.Invariant($"TCK-{t.TicketId.ToString()[..8]}")
                    : t.TicketNumber,
                string.IsNullOrWhiteSpace(t.Channel)
                    ? t.RetailTransaction?.Channel ?? "Unknown"
                    : t.Channel,
                t.RetailTransaction?.TotalAmount ?? 0m,
                string.IsNullOrWhiteSpace(t.Status) ? "Open" : t.Status,
                t.RaisedOnUtc))
            .ToList();

        var todaysSalesSource = await dbContext.RetailTransactions
            .AsNoTracking()
            .Where(rt => rt.OccurredOnUtc >= startOfDayUtc)
            .Select(rt => rt.TotalAmount)
            .ToListAsync(cancellationToken);

        var todaysSales = todaysSalesSource.Count == 0 ? 0m : todaysSalesSource.Sum();

        var openOrders = ticketEntities.Count(t =>
            !string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(t.Status, "Resolved", StringComparison.OrdinalIgnoreCase));

        var loyaltyEnrollments = await dbContext.RetailTransactions
            .AsNoTracking()
            .Where(rt => rt.LoyaltyEnrollment && rt.OccurredOnUtc >= startOfDayUtc)
            .CountAsync(cancellationToken);

        var promotions = await BuildPromotionsAsync(dbContext, utcNow, cancellationToken);

        var suspended = await dbContext.RetailSuspendedTransactions
            .AsNoTracking()
            .OrderByDescending(s => s.SuspendedOnUtc)
            .Take(10)
            .Select(s => new RetailSuspendedTicketDto(
                s.RetailSuspendedTransactionId,
                string.IsNullOrWhiteSpace(s.TicketNumber)
                    ? FormattableString.Invariant($"SUS-{s.RetailSuspendedTransactionId.ToString().Substring(0, 8)}")
                    : s.TicketNumber,
                string.IsNullOrWhiteSpace(s.Channel) ? "In-Store" : s.Channel,
                s.Subtotal,
                string.IsNullOrWhiteSpace(s.Status) ? "Suspended" : s.Status,
                s.SuspendedOnUtc,
                s.ResumedOnUtc))
            .ToListAsync(cancellationToken);

        var returns = await dbContext.RetailReturns
            .AsNoTracking()
            .OrderByDescending(r => r.ReturnedOnUtc)
            .Take(10)
            .Select(r => new RetailReturnDto(
                r.RetailReturnId,
                string.IsNullOrWhiteSpace(r.TicketNumber) ? "N/A" : r.TicketNumber,
                string.IsNullOrWhiteSpace(r.Channel) ? "In-Store" : r.Channel,
                r.Amount,
                string.IsNullOrWhiteSpace(r.Status) ? "Pending" : r.Status,
                string.IsNullOrWhiteSpace(r.Reason) ? "Unspecified" : r.Reason,
                r.ReturnedOnUtc))
            .ToListAsync(cancellationToken);

        var returnsToday = returns.Count(r => r.ReturnedOnUtc.Date == utcNow.Date);
        var summary = new RetailSummaryDto(todaysSales, openOrders, loyaltyEnrollments, suspended.Count, returnsToday);

        return new RetailDashboardDto(tickets, promotions, suspended, returns, summary);
    }

    public async Task<RetailSuspendedTicketDto> SuspendAsync(SuspendTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var suspendedId = _idGenerator.Create();
        var ticketNumber = string.IsNullOrWhiteSpace(request.TicketNumber)
            ? FormattableString.Invariant($"SUS-{ShortId(suspendedId)}")
            : request.TicketNumber.Trim();

        var entity = new RetailSuspendedTransaction
        {
            RetailSuspendedTransactionId = suspendedId,
            TicketNumber = ticketNumber,
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? "In-Store" : request.Channel.Trim(),
            Subtotal = request.Subtotal,
            Status = "Suspended",
            SuspendedOnUtc = DateTime.UtcNow,
            Notes = request.Notes,
            PayloadJson = request.PayloadJson,
            Audit = new AuditTrail { CreatedBy = "workflow", SourceModule = "IS.Retail" }
        };

        dbContext.RetailSuspendedTransactions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RetailSuspendedTicketDto(
            entity.RetailSuspendedTransactionId,
            entity.TicketNumber,
            entity.Channel,
            entity.Subtotal,
            entity.Status,
            entity.SuspendedOnUtc,
            entity.ResumedOnUtc);
    }

    public async Task<RetailSuspendedTicketDto?> ResumeAsync(ResumeTransactionRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var entity = await dbContext.RetailSuspendedTransactions
            .FirstOrDefaultAsync(s => s.RetailSuspendedTransactionId == request.RetailSuspendedTransactionId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.ResumedOnUtc = request.ResumedOnUtc ?? DateTime.UtcNow;
        entity.Status = string.IsNullOrWhiteSpace(request.Status) ? "Resumed" : request.Status.Trim();
        entity.Audit.ModifiedOnUtc = DateTime.UtcNow;
        entity.Audit.ModifiedBy = "workflow";

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RetailSuspendedTicketDto(
            entity.RetailSuspendedTransactionId,
            entity.TicketNumber,
            entity.Channel,
            entity.Subtotal,
            entity.Status,
            entity.SuspendedOnUtc,
            entity.ResumedOnUtc);
    }

    public async Task<RetailReturnDto> RecordReturnAsync(RecordReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        await EnsureSeedDataAsync(dbContext, cancellationToken);

        var entity = new RetailReturn
        {
            RetailReturnId = _idGenerator.Create(),
            RetailTransactionId = request.RetailTransactionId,
            TicketNumber = request.TicketNumber.Trim(),
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? "In-Store" : request.Channel.Trim(),
            Amount = request.Amount,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status.Trim(),
            Reason = request.Reason.Trim(),
            ReturnedOnUtc = request.ReturnedOnUtc ?? DateTime.UtcNow,
            Audit = new AuditTrail { CreatedBy = "workflow", SourceModule = "IS.Retail" }
        };

        dbContext.RetailReturns.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RetailReturnDto(
            entity.RetailReturnId,
            entity.TicketNumber,
            entity.Channel,
            entity.Amount,
            entity.Status,
            entity.Reason,
            entity.ReturnedOnUtc);
    }

    private static async Task<IReadOnlyList<RetailPromotionDto>> BuildPromotionsAsync(ExcellCoreContext dbContext, DateTime utcNow, CancellationToken cancellationToken)
    {
        var horizonUtc = utcNow.AddDays(-7);

        var recentTransactions = await dbContext.RetailTransactions
            .AsNoTracking()
            .Where(rt => rt.OccurredOnUtc >= horizonUtc)
            .Select(rt => new { rt.Channel, rt.TotalAmount })
            .ToListAsync(cancellationToken);

        if (recentTransactions.Count == 0)
        {
            return Array.Empty<RetailPromotionDto>();
        }

        var channelStats = recentTransactions
            .GroupBy(rt => rt.Channel)
            .Select(group => new
            {
                Channel = group.Key,
                Count = group.Count(),
                Total = group.Sum(item => item.TotalAmount)
            })
            .OrderByDescending(x => x.Total)
            .ThenByDescending(x => x.Count)
            .Take(3)
            .ToList();

        var promotions = new List<RetailPromotionDto>(channelStats.Count);

        for (var index = 0; index < channelStats.Count; index++)
        {
            var stat = channelStats[index];
            var channelName = string.IsNullOrWhiteSpace(stat.Channel) ? "Omni-channel" : stat.Channel.Trim();
            var title = FormattableString.Invariant($"{channelName} spotlight");
            var description = FormattableString.Invariant(
                $"{stat.Count} orders totaling {stat.Total.ToString("C0", CultureInfo.CurrentCulture)} in the last 7 days.");
            var endsOn = utcNow.Date.AddDays(3 + index);
            promotions.Add(new RetailPromotionDto(title, description, endsOn));
        }

        return promotions;
    }

    private async Task EnsureSeedDataAsync(ExcellCoreContext dbContext, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        if (!await dbContext.RetailTransactions.AsNoTracking().AnyAsync(cancellationToken))
        {
            var transactions = new List<RetailTransaction>
            {
                CreateTransaction("POS-48312", "In-Store", 184.25m, "Completed", nowUtc.AddMinutes(-42), true, nowUtc.AddMinutes(-42), nowUtc.AddMinutes(-12)),
                CreateTransaction("WEB-90315", "Online", 92.10m, "Preparing", nowUtc.AddMinutes(-25), false, nowUtc.AddMinutes(-25), null),
                CreateTransaction("APP-77410", "Mobile", 57.40m, "Completed", nowUtc.AddHours(-1), true, nowUtc.AddHours(-1), nowUtc.AddMinutes(-5)),
                CreateTransaction("POS-48345", "In-Store", 248.90m, "Awaiting Pickup", nowUtc.AddMinutes(-12), false, nowUtc.AddMinutes(-12), null)
            };

            await dbContext.RetailTransactions.AddRangeAsync(transactions, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.RetailSuspendedTransactions.AsNoTracking().AnyAsync(cancellationToken))
        {
            var suspended = new List<RetailSuspendedTransaction>
            {
                new()
                {
                    RetailSuspendedTransactionId = _idGenerator.Create(),
                    TicketNumber = "POS-SUS-1001",
                    Channel = "In-Store",
                    Subtotal = 132.40m,
                    Status = "Suspended",
                    SuspendedOnUtc = nowUtc.AddMinutes(-20),
                    Notes = "Customer stepped away; holding cart",
                    Audit = new AuditTrail { CreatedBy = "seed", SourceModule = "IS.Retail" }
                },
                new()
                {
                    RetailSuspendedTransactionId = _idGenerator.Create(),
                    TicketNumber = "WEB-SUS-2042",
                    Channel = "Online",
                    Subtotal = 78.10m,
                    Status = "Resumed",
                    SuspendedOnUtc = nowUtc.AddHours(-1),
                    ResumedOnUtc = nowUtc.AddMinutes(-5),
                    Notes = "Customer resumed checkout after chat assist",
                    Audit = new AuditTrail { CreatedBy = "seed", SourceModule = "IS.Retail" }
                }
            };

            await dbContext.RetailSuspendedTransactions.AddRangeAsync(suspended, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.RetailReturns.AsNoTracking().AnyAsync(cancellationToken))
        {
            var returns = new List<RetailReturn>
            {
                new()
                {
                    RetailReturnId = _idGenerator.Create(),
                    RetailTransactionId = null,
                    TicketNumber = "POS-48312",
                    Channel = "In-Store",
                    Amount = 42.15m,
                    Status = "Pending",
                    Reason = "Size exchange",
                    ReturnedOnUtc = nowUtc.AddMinutes(-10),
                    Audit = new AuditTrail { CreatedBy = "seed", SourceModule = "IS.Retail" }
                },
                new()
                {
                    RetailReturnId = _idGenerator.Create(),
                    RetailTransactionId = null,
                    TicketNumber = "WEB-90315",
                    Channel = "Online",
                    Amount = 18.90m,
                    Status = "Completed",
                    Reason = "Damaged packaging",
                    ReturnedOnUtc = nowUtc.AddHours(-2),
                    Audit = new AuditTrail { CreatedBy = "seed", SourceModule = "IS.Retail" }
                }
            };

            await dbContext.RetailReturns.AddRangeAsync(returns, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private RetailTransaction CreateTransaction(
        string ticketNumber,
        string channel,
        decimal totalAmount,
        string status,
        DateTime occurredOnUtc,
        bool loyaltyEnrollment,
        DateTime raisedOnUtc,
        DateTime? resolvedOnUtc)
    {
        var transactionId = _idGenerator.Create();

        var transaction = new RetailTransaction
        {
            RetailTransactionId = transactionId,
            TicketNumber = ticketNumber,
            Channel = channel,
            TotalAmount = totalAmount,
            Status = status,
            OccurredOnUtc = occurredOnUtc,
            LoyaltyEnrollment = loyaltyEnrollment,
            Audit = new AuditTrail
            {
                CreatedBy = "seed",
                SourceModule = "IS.Retail"
            }
        };

        transaction.Tickets.Add(new Ticket
        {
            TicketId = _idGenerator.Create(),
            RetailTransactionId = transactionId,
            TicketNumber = ticketNumber,
            Title = FormattableString.Invariant($"Retail order {ticketNumber}"),
            Status = status,
            Channel = channel,
            RaisedOnUtc = raisedOnUtc,
            ResolvedOnUtc = resolvedOnUtc,
            Audit = new AuditTrail
            {
                CreatedBy = "seed",
                SourceModule = "IS.Retail"
            }
        });

        return transaction;
    }

    private static string ShortId(Guid id)
    {
        return id.ToString("N").Substring(0, 8);
    }
}
