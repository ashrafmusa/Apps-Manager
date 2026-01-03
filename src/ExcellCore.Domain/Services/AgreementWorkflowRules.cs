using System;
using System.Collections.Generic;
using System.Linq;
using ExcellCore.Domain.Entities;

namespace ExcellCore.Domain.Services;

public static class AgreementWorkflowRules
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTransitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [AgreementStatus.Draft] = new[] { AgreementStatus.Draft, AgreementStatus.PendingApproval },
        [AgreementStatus.PendingApproval] = new[] { AgreementStatus.PendingApproval, AgreementStatus.Approved, AgreementStatus.Draft },
        [AgreementStatus.Approved] = new[] { AgreementStatus.Approved, AgreementStatus.Active },
        [AgreementStatus.Active] = new[] { AgreementStatus.Active, AgreementStatus.Expired },
        [AgreementStatus.Expired] = new[] { AgreementStatus.Expired }
    };

    public static void EnsureTransition(string currentStatus, string nextStatus)
    {
        currentStatus = Normalize(currentStatus);
        nextStatus = Normalize(nextStatus);

        if (currentStatus.Equals(nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!AllowedTransitions.TryGetValue(currentStatus, out var allowed) || !allowed.Any(s => s.Equals(nextStatus, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Illegal agreement status transition from '{currentStatus}' to '{nextStatus}'.");
        }
    }

    public static string Normalize(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return AgreementStatus.Draft;
        }

        return status.Trim();
    }

    public static bool IsPricingEligible(string status)
    {
        var normalized = Normalize(status);
        return normalized.Equals(AgreementStatus.Active, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(AgreementStatus.Approved, StringComparison.OrdinalIgnoreCase);
    }
}
