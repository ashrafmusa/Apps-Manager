using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record ClinicalDashboardDto(
    IReadOnlyList<MedicationOrderDto> Orders,
    IReadOnlyList<MedicationAdministrationDto> UpcomingAdministrations,
    ClinicalSummaryDto Summary);

public sealed record PatientJourneyDto(
    Guid PatientId,
    string PatientName,
    IReadOnlyList<JourneyEventDto> Events,
    IReadOnlyList<RoleWorklistItemDto> Worklist,
    IReadOnlyList<JourneySignalDto> Signals);

public sealed record JourneyEventDto(
    Guid EventId,
    string Type,
    string Title,
    string Status,
    string Detail,
    DateTime WhenUtc,
    Guid? MedicationOrderId,
    string? SignalSeverity = null,
    string? SignalMessage = null);

public sealed record RoleWorklistItemDto(
    string Role,
    string Title,
    string Status,
    DateTime DueUtc,
    Guid? MedicationOrderId,
    Guid? MedicationAdministrationId);

public sealed record JourneySignalDto(
    string Severity,
    string Message,
    DateTime? WhenUtc,
    Guid? MedicationOrderId = null,
    Guid? MedicationAdministrationId = null);

public sealed record DispenseEventDto(
    Guid DispenseEventId,
    Guid MedicationOrderId,
    string InventoryItem,
    decimal Quantity,
    string Unit,
    DateTime DispensedOnUtc,
    string? DispensedBy,
    string? Location);

public sealed record MedicationOrderDto(
    Guid MedicationOrderId,
    string OrderNumber,
    string PatientName,
    string Medication,
    string Dose,
    string Route,
    string Frequency,
    string Status,
    string OrderingProvider,
    DateTime OrderedOnUtc,
    DateTime? StartOnUtc);

public sealed record MedicationAdministrationDto(
    Guid MedicationAdministrationId,
    Guid MedicationOrderId,
    string OrderNumber,
    string Medication,
    DateTime ScheduledForUtc,
    DateTime? AdministeredOnUtc,
    string Status,
    string? PerformedBy);

public sealed record ClinicalSummaryDto(
    int ActiveOrders,
    int PendingAdministrations,
    int DispensesToday);

public sealed record CreateMedicationOrderRequest(
    Guid? PartyId,
    string PatientName,
    string Medication,
    string Dose,
    string Route,
    string Frequency,
    string OrderingProvider,
    DateTime? StartOnUtc,
    string? OrderNumber = null,
    string? Status = null,
    string? Notes = null,
    bool ScheduleFirstDose = true);

public sealed record RecordDispenseRequest(
    Guid MedicationOrderId,
    string InventoryItem,
    decimal Quantity,
    string Unit,
    DateTime? DispensedOnUtc = null,
    string? DispensedBy = null,
    string? Location = null);

public sealed record RecordAdministrationRequest(
    Guid MedicationOrderId,
    DateTime ScheduledForUtc,
    DateTime? AdministeredOnUtc,
    string Status,
    string? PerformedBy,
    string? Notes = null);
