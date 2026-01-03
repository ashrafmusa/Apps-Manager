using System;
using ExcellCore.Domain.Entities;

namespace ExcellCore.Domain.Services.Pricing;

public static class PricingCalculator
{
    public static PricingResultDto Calculate(PricingCalculationContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context.Quantity), "Quantity must be greater than zero.");
        }

        if (!AgreementWorkflowRules.IsPricingEligible(context.Status))
        {
            return new PricingResultDto(0m, 0m, 0m);
        }

        if (!IsEffectiveOn(context))
        {
            return new PricingResultDto(0m, 0m, 0m);
        }

        var baseAmount = context.RateBaseAmount > 0 ? context.RateBaseAmount.Value : context.ListPrice;
        if (baseAmount <= 0)
        {
            return new PricingResultDto(0m, 0m, 0m);
        }

        var discountPercent = Math.Clamp(context.DiscountPercent, 0m, 100m);
        var copayPercent = Math.Clamp(context.CopayPercent ?? 0m, 0m, 100m);

        var discountPerUnit = baseAmount * (discountPercent / 100m);
        var copayPerUnit = baseAmount * (copayPercent / 100m);
        var netPerUnit = Math.Max(baseAmount - discountPerUnit - copayPerUnit, 0m);

        var quantity = context.Quantity;
        var netTotal = netPerUnit * quantity;
        var discountTotal = discountPerUnit * quantity;
        var copayTotal = copayPerUnit * quantity;

        return new PricingResultDto(netTotal, discountTotal, copayTotal);
    }

    private static bool IsEffectiveOn(PricingCalculationContext context)
    {
        var today = context.PricingDate;
        if (today < context.EffectiveFrom)
        {
            return false;
        }

        if (context.EffectiveTo.HasValue && today > context.EffectiveTo.Value)
        {
            return false;
        }

        if (context.Status.Equals(AgreementStatus.Approved, StringComparison.OrdinalIgnoreCase) && context.RenewalDate.HasValue && today > context.RenewalDate.Value)
        {
            return false;
        }

        return true;
    }
}

public sealed record PricingCalculationContext(
    string Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateOnly PricingDate,
    decimal ListPrice,
    int Quantity,
    decimal? RateBaseAmount,
    decimal DiscountPercent,
    decimal? CopayPercent,
    DateOnly? RenewalDate,
    DateOnly? LastRenewedOn);
