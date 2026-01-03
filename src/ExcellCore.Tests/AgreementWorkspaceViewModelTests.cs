using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Services;
using ExcellCore.Module.Abstractions;
using ExcellCore.Module.Core.Agreements.ViewModels;
using Xunit;

namespace ExcellCore.Tests;

public sealed class AgreementWorkspaceViewModelTests
{
    [Fact]
    public void SaveCommand_Disabled_WhenImpactedPartyMissingResolvedIdentity()
    {
        var agreementService = new StubAgreementService();
        var partyService = new StubPartyService();
        var localizationService = new StubLocalizationService();
        var localizationContext = new StubLocalizationContext();
        var viewModel = new AgreementWorkspaceViewModel(agreementService, partyService, localizationService, localizationContext);

        viewModel.Form.AgreementName = "Test Agreement";
        viewModel.Form.PayerName = "Test Payer";
        viewModel.ImpactedParties[0].PartyName = "Typed Only";

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void SaveCommand_AllowsExecution_AfterSelectingKnownIdentity()
    {
        var agreementService = new StubAgreementService();
        var partyService = new StubPartyService();
        var localizationService = new StubLocalizationService();
        var localizationContext = new StubLocalizationContext();
        var viewModel = new AgreementWorkspaceViewModel(agreementService, partyService, localizationService, localizationContext);

        viewModel.Form.AgreementName = "Enterprise Plan";
        viewModel.Form.PayerName = "Contoso";
        viewModel.Rates[0].ServiceCode = "SRV-100";

        var resolvedPartyId = Guid.NewGuid();
        var impacted = viewModel.ImpactedParties[0];
        impacted.SelectedLookup = new PartyLookupResultModel(resolvedPartyId, "Ada Lovelace", "Identity", "NID-42", "Identity · National ID");
        impacted.Relationship = "Subscriber";

        agreementService.SaveResult = new AgreementDetailDto(
            resolvedPartyId,
            viewModel.Form.AgreementName,
            viewModel.Form.PayerName,
            viewModel.Form.CoverageType,
            DateOnly.FromDateTime(DateTime.Today),
            null,
            new[] { new AgreementRateDto(Guid.NewGuid(), viewModel.Rates[0].ServiceCode, 0m, 0m, 0m) },
            AgreementStatus.Active,
            false,
            false,
            null,
            null,
            Array.Empty<AgreementApprovalDto>(),
            new[] { new AgreementImpactedPartyDto(Guid.NewGuid(), resolvedPartyId, "Ada Lovelace", "Identity", "Subscriber") });

        Assert.True(viewModel.SaveCommand.CanExecute(null));

        viewModel.SaveCommand.Execute(null);

        Assert.True(agreementService.SaveCalled);
        Assert.Equal(resolvedPartyId, agreementService.LastSavedDetail?.ImpactedParties.Single().PartyId);
        Assert.Equal("Ada Lovelace", impacted.PartyName);
        Assert.Equal("Identity", impacted.PartyType);
    }

    private sealed class StubAgreementService : IAgreementService
    {
        public bool SaveCalled { get; private set; }
        public AgreementDetailDto? LastSavedDetail { get; private set; }
        public AgreementDetailDto? SaveResult { get; set; }

        public Task<AgreementDetailDto?> GetAsync(Guid agreementId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AgreementDetailDto?>(null);
        }

        public Task<AgreementDetailDto> SaveAsync(AgreementDetailDto detail, CancellationToken cancellationToken = default)
        {
            SaveCalled = true;
            LastSavedDetail = detail;
            return Task.FromResult(SaveResult ?? detail);
        }

        public Task<IReadOnlyList<AgreementSummaryDto>> SearchAsync(string? searchTerm, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AgreementSummaryDto> summaries = Array.Empty<AgreementSummaryDto>();
            return Task.FromResult(summaries);
        }

        public Task<PricingResultDto> CalculatePriceAsync(Guid agreementId, string serviceCode, decimal listPrice, int quantity, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgreementDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = new AgreementDashboardDto(0, 0, 0, 0, 0, 0, 0m, 0m);
            return Task.FromResult(snapshot);
        }

        public Task<AgreementApprovalTriageSnapshotDto> GetApprovalTriageAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = new AgreementApprovalTriageSnapshotDto(Array.Empty<AgreementApprovalTriageDto>(), Array.Empty<AgreementApprovalHeatMapBucketDto>(), Array.Empty<ApprovalActionInsightDto>());
            return Task.FromResult(snapshot);
        }

        public Task<AgreementDetailDto> RequestApprovalAsync(Guid agreementId, string approver, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgreementDetailDto> CompleteApprovalAsync(Guid agreementId, Guid approvalId, string decision, string? comments, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgreementDetailDto> ScheduleRenewalAsync(Guid agreementId, DateOnly renewalDate, bool autoRenew, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgreementDetailDto> MarkRenewedAsync(Guid agreementId, DateOnly renewedOn, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgreementValidationResultDto> ValidateWorkflowAsync(Guid agreementId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<int> EscalatePendingApprovalsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SendApprovalReminderAsync(Guid agreementId, Guid approvalId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task FastTrackApprovalAsync(Guid agreementId, Guid approvalId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubPartyService : IPartyService
    {
        public Task<PartyDetailDto?> GetAsync(Guid partyId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PartyDetailDto?>(null);
        }

        public Task<IReadOnlyList<PartyLookupResultDto>> LookupAsync(string? searchTerm, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PartyLookupResultDto> results = Array.Empty<PartyLookupResultDto>();
            return Task.FromResult(results);
        }

        public Task<PartyDetailDto> SaveAsync(PartyDetailDto detail, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PartySummaryDto>> SearchAsync(string? searchTerm, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PartySummaryDto> results = Array.Empty<PartySummaryDto>();
            return Task.FromResult(results);
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ImpactedPartyColumn"] = "Impacted Party",
            ["ImpactedPartyTypeColumn"] = "Type",
            ["ImpactedPartyRelationshipColumn"] = "Relationship",
            ["ValidationUnknownIdentity"] = "Select a known identity before saving impacted parties.",
            ["ValidationIncompleteAgreement"] = "Fill in required fields and add at least one rate before saving."
        };

        public string GetString(string key, IEnumerable<string> contexts)
        {
            return Map.TryGetValue(key, out var value) ? value : key;
        }

        public IReadOnlyDictionary<string, string> GetStrings(IEnumerable<string> keys, IEnumerable<string> contexts)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                result[key] = GetString(key, contexts);
            }

            return result;
        }
    }

    private sealed class StubLocalizationContext : ILocalizationContext
    {
        public IReadOnlyList<string> Contexts { get; private set; } = new List<string> { "Core.Agreements", "Default" };

        public event EventHandler? ContextsChanged;

        public void SetContexts(IEnumerable<string> contexts)
        {
            Contexts = contexts.ToList();
            ContextsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
