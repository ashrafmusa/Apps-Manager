using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record CorporateDashboardDto(
    IReadOnlyList<CorporateContractDto> Contracts,
    IReadOnlyList<CorporateAllocationDto> Allocations,
    CorporateSummaryDto Summary);

public sealed record CorporateContractDto(
    Guid CorporateContractId,
    string ContractCode,
    string CustomerName,
    decimal ContractValue,
    DateTime RenewalDate,
    string Category,
    string Program,
    decimal AllocationRatio,
    string AllocationStatus);

public sealed record CorporateAllocationDto(
    string Program,
    decimal AllocationRatio,
    string Status);

public sealed record CorporateSummaryDto(
    decimal AnnualizedRevenue,
    int RenewalsDue,
    int AllocationRisks);
