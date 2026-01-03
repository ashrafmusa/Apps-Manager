using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Events;
using ExcellCore.Domain.Services.Pricing;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class AgreementService : IAgreementService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;
    private readonly IAppEventBus? _eventBus;
    private static readonly TimeSpan ApprovalEscalationThreshold = TimeSpan.FromHours(24);

    public AgreementService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator, IAppEventBus? eventBus = null)
    {
        _contextFactory = contextFactory;
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _eventBus = eventBus;
    }

    public async Task<IReadOnlyList<AgreementSummaryDto>> SearchAsync(string? searchTerm, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        var query = dbContext.Agreements.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.AgreementName, $"%{term}%") ||
                EF.Functions.Like(a.PayerName, $"%{term}%") ||
                EF.Functions.Like(a.CoverageType, $"%{term}%"));
        }

        var results = await query
            .OrderBy(a => a.AgreementName)
            .Select(a => new AgreementSummaryDto(
                a.AgreementId,
                a.AgreementName,
                a.PayerName,
                a.EffectiveFrom,
                a.EffectiveTo,
                a.Rates.Count,
                a.Status ?? AgreementStatus.Draft,
                a.RenewalDate))
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<AgreementDetailDto?> GetAsync(Guid agreementId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        var agreement = await dbContext.Agreements
            .Include(a => a.Rates)
            .Include(a => a.Approvals)
            .Include(a => a.ImpactedParties)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId, cancellationToken);

        if (agreement is null)
        {
            return null;
        }

        var impactedPartyDtos = new List<AgreementImpactedPartyDto>();

        if (agreement.ImpactedParties is { Count: > 0 })
        {
            var partyIds = agreement.ImpactedParties
                .Select(p => p.PartyId)
                .Distinct()
                .ToArray();

            var partyLookup = partyIds.Length == 0
                ? new Dictionary<Guid, (string Name, string Type)>()
                : await dbContext.Parties
                    .Where(p => partyIds.Contains(p.PartyId))
                    .Select(p => new { p.PartyId, p.DisplayName, p.PartyType })
                    .ToDictionaryAsync(p => p.PartyId, p => (Name: p.DisplayName, Type: p.PartyType), cancellationToken);

            impactedPartyDtos = agreement.ImpactedParties
                .OrderBy(p => string.IsNullOrWhiteSpace(p.Relationship) ? string.Empty : p.Relationship)
                .ThenBy(p => partyLookup.TryGetValue(p.PartyId, out var resolvedName) ? resolvedName.Name : string.Empty)
                .Select(p =>
                {
                    var displayInfo = partyLookup.TryGetValue(p.PartyId, out var resolved) ? resolved : (Name: string.Empty, Type: string.Empty);
                    var relationship = string.IsNullOrWhiteSpace(p.Relationship) ? string.Empty : p.Relationship.Trim();
                    return new AgreementImpactedPartyDto(
                        p.AgreementImpactedPartyId,
                        p.PartyId,
                        displayInfo.Name,
                        displayInfo.Type,
                        relationship);
                })
                .ToList();
        }

        return new AgreementDetailDto(
            agreement.AgreementId,
            agreement.AgreementName,
            agreement.PayerName,
            agreement.CoverageType,
            agreement.EffectiveFrom,
            agreement.EffectiveTo,
            agreement.Rates
                .OrderBy(r => r.ServiceCode)
                .Select(r => new AgreementRateDto(r.AgreementRateId, r.ServiceCode, r.BaseAmount, r.DiscountPercent, r.CopayPercent))
                .ToList(),
            agreement.Status ?? AgreementStatus.Draft,
            agreement.RequiresApproval,
            agreement.AutoRenew,
            agreement.RenewalDate,
            agreement.LastRenewedOn,
            agreement.Approvals
                .OrderByDescending(a => a.RequestedOnUtc)
                .Select(a => new AgreementApprovalDto(a.AgreementApprovalId, a.Approver, a.Decision, a.Comments, a.RequestedOnUtc, a.DecidedOnUtc))
                .ToList(),
            impactedPartyDtos);
    }

    public async Task<AgreementDetailDto> SaveAsync(AgreementDetailDto detail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(detail.AgreementName))
        {
            throw new ArgumentException("Agreement name is required.", nameof(detail));
        }
        if (string.IsNullOrWhiteSpace(detail.PayerName))
        {
            throw new ArgumentException("Payer name is required.", nameof(detail));
        }
        if (detail.Rates is null || detail.Rates.Count == 0)
        {
            throw new ArgumentException("At least one rate must be provided.", nameof(detail));
        }

        var status = AgreementWorkflowRules.Normalize(detail.Status);

        if (!detail.AgreementId.HasValue && !status.Equals(AgreementStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("New agreements must begin in Draft status.");
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        Agreement? entity = null;

        if (detail.AgreementId.HasValue)
        {
            entity = await dbContext.Agreements
                .Include(a => a.Rates)
                .Include(a => a.Approvals)
                .Include(a => a.ImpactedParties)
                .FirstOrDefaultAsync(a => a.AgreementId == detail.AgreementId.Value, cancellationToken);
        }

        var isUpdate = entity is not null;

        if (entity is null)
        {
            entity = new Agreement
            {
                AgreementId = detail.AgreementId ?? _idGenerator.Create(),
                Status = AgreementStatus.Draft,
                RequiresApproval = false,
                AutoRenew = detail.AutoRenew,
                RenewalDate = detail.RenewalDate,
                LastRenewedOn = detail.LastRenewedOn,
                Audit = new AuditTrail
                {
                    CreatedBy = "desktop",
                    SourceModule = "Core.Agreements"
                }
            };
            dbContext.Agreements.Add(entity);
        }
        else
        {
            var currentStatus = AgreementWorkflowRules.Normalize(entity.Status);
            AgreementWorkflowRules.EnsureTransition(currentStatus, status);
            entity.Audit ??= new AuditTrail();
            entity.Audit.ModifiedBy = "desktop";
            entity.Audit.ModifiedOnUtc = DateTime.UtcNow;
        }

        entity.AgreementName = detail.AgreementName.Trim();
        entity.PayerName = detail.PayerName.Trim();
        entity.CoverageType = detail.CoverageType.Trim();
        entity.EffectiveFrom = detail.EffectiveFrom;
        entity.EffectiveTo = detail.EffectiveTo;
        entity.AutoRenew = detail.AutoRenew;
        entity.RenewalDate = detail.RenewalDate;
        entity.LastRenewedOn = detail.LastRenewedOn;
        ApplyStatus(entity, status, detail.RequiresApproval);

        dbContext.AgreementRates.RemoveRange(entity.Rates);
        entity.Rates.Clear();
        dbContext.AgreementApprovals.RemoveRange(entity.Approvals);
        entity.Approvals.Clear();
        dbContext.AgreementImpactedParties.RemoveRange(entity.ImpactedParties);
        entity.ImpactedParties.Clear();

        foreach (var rateDto in detail.Rates)
        {
            if (string.IsNullOrWhiteSpace(rateDto.ServiceCode))
            {
                throw new ArgumentException("Service code is required on every rate.", nameof(detail));
            }

            var serviceCode = rateDto.ServiceCode.Trim().ToUpperInvariant();
            var rate = new AgreementRate
            {
                AgreementRateId = rateDto.AgreementRateId ?? _idGenerator.Create(),
                AgreementId = entity.AgreementId,
                ServiceCode = serviceCode,
                BaseAmount = rateDto.BaseAmount,
                DiscountPercent = rateDto.DiscountPercent,
                CopayPercent = rateDto.CoPayPercent,
                Audit = new AuditTrail
                {
                    CreatedBy = isUpdate ? "desktop" : "desktop",
                    SourceModule = "Core.Agreements"
                }
            };
            entity.Rates.Add(rate);
        }

        if (detail.Approvals is not null)
        {
            foreach (var approvalDto in detail.Approvals)
            {
                var approver = approvalDto.Approver?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(approver))
                {
                    continue;
                }

                var approval = new AgreementApproval
                {
                    AgreementApprovalId = approvalDto.AgreementApprovalId ?? _idGenerator.Create(),
                    AgreementId = entity.AgreementId,
                    Approver = approver,
                    Decision = string.IsNullOrWhiteSpace(approvalDto.Decision) ? ApprovalDecision.Pending : approvalDto.Decision.Trim(),
                    Comments = string.IsNullOrWhiteSpace(approvalDto.Comments) ? null : approvalDto.Comments.Trim(),
                    RequestedOnUtc = approvalDto.RequestedOnUtc == default ? DateTime.UtcNow : approvalDto.RequestedOnUtc,
                    DecidedOnUtc = approvalDto.DecidedOnUtc,
                    Audit = new AuditTrail
                    {
                        CreatedBy = isUpdate ? "desktop" : "desktop",
                        SourceModule = "Core.Agreements"
                    }
                };

                entity.Approvals.Add(approval);
            }
        }

        if (detail.ImpactedParties is not null)
        {
            foreach (var impactedDto in detail.ImpactedParties)
            {
                if (impactedDto is null || impactedDto.PartyId == Guid.Empty)
                {
                    continue;
                }

                var impacted = new AgreementImpactedParty
                {
                    AgreementImpactedPartyId = impactedDto.AgreementImpactedPartyId ?? _idGenerator.Create(),
                    AgreementId = entity.AgreementId,
                    PartyId = impactedDto.PartyId,
                    Relationship = string.IsNullOrWhiteSpace(impactedDto.Relationship) ? string.Empty : impactedDto.Relationship.Trim(),
                    Audit = new AuditTrail
                    {
                        CreatedBy = "desktop",
                        SourceModule = "Core.Agreements"
                    }
                };

                entity.ImpactedParties.Add(impacted);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(entity.AgreementId, cancellationToken) ??
            new AgreementDetailDto(entity.AgreementId, entity.AgreementName, entity.PayerName, entity.CoverageType, entity.EffectiveFrom, entity.EffectiveTo, Array.Empty<AgreementRateDto>(), entity.Status, entity.RequiresApproval, entity.AutoRenew, entity.RenewalDate, entity.LastRenewedOn, Array.Empty<AgreementApprovalDto>(), Array.Empty<AgreementImpactedPartyDto>());
    }

    public async Task<AgreementDetailDto> RequestApprovalAsync(Guid agreementId, string approver, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approver))
        {
            throw new ArgumentException("Approver name is required.", nameof(approver));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var entity = await dbContext.Agreements
            .Include(a => a.Approvals)
            .Include(a => a.Rates)
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Agreement was not found.");
        }

        var normalizedApprover = approver.Trim();
        var currentStatus = AgreementWorkflowRules.Normalize(entity.Status);
        AgreementWorkflowRules.EnsureTransition(currentStatus, AgreementStatus.PendingApproval);

        if (entity.Approvals?.Any(a => a.Decision == ApprovalDecision.Pending && a.Approver.Equals(normalizedApprover, StringComparison.OrdinalIgnoreCase)) == true)
        {
            throw new InvalidOperationException($"An approval request is already pending for {normalizedApprover}.");
        }

        var approval = new AgreementApproval
        {
            AgreementApprovalId = _idGenerator.Create(),
            AgreementId = agreementId,
            Approver = normalizedApprover,
            Decision = ApprovalDecision.Pending,
            RequestedOnUtc = DateTime.UtcNow,
            Audit = new AuditTrail
            {
                CreatedBy = "desktop",
                SourceModule = "Core.Agreements"
            }
        };

        entity.Approvals ??= new List<AgreementApproval>();
        entity.Approvals.Add(approval);
        ApplyStatus(entity, AgreementStatus.PendingApproval, true);

        entity.Audit ??= new AuditTrail();
        entity.Audit.ModifiedBy = "desktop";
        entity.Audit.ModifiedOnUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (_eventBus is not null)
        {
            await _eventBus.PublishAsync(new ApprovalRequiredEvent(agreementId, normalizedApprover), cancellationToken);
        }

        return await GetAsync(agreementId, cancellationToken) ??
               throw new InvalidOperationException("Agreement could not be reloaded after requesting approval.");
    }

    public async Task<AgreementDetailDto> CompleteApprovalAsync(Guid agreementId, Guid approvalId, string decision, string? comments, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            throw new ArgumentException("Decision is required.", nameof(decision));
        }

        var normalizedDecision = decision.Trim();
        if (normalizedDecision != ApprovalDecision.Approved && normalizedDecision != ApprovalDecision.Rejected)
        {
            throw new ArgumentException("Decision must be Approved or Rejected.", nameof(decision));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var entity = await dbContext.Agreements
            .Include(a => a.Approvals)
            .Include(a => a.Rates)
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Agreement was not found.");
        }

        var approval = entity.Approvals?.FirstOrDefault(a => a.AgreementApprovalId == approvalId);
        if (approval is null)
        {
            throw new InvalidOperationException("Approval record not found.");
        }

        if (approval.Decision != ApprovalDecision.Pending)
        {
            throw new InvalidOperationException("Approval decision has already been recorded.");
        }

        approval.Decision = normalizedDecision;
        approval.Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();
        approval.DecidedOnUtc = DateTime.UtcNow;
        approval.Audit ??= new AuditTrail();
        approval.Audit.ModifiedBy = "desktop";
        approval.Audit.ModifiedOnUtc = DateTime.UtcNow;

        var targetStatus = normalizedDecision == ApprovalDecision.Approved ? AgreementStatus.Approved : AgreementStatus.Draft;
        var currentStatus = AgreementWorkflowRules.Normalize(entity.Status);
        AgreementWorkflowRules.EnsureTransition(currentStatus, targetStatus);
        ApplyStatus(entity, targetStatus, false);
        entity.Audit ??= new AuditTrail();
        entity.Audit.ModifiedBy = "desktop";
        entity.Audit.ModifiedOnUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(agreementId, cancellationToken) ??
               throw new InvalidOperationException("Agreement could not be reloaded after completing approval.");
    }

    public async Task<AgreementDetailDto> ScheduleRenewalAsync(Guid agreementId, DateOnly renewalDate, bool autoRenew, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var entity = await dbContext.Agreements
            .Include(a => a.Rates)
            .Include(a => a.Approvals)
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Agreement was not found.");
        }

        entity.RenewalDate = renewalDate;
        entity.AutoRenew = autoRenew;
        var currentStatus = AgreementWorkflowRules.Normalize(entity.Status);
        if (currentStatus != AgreementStatus.Approved && currentStatus != AgreementStatus.Active)
        {
            throw new InvalidOperationException("Renewal scheduling requires an approved or active agreement.");
        }

        if (currentStatus == AgreementStatus.Approved)
        {
            AgreementWorkflowRules.EnsureTransition(currentStatus, AgreementStatus.Active);
        }

        ApplyStatus(entity, AgreementStatus.Active, false);

        entity.Audit ??= new AuditTrail();
        entity.Audit.ModifiedBy = "desktop";
        entity.Audit.ModifiedOnUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(agreementId, cancellationToken) ??
               throw new InvalidOperationException("Agreement could not be reloaded after scheduling renewal.");
    }

    public async Task<AgreementDetailDto> MarkRenewedAsync(Guid agreementId, DateOnly renewedOn, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var entity = await dbContext.Agreements
            .Include(a => a.Rates)
            .Include(a => a.Approvals)
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Agreement was not found.");
        }

        var currentStatus = AgreementWorkflowRules.Normalize(entity.Status);
        if (currentStatus != AgreementStatus.Active && currentStatus != AgreementStatus.Approved)
        {
            throw new InvalidOperationException("Only approved or active agreements can be renewed.");
        }

        if (currentStatus == AgreementStatus.Approved)
        {
            AgreementWorkflowRules.EnsureTransition(currentStatus, AgreementStatus.Active);
        }

        entity.LastRenewedOn = renewedOn;
        if (entity.AutoRenew)
        {
            entity.RenewalDate = renewedOn.AddYears(1);
        }
        else if (entity.RenewalDate.HasValue && entity.RenewalDate <= renewedOn)
        {
            entity.RenewalDate = null;
        }

        ApplyStatus(entity, AgreementStatus.Active, false);
        entity.Audit ??= new AuditTrail();
        entity.Audit.ModifiedBy = "desktop";
        entity.Audit.ModifiedOnUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(agreementId, cancellationToken) ??
               throw new InvalidOperationException("Agreement could not be reloaded after marking renewed.");
    }

    public async Task<PricingResultDto> CalculatePriceAsync(Guid agreementId, string serviceCode, decimal listPrice, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var agreement = await dbContext.Agreements
            .Include(a => a.Rates)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId, cancellationToken);

        if (agreement is null)
        {
            throw new InvalidOperationException("Agreement was not found.");
        }

        var normalizedCode = serviceCode.Trim().ToUpperInvariant();
        var rate = agreement.Rates.FirstOrDefault(r => r.ServiceCode == normalizedCode);

        var context = new PricingCalculationContext(
            AgreementWorkflowRules.Normalize(agreement.Status),
            agreement.EffectiveFrom,
            agreement.EffectiveTo,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            listPrice,
            quantity,
            rate?.BaseAmount,
            rate?.DiscountPercent ?? 0m,
            rate?.CopayPercent,
            agreement.RenewalDate,
            agreement.LastRenewedOn);

        return PricingCalculator.Calculate(context);
    }

    public async Task<AgreementValidationResultDto> ValidateWorkflowAsync(Guid agreementId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var entity = await dbContext.Agreements
            .Include(a => a.Rates)
            .Include(a => a.Approvals)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Agreement was not found.");
        }

        var issues = new List<string>();
        var normalizedStatus = AgreementWorkflowRules.Normalize(entity.Status);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        if (!IsKnownStatus(normalizedStatus))
        {
            issues.Add($"Unknown workflow status '{entity.Status}'.");
        }

        if (entity.Rates is null || entity.Rates.Count == 0)
        {
            issues.Add("Agreement has no rates defined.");
        }

        var pendingApprovals = entity.Approvals?.Where(a => a.Decision == ApprovalDecision.Pending).ToList() ?? new List<AgreementApproval>();

        if (!entity.RequiresApproval && pendingApprovals.Count > 0)
        {
            issues.Add("Pending approvals exist while RequiresApproval is false.");
        }

        if (normalizedStatus == AgreementStatus.PendingApproval && pendingApprovals.Count == 0)
        {
            issues.Add("Status is Pending Approval but no pending approvals are recorded.");
        }

        if (normalizedStatus == AgreementStatus.Approved && (entity.Approvals?.All(a => a.Decision != ApprovalDecision.Approved) ?? true))
        {
            issues.Add("Status is Approved but no approval decision is marked Approved.");
        }

        if (normalizedStatus == AgreementStatus.Active)
        {
            if (entity.EffectiveTo.HasValue && entity.EffectiveTo.Value < today)
            {
                issues.Add("Agreement is Active but the effective period has ended.");
            }

            if (entity.RenewalDate.HasValue && entity.RenewalDate.Value < today && !entity.AutoRenew)
            {
                issues.Add("Agreement is Active with a lapsed renewal date and auto-renew disabled.");
            }
        }

        if (pendingApprovals.Any(a => a.RequestedOnUtc <= DateTime.UtcNow - ApprovalEscalationThreshold && a.EscalatedOnUtc is null))
        {
            issues.Add("Pending approvals past escalation threshold have not been escalated.");
        }

        return new AgreementValidationResultDto(issues.Count == 0, issues);
    }

    public async Task<int> EscalatePendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var cutoff = DateTime.UtcNow - ApprovalEscalationThreshold;
        var staleApprovals = await dbContext.AgreementApprovals
            .Where(a => a.Decision == ApprovalDecision.Pending && a.RequestedOnUtc <= cutoff && a.EscalatedOnUtc == null)
            .ToListAsync(cancellationToken);

        if (staleApprovals.Count == 0)
        {
            return 0;
        }

        var utcNow = DateTime.UtcNow;

        foreach (var approval in staleApprovals)
        {
            approval.EscalatedOnUtc = utcNow;
            approval.Audit ??= new AuditTrail();
            approval.Audit.ModifiedBy = "desktop";
            approval.Audit.ModifiedOnUtc = utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (_eventBus is not null)
        {
            foreach (var approval in staleApprovals)
            {
                await _eventBus.PublishAsync(new ApprovalEscalatedEvent(approval.AgreementId, approval.Approver, approval.RequestedOnUtc), cancellationToken);
            }
        }

        return staleApprovals.Count;
    }

    private static void ApplyStatus(Agreement agreement, string status, bool requiresApproval)
    {
        if (agreement is null)
        {
            throw new ArgumentNullException(nameof(agreement));
        }

        var normalized = AgreementWorkflowRules.Normalize(status);
        agreement.Status = normalized;
        agreement.RequiresApproval = requiresApproval;
    }

    private static bool IsKnownStatus(string status)
    {
        return status.Equals(AgreementStatus.Draft, StringComparison.OrdinalIgnoreCase)
               || status.Equals(AgreementStatus.PendingApproval, StringComparison.OrdinalIgnoreCase)
               || status.Equals(AgreementStatus.Approved, StringComparison.OrdinalIgnoreCase)
               || status.Equals(AgreementStatus.Active, StringComparison.OrdinalIgnoreCase)
               || status.Equals(AgreementStatus.Expired, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AgreementDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var expiryHorizon = today.AddDays(30);

        var agreementsQuery = dbContext.Agreements.AsNoTracking();

        var totalAgreements = await agreementsQuery.CountAsync(cancellationToken);
        var activeAgreements = await agreementsQuery.CountAsync(
            a => a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo >= today),
            cancellationToken);
        var expiringSoon = await agreementsQuery.CountAsync(
            a => a.EffectiveTo != null && a.EffectiveTo >= today && a.EffectiveTo <= expiryHorizon,
            cancellationToken);
        var renewalsDueSoon = await agreementsQuery.CountAsync(
            a => a.RenewalDate != null && a.RenewalDate >= today && a.RenewalDate <= expiryHorizon,
            cancellationToken);

        var ratesQuery = dbContext.AgreementRates.AsNoTracking();
        var totalRates = await ratesQuery.CountAsync(cancellationToken);
        var averageDiscount = await ratesQuery.Select(r => (decimal?)r.DiscountPercent).AverageAsync(cancellationToken) ?? 0m;
        var averageCopay = await ratesQuery.Select(r => (decimal?)r.CopayPercent).AverageAsync(cancellationToken) ?? 0m;

        var pendingApprovals = await dbContext.AgreementApprovals
            .AsNoTracking()
            .CountAsync(a => a.Decision == ApprovalDecision.Pending, cancellationToken);

        return new AgreementDashboardDto(
            totalAgreements,
            activeAgreements,
            expiringSoon,
            pendingApprovals,
            renewalsDueSoon,
            totalRates,
            decimal.Round(averageDiscount, 1),
            decimal.Round(averageCopay, 1));
    }

    public async Task<AgreementApprovalTriageSnapshotDto> GetApprovalTriageAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var trackedActionTypes = new[] { ActionLogType.Reminder, ActionLogType.FastTrack };
        var trackingWindowUtc = nowUtc.AddDays(-30);

        var actionLogs = await dbContext.ActionLogs
            .AsNoTracking()
            .Where(log => log.ActionedOnUtc >= trackingWindowUtc &&
                          (log.ActionType == ActionLogType.Reminder || log.ActionType == ActionLogType.FastTrack))
            .Select(log => new { log.ActionType, log.ActionedOnUtc })
            .ToListAsync(cancellationToken);

        var pendingApprovals = await dbContext.AgreementApprovals
            .AsNoTracking()
            .Where(a => a.Decision == ApprovalDecision.Pending)
            .Select(a => new
            {
                a.AgreementId,
                a.AgreementApprovalId,
                a.Approver,
                a.RequestedOnUtc,
                a.EscalatedOnUtc
            })
            .ToListAsync(cancellationToken);

        List<AgreementApprovalTriageDto> orderedTriage;
        List<AgreementApprovalHeatMapBucketDto> heatMap;
        List<ApprovalActionInsightDto> actionInsights;

        if (pendingApprovals.Count == 0)
        {
            orderedTriage = new List<AgreementApprovalTriageDto>();
            heatMap = new List<AgreementApprovalHeatMapBucketDto>();
        }
        else
        {
            var agreementIds = pendingApprovals
                .Select(a => a.AgreementId)
                .Distinct()
                .ToList();

            var agreements = await dbContext.Agreements
                .AsNoTracking()
                .Where(a => agreementIds.Contains(a.AgreementId))
                .Select(a => new
                {
                    a.AgreementId,
                    a.AgreementName,
                    a.PayerName
                })
                .ToListAsync(cancellationToken);

            var rateSummaries = await dbContext.AgreementRates
                .AsNoTracking()
                .Where(r => agreementIds.Contains(r.AgreementId))
                .GroupBy(r => r.AgreementId)
                .Select(g => new
                {
                    AgreementId = g.Key,
                    RateCount = g.Count(),
                    TotalBase = g.Sum(r => (double)r.BaseAmount)
                })
                .ToListAsync(cancellationToken);

            var impactedRecords = await dbContext.AgreementImpactedParties
                .AsNoTracking()
                .Where(ip => agreementIds.Contains(ip.AgreementId))
                .Select(ip => new
                {
                    ip.AgreementId,
                    ip.PartyId
                })
                .ToListAsync(cancellationToken);

            var impactedPartyIds = impactedRecords
                .Select(r => r.PartyId)
                .Distinct()
                .ToList();

            var impactedPartyNames = impactedPartyIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await dbContext.Parties
                    .AsNoTracking()
                    .Where(p => impactedPartyIds.Contains(p.PartyId))
                    .Select(p => new { p.PartyId, p.DisplayName })
                    .ToDictionaryAsync(p => p.PartyId, p => p.DisplayName, cancellationToken);

            var impactedLookup = impactedRecords
                .GroupBy(r => r.AgreementId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var names = new List<string>();
                        foreach (var record in g)
                        {
                            if (impactedPartyNames.TryGetValue(record.PartyId, out var resolvedName) && !string.IsNullOrWhiteSpace(resolvedName))
                            {
                                names.Add(resolvedName);
                            }
                        }

                        return names
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    });

            var agreementLookup = agreements.ToDictionary(a => a.AgreementId);
            var rateLookup = rateSummaries.ToDictionary(r => r.AgreementId);

            var triage = new List<AgreementApprovalTriageDto>(pendingApprovals.Count);
            foreach (var pending in pendingApprovals)
            {
                if (!agreementLookup.TryGetValue(pending.AgreementId, out var agreement))
                {
                    continue;
                }

                var rateSummary = rateLookup.TryGetValue(pending.AgreementId, out var value)
                    ? value
                    : null;

                var potentialValue = rateSummary is null ? 0m : Convert.ToDecimal(rateSummary.TotalBase);

                var partyNames = impactedLookup.TryGetValue(pending.AgreementId, out var impactedNames)
                    ? impactedNames
                    : new List<string>();

                var impactedCount = partyNames.Count > 0
                    ? partyNames.Count
                    : rateSummary?.RateCount ?? 0;

                triage.Add(new AgreementApprovalTriageDto(
                    pending.AgreementId,
                    pending.AgreementApprovalId,
                    agreement.AgreementName,
                    agreement.PayerName,
                    pending.Approver,
                    pending.RequestedOnUtc,
                    pending.EscalatedOnUtc,
                    impactedCount,
                    potentialValue,
                    partyNames));
            }

            orderedTriage = triage
                .OrderByDescending(t => t.PotentialValue)
                .ThenByDescending(t => t.RequestedOnUtc)
                .ToList();

            var bucketDefinitions = new List<(string Label, TimeSpan Min, TimeSpan? Max)>
            {
                ("<8h", TimeSpan.Zero, TimeSpan.FromHours(8)),
                ("8-24h", TimeSpan.FromHours(8), TimeSpan.FromHours(24)),
                ("1-3d", TimeSpan.FromHours(24), TimeSpan.FromDays(3)),
                ("3-7d", TimeSpan.FromDays(3), TimeSpan.FromDays(7)),
                (">7d", TimeSpan.FromDays(7), null)
            };

            heatMap = new List<AgreementApprovalHeatMapBucketDto>(bucketDefinitions.Count);
            foreach (var bucket in bucketDefinitions)
            {
                var bucketItems = orderedTriage
                    .Where(item =>
                    {
                        var age = nowUtc - item.RequestedOnUtc;
                        if (age < TimeSpan.Zero)
                        {
                            age = TimeSpan.Zero;
                        }

                        if (age < bucket.Min)
                        {
                            return false;
                        }

                        if (bucket.Max is null)
                        {
                            return true;
                        }

                        return age < bucket.Max.Value;
                    })
                    .ToList();

                var bucketValue = bucketItems.Sum(item => item.PotentialValue);
                heatMap.Add(new AgreementApprovalHeatMapBucketDto(bucket.Label, bucketItems.Count, bucketValue));
            }
        }

        var sevenDayBoundary = nowUtc.AddDays(-7);
        actionInsights = trackedActionTypes
            .Select(actionType =>
            {
                var matches = actionLogs
                    .Where(log => string.Equals(log.ActionType, actionType, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var count30 = matches.Count;
                var count7 = matches.Count(log => log.ActionedOnUtc >= sevenDayBoundary);
                var mostRecent = matches.Count == 0 ? (DateTime?)null : matches.Max(log => log.ActionedOnUtc);

                return new ApprovalActionInsightDto(actionType, count7, count30, mostRecent);
            })
            .ToList();

        return new AgreementApprovalTriageSnapshotDto(orderedTriage, heatMap, actionInsights);
    }

    public async Task SendApprovalReminderAsync(Guid agreementId, Guid approvalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var approval = await dbContext.AgreementApprovals
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId && a.AgreementApprovalId == approvalId, cancellationToken);

        if (approval is null)
        {
            throw new InvalidOperationException("Approval record not found.");
        }

        if (approval.Decision != ApprovalDecision.Pending)
        {
            throw new InvalidOperationException("Only pending approvals can be reminded.");
        }

        approval.Audit ??= new AuditTrail();
        approval.Audit.ModifiedBy = "desktop";
        approval.Audit.ModifiedOnUtc = DateTime.UtcNow;

        dbContext.ActionLogs.Add(new ActionLog
        {
            ActionLogId = _idGenerator.Create(),
            AgreementId = agreementId,
            AgreementApprovalId = approvalId,
            ActionType = ActionLogType.Reminder,
            PerformedBy = "desktop",
            ActionedOnUtc = DateTime.UtcNow,
            Notes = FormattableString.Invariant($"Reminder dispatched for {approval.Approver}"),
            Audit = new AuditTrail
            {
                CreatedBy = "desktop",
                SourceModule = "Core.Agreements"
            }
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        if (_eventBus is not null)
        {
            await _eventBus.PublishAsync(new ApprovalReminderEvent(
                agreementId,
                approvalId,
                approval.Approver,
                approval.RequestedOnUtc,
                DateTime.UtcNow),
                cancellationToken);
        }
    }

    public async Task FastTrackApprovalAsync(Guid agreementId, Guid approvalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var approval = await dbContext.AgreementApprovals
            .FirstOrDefaultAsync(a => a.AgreementId == agreementId && a.AgreementApprovalId == approvalId, cancellationToken);

        if (approval is null)
        {
            throw new InvalidOperationException("Approval record not found.");
        }

        if (approval.Decision != ApprovalDecision.Pending)
        {
            throw new InvalidOperationException("Only pending approvals can be fast-tracked.");
        }

        var utcNow = DateTime.UtcNow;
        approval.EscalatedOnUtc = utcNow;
        approval.Audit ??= new AuditTrail();
        approval.Audit.ModifiedBy = "desktop";
        approval.Audit.ModifiedOnUtc = utcNow;

        dbContext.ActionLogs.Add(new ActionLog
        {
            ActionLogId = _idGenerator.Create(),
            AgreementId = agreementId,
            AgreementApprovalId = approvalId,
            ActionType = ActionLogType.FastTrack,
            PerformedBy = "desktop",
            ActionedOnUtc = utcNow,
            Notes = FormattableString.Invariant($"Fast-tracked approval for {approval.Approver}"),
            Audit = new AuditTrail
            {
                CreatedBy = "desktop",
                SourceModule = "Core.Agreements"
            }
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        if (_eventBus is not null)
        {
            await _eventBus.PublishAsync(new ApprovalEscalatedEvent(
                approval.AgreementId,
                approval.Approver,
                approval.RequestedOnUtc),
                cancellationToken);
        }
    }

}
