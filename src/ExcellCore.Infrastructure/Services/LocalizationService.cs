using System;
using System.Collections.Generic;
using System.Linq;
using ExcellCore.Module.Abstractions;

namespace ExcellCore.Infrastructure.Services;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Core.Agreements"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ImpactedPartyColumn"] = "Impacted Party",
                ["ImpactedPartyTypeColumn"] = "Type",
                ["ImpactedPartyRelationshipColumn"] = "Relationship",
                ["ValidationUnknownIdentity"] = "Select a known identity before saving impacted parties.",
                ["ValidationIncompleteAgreement"] = "Fill in required fields and add at least one rate before saving.",
                ["AgreementsTitle"] = "Agreement Engine",
                ["AgreementsModuleBadge"] = "(Core Module)",
                ["SummaryActiveAgreements"] = "Active Agreements",
                ["SummaryPendingApprovals"] = "Pending Approvals",
                ["SummaryRenewals"] = "Renewals (30d)",
                ["SummaryAvgDiscount"] = "Avg Discount",
                ["SummaryPricingRuns"] = "Pricing Runs",
                ["SlaHeatMapTitle"] = "SLA Heat Map",
                ["SlaHeatMapEmpty"] = "No pending approvals to score.",
                ["ReminderFastTrackTitle"] = "Reminder & Fast-Track Activity",
                ["ReminderFastTrackEmpty"] = "No recent reminder or fast-track activity.",
                ["SearchButton"] = "Search",
                ["NewButton"] = "New",
                ["SaveButton"] = "Save",
                ["ApprovalsHeader"] = "Approvals",
                ["ApproverHeader"] = "Approver",
                ["DecisionHeader"] = "Decision",
                ["RequestedHeader"] = "Requested",
                ["DecidedHeader"] = "Decided",
                ["ApproverLabel"] = "Approver",
                ["RequestApprovalButton"] = "Request Approval",
                ["ReviewerCommentsLabel"] = "Reviewer Comments",
                ["ApproveButton"] = "Approve",
                ["RejectButton"] = "Reject",
                ["AddImpactButton"] = "Add Impact",
                ["RemoveButton"] = "Remove",
                ["AddRateButton"] = "Add Rate",
                ["PricingHeader"] = "Pricing",
                ["ServiceCodeLabel"] = "Service Code",
                ["QuantityLabel"] = "Quantity",
                ["ListPriceLabel"] = "List Price",
                ["CalculateButton"] = "Calculate",
                ["NetLabel"] = "Net",
                ["DiscountLabel"] = "Discount",
                ["CopayLabel"] = "Co-pay",
                ["PricingHistoryHeader"] = "Pricing History",
                ["PricingHistoryWhen"] = "When",
                ["PricingHistoryService"] = "Service",
                ["PricingHistoryQty"] = "Qty",
                ["PricingHistoryList"] = "List",
                ["PricingHistoryNet"] = "Net",
                ["PricingHistoryDiscount"] = "Discount",
                ["PricingHistoryCopay"] = "Co-pay",
                ["RenewalDateLabel"] = "Renewal Date",
                ["AutoRenewLabel"] = "Auto Renew",
                ["ScheduleRenewalButton"] = "Schedule",
                ["MarkRenewedButton"] = "Mark Renewed",
                ["StatusLabel"] = "Status",
                ["LastRenewedLabel"] = "Last Renewed",
                ["RequiresApprovalLabel"] = "Requires Approval",
                ["AgreementColumn"] = "Agreement",
                ["PayerColumn"] = "Payer",
                ["EffectiveColumn"] = "Effective",
                ["ExpiresColumn"] = "Expires",
                ["StatusColumn"] = "Status",
                ["RatesColumn"] = "Rates",
                ["AgreementDetailsHeader"] = "Agreement Details",
                ["AgreementNameLabel"] = "Agreement Name",
                ["PayerLabel"] = "Payer",
                ["CoverageTypeLabel"] = "Coverage Type",
                ["EffectiveFromLabel"] = "Effective From",
                ["EffectiveToLabel"] = "Effective To",
                ["RemindApproverButton"] = "Remind Approver",
                ["FastTrackButton"] = "Fast-Track"
            },
            ["Core.Identity.Clinical"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Metadata.BloodType"] = "Blood Type",
                ["Metadata.Allergies"] = "Allergies"
            },
            ["Core.Identity.Retail"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Metadata.LoyaltyTier"] = "Loyalty Tier",
                ["Metadata.PreferredChannel"] = "Preferred Channel"
            },
            ["Core.Identity.Corporate"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Metadata.AccountManager"] = "Account Manager",
                ["Metadata.CostCenter"] = "Cost Center"
            },
            ["Extensions.Reporting.Telemetry"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Title"] = "System Telemetry",
                ["Subtitle"] = "Slow-query health and performance trends.",
                ["TrendLabel"] = "Telemetry Trend",
                ["SeverityBreakdownLabel"] = "Severity Breakdown",
                ["RefreshButton"] = "Refresh",
                ["LoadingStatus"] = "Loading telemetry health...",
                ["FailedStatus"] = "Failed to load telemetry: {0}",
                ["UpdatedStatus"] = "Telemetry updated {0}"
            },
            ["Default"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DisplayName"] = "Display Name"
            }
        };

    public string GetString(string key, IEnumerable<string> contexts)
    {
        var resolved = Resolve(key, contexts);
        if (resolved is not null)
        {
            return resolved;
        }

        return key;
    }

    public IReadOnlyDictionary<string, string> GetStrings(IEnumerable<string> keys, IEnumerable<string> contexts)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            dict[key] = GetString(key, contexts);
        }

        return dict;
    }

    private static string? Resolve(string key, IEnumerable<string> contexts)
    {
        foreach (var context in contexts)
        {
            if (Resources.TryGetValue(context, out var map) && map.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        if (Resources.TryGetValue("Default", out var defaults) && defaults.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return null;
    }
}
