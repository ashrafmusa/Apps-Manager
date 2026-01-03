using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using ExcellCore.Module.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Infrastructure.Services;

public sealed class MetadataFormService : IMetadataFormService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<MetadataFieldDefinition>> Definitions =
        new Dictionary<string, IReadOnlyList<MetadataFieldDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Core.Identity.Clinical"] = new List<MetadataFieldDefinition>
            {
                new("Metadata.BloodType", "Blood Type"),
                new("Metadata.Allergies", "Allergies")
            },
            ["Core.Identity.Retail"] = new List<MetadataFieldDefinition>
            {
                new("Metadata.LoyaltyTier", "Loyalty Tier"),
                new("Metadata.PreferredChannel", "Preferred Channel")
            },
            ["Core.Identity.Corporate"] = new List<MetadataFieldDefinition>
            {
                new("Metadata.AccountManager", "Account Manager"),
                new("Metadata.CostCenter", "Cost Center")
            }
        };

    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;

    public MetadataFormService(IDbContextFactory<ExcellCoreContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public IReadOnlyList<MetadataFieldDefinition> GetDefinitions(string context)
    {
        if (Definitions.TryGetValue(context, out var defs))
        {
            return defs;
        }

        return Array.Empty<MetadataFieldDefinition>();
    }

    public async Task<IReadOnlyList<MetadataFieldValue>> GetFieldsAsync(string context, Guid? aggregateId, CancellationToken cancellationToken = default)
    {
        var definitions = GetDefinitions(context);
        if (!definitions.Any() || aggregateId is null)
        {
            return definitions.Select(d => new MetadataFieldValue(d.Key, d.Label, null, d.DataType)).ToList();
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var values = await db.PartyMetadata
            .Where(m => m.PartyId == aggregateId.Value && m.Context == context)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        return definitions
            .Select(d => new MetadataFieldValue(d.Key, d.Label, map.TryGetValue(d.Key, out var val) ? val : null, d.DataType))
            .ToList();
    }

    public async Task SaveFieldsAsync(string context, Guid aggregateId, IEnumerable<MetadataFieldValue> fields, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.PartyMetadata
            .Where(m => m.PartyId == aggregateId && m.Context == context)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingMap = existing.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            if (existingMap.TryGetValue(field.Key, out var entity))
            {
                entity.Value = field.Value;
                entity.Audit ??= new AuditTrail();
                entity.Audit.ModifiedBy = "identity";
                entity.Audit.ModifiedOnUtc = DateTime.UtcNow;
            }
            else
            {
                db.PartyMetadata.Add(new PartyMetadata
                {
                    PartyMetadataId = Guid.NewGuid(),
                    PartyId = aggregateId,
                    Context = context,
                    Key = field.Key,
                    Value = field.Value,
                    Audit = new AuditTrail
                    {
                        CreatedBy = "identity",
                        SourceModule = "Core.Identity"
                    }
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
