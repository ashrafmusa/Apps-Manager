using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record PlaceLabOrderRequest(string TestCode, string? PatientName = null, string? ExternalSystem = null, string? ExternalOrderId = null, string? OrderingProvider = null, string? Notes = null);

public sealed record IngestResultRequest(string OrderNumber, string ResultCode, string ResultValue, string ResultStatus = "Final", DateTime? CollectedOnUtc = null, string? Units = null, string? ReferenceRange = null, string? PerformedBy = null, bool Ack = true);

public sealed record LabOrderDto(Guid LabOrderId, string OrderNumber, string TestCode, string Status, DateTime OrderedOnUtc, IReadOnlyList<OrderResultDto> Results);

public sealed record OrderResultDto(Guid OrderResultId, string ResultCode, string ResultValue, string ResultStatus, DateTime CollectedOnUtc, string? Units, string? ReferenceRange, string? PerformedBy);

public sealed record OrdersDashboardDto(IReadOnlyList<LabOrderDto> Orders);
