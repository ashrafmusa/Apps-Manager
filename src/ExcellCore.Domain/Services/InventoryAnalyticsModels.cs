using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record InventoryStockItemDto(
    string Sku,
    string ItemName,
    string Location,
    decimal QuantityOnHand,
    decimal ReorderPoint,
    decimal OnOrder,
    DateTime LastMovementUtc);

public sealed record InventoryStockSnapshotDto(IReadOnlyList<InventoryStockItemDto> Items);

public sealed record InventoryAnomalySignalDto(
    string Sku,
    string ItemName,
    string Location,
    string SignalType,
    string Severity,
    double VelocityPerDay,
    decimal QuantityOnHand,
    decimal ReorderPoint,
    string Message,
    DateTime DetectedOnUtc);

public sealed record InventoryAnomalyResultDto(
    IReadOnlyList<InventoryAnomalySignalDto> Signals,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);
