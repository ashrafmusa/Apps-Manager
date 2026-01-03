using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IAgreementService
{
    Task<IReadOnlyList<AgreementSummaryDto>> SearchAsync(string? searchTerm, CancellationToken cancellationToken = default);
    Task<AgreementDetailDto?> GetAsync(Guid agreementId, CancellationToken cancellationToken = default);
    Task<AgreementDetailDto> SaveAsync(AgreementDetailDto detail, CancellationToken cancellationToken = default);
    Task<PricingResultDto> CalculatePriceAsync(Guid agreementId, string serviceCode, decimal listPrice, int quantity, CancellationToken cancellationToken = default);
    Task<AgreementDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<AgreementApprovalTriageSnapshotDto> GetApprovalTriageAsync(CancellationToken cancellationToken = default);
    Task<AgreementDetailDto> RequestApprovalAsync(Guid agreementId, string approver, CancellationToken cancellationToken = default);
    Task<AgreementDetailDto> CompleteApprovalAsync(Guid agreementId, Guid approvalId, string decision, string? comments, CancellationToken cancellationToken = default);
    Task<AgreementDetailDto> ScheduleRenewalAsync(Guid agreementId, DateOnly renewalDate, bool autoRenew, CancellationToken cancellationToken = default);
    Task<AgreementDetailDto> MarkRenewedAsync(Guid agreementId, DateOnly renewedOn, CancellationToken cancellationToken = default);
    Task<AgreementValidationResultDto> ValidateWorkflowAsync(Guid agreementId, CancellationToken cancellationToken = default);
    Task<int> EscalatePendingApprovalsAsync(CancellationToken cancellationToken = default);
    Task SendApprovalReminderAsync(Guid agreementId, Guid approvalId, CancellationToken cancellationToken = default);
    Task FastTrackApprovalAsync(Guid agreementId, Guid approvalId, CancellationToken cancellationToken = default);
}
