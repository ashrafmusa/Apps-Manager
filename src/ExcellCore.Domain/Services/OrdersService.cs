using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class OrdersService : IOrdersService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public OrdersService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<LabOrderDto> PlaceLabOrderAsync(PlaceLabOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var order = new LabOrder
        {
            LabOrderId = _idGenerator.Create(),
            OrderNumber = $"LAB-{_idGenerator.Create():N}",
            TestCode = request.TestCode,
            Status = "Submitted",
            ExternalSystem = request.ExternalSystem,
            ExternalOrderId = request.ExternalOrderId,
            OrderingProvider = request.OrderingProvider,
            Notes = request.Notes,
            OrderedOnUtc = DateTime.UtcNow,
            Audit = AuditTrail.ForCreate(request.OrderingProvider ?? "orders", "orders")
        };

        await dbContext.LabOrders.AddAsync(order, cancellationToken).ConfigureAwait(false);

        var telemetry = new TelemetryEvent
        {
            TelemetryEventId = Guid.NewGuid(),
            EventType = "LabOrderOutbound",
            DurationMilliseconds = 0,
            CommandText = FormattableString.Invariant($"order:{order.OrderNumber};test:{order.TestCode};ext:{request.ExternalSystem ?? ""}"),
            OccurredOnUtc = DateTime.UtcNow,
            Audit = AuditTrail.ForCreate("orders", "orders")
        };

        await dbContext.TelemetryEvents.AddAsync(telemetry, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToLabOrderDto(order);
    }

    public async Task<OrderResultDto> IngestResultAsync(IngestResultRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var order = await dbContext.LabOrders
            .Include(o => o.Results)
            .FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Order not found.");

        var result = new OrderResult
        {
            OrderResultId = _idGenerator.Create(),
            LabOrderId = order.LabOrderId,
            ResultCode = request.ResultCode,
            ResultValue = request.ResultValue,
            ResultStatus = request.ResultStatus,
            Units = request.Units,
            ReferenceRange = request.ReferenceRange,
            CollectedOnUtc = request.CollectedOnUtc ?? DateTime.UtcNow,
            PerformedBy = request.PerformedBy,
            Audit = AuditTrail.ForCreate(request.PerformedBy ?? "orders", "orders")
        };

        order.Status = request.ResultStatus;
        order.Results.Add(result);

        var telemetry = new TelemetryEvent
        {
            TelemetryEventId = Guid.NewGuid(),
            EventType = request.Ack ? "LabResultAck" : "LabResultErr",
            DurationMilliseconds = request.Ack ? 1 : 0,
            CommandText = FormattableString.Invariant($"order:{order.OrderNumber};result:{request.ResultCode};status:{request.ResultStatus}"),
            OccurredOnUtc = DateTime.UtcNow,
            Audit = AuditTrail.ForCreate("orders", "orders")
        };

        await dbContext.OrderResults.AddAsync(result, cancellationToken).ConfigureAwait(false);
        await dbContext.TelemetryEvents.AddAsync(telemetry, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToOrderResultDto(result);
    }

    public async Task<OrdersDashboardDto> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var orders = await dbContext.LabOrders
            .Include(o => o.Results)
            .OrderByDescending(o => o.OrderedOnUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = orders.Select(ToLabOrderDto).ToList();
        return new OrdersDashboardDto(dtos);
    }

    private static LabOrderDto ToLabOrderDto(LabOrder order) => new(
        order.LabOrderId,
        order.OrderNumber,
        order.TestCode,
        order.Status,
        order.OrderedOnUtc,
        order.Results.Select(ToOrderResultDto).ToList());

    private static OrderResultDto ToOrderResultDto(OrderResult result) => new(
        result.OrderResultId,
        result.ResultCode,
        result.ResultValue,
        result.ResultStatus,
        result.CollectedOnUtc,
        result.Units,
        result.ReferenceRange,
        result.PerformedBy);
}
