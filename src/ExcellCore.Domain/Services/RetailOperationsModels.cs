using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record RetailDashboardDto(
    IReadOnlyList<RetailTicketDto> Tickets,
    IReadOnlyList<RetailPromotionDto> Promotions,
    IReadOnlyList<RetailSuspendedTicketDto> Suspended,
    IReadOnlyList<RetailReturnDto> Returns,
    RetailSummaryDto Summary);

public sealed record RetailTicketDto(
    Guid TicketId,
    string TicketNumber,
    string Channel,
    decimal TotalAmount,
    string Status,
    DateTime RaisedOnUtc);

public sealed record RetailPromotionDto(
    string Name,
    string Description,
    DateTime EndsOnUtc);

public sealed record RetailSuspendedTicketDto(
    Guid RetailSuspendedTransactionId,
    string TicketNumber,
    string Channel,
    decimal Subtotal,
    string Status,
    DateTime SuspendedOnUtc,
    DateTime? ResumedOnUtc);

public sealed record RetailReturnDto(
    Guid RetailReturnId,
    string TicketNumber,
    string Channel,
    decimal Amount,
    string Status,
    string Reason,
    DateTime ReturnedOnUtc);

public sealed record RetailSummaryDto(
    decimal DailySales,
    int OpenOrders,
    int LoyaltyEnrollments,
    int SuspendedCount,
    int ReturnsToday);

public sealed record SuspendTransactionRequest(
    string Channel,
    decimal Subtotal,
    string? TicketNumber = null,
    string? Notes = null,
    string? PayloadJson = null);

public sealed record ResumeTransactionRequest(
    Guid RetailSuspendedTransactionId,
    DateTime? ResumedOnUtc = null,
    string? Status = null);

public sealed record RecordReturnRequest(
    string TicketNumber,
    string Channel,
    decimal Amount,
    string Reason,
    string Status = "Pending",
    Guid? RetailTransactionId = null,
    DateTime? ReturnedOnUtc = null);
