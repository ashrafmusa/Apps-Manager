using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Domain.Services;

public sealed class PartyService : IPartyService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;
    private readonly ISequentialGuidGenerator _idGenerator;

    public PartyService(IDbContextFactory<ExcellCoreContext> contextFactory, ISequentialGuidGenerator idGenerator)
    {
        _contextFactory = contextFactory;
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<IReadOnlyList<PartySummaryDto>> SearchAsync(string? searchTerm, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        var query = dbContext.Parties.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.DisplayName, $"%{term}%") ||
                (p.NationalId != null && EF.Functions.Like(p.NationalId, $"%{term}%")) ||
                p.Identifiers.Any(i => EF.Functions.Like(i.Value, $"%{term}%")));
        }

        var results = await query
            .OrderBy(p => p.DisplayName)
            .Select(p => new PartySummaryDto(
                p.PartyId,
                p.DisplayName,
                p.PartyType,
                p.NationalId ?? p.Identifiers.OrderBy(i => i.Scheme).Select(i => i.Value).FirstOrDefault(),
                p.DateOfBirth))
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<PartyLookupResultDto>> LookupAsync(string? searchTerm, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);

        var query = dbContext.Parties.AsNoTracking();

        var hasSearch = !string.IsNullOrWhiteSpace(searchTerm) && searchTerm.Trim().Length >= 2;

        if (hasSearch)
        {
            var term = searchTerm!.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.DisplayName, $"%{term}%") ||
                (p.NationalId != null && EF.Functions.Like(p.NationalId, $"%{term}%")) ||
                p.Identifiers.Any(i => EF.Functions.Like(i.Value, $"%{term}%")));
        }

        query = query.OrderBy(p => p.DisplayName);

        var results = await query
            .Take(20)
            .Select(p => new PartyLookupResultDto(
                p.PartyId,
                p.DisplayName,
                p.PartyType,
                p.NationalId ?? p.Identifiers.OrderBy(i => i.Scheme).Select(i => i.Value).FirstOrDefault(),
                BuildRelationshipContext(p)))
            .ToListAsync(cancellationToken);

        return results;
    }

    private static string BuildRelationshipContext(Party party)
    {
        var scheme = party.NationalId is not null
            ? "National ID"
            : party.Identifiers.OrderBy(i => i.Scheme).Select(i => i.Scheme).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(scheme))
        {
            return party.PartyType;
        }

        return FormattableString.Invariant($"{party.PartyType} · {scheme}");
    }

    public async Task<PartyDetailDto?> GetAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        var entity = await dbContext.Parties
            .Include(p => p.Identifiers)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartyId == partyId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return Map(entity);
    }

    public async Task<PartyDetailDto> SaveAsync(PartyDetailDto detail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(detail.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(detail));
        }
        if (string.IsNullOrWhiteSpace(detail.PartyType))
        {
            throw new ArgumentException("Party type is required.", nameof(detail));
        }

        await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.EnsureDatabaseMigratedAsync(cancellationToken);
        Party? entity = null;

        if (detail.PartyId.HasValue)
        {
            entity = await dbContext.Parties
                .Include(p => p.Identifiers)
                .FirstOrDefaultAsync(p => p.PartyId == detail.PartyId.Value, cancellationToken);
        }

        var isUpdate = entity is not null;

        if (entity is null)
        {
            entity = new Party
            {
                PartyId = detail.PartyId ?? _idGenerator.Create(),
                Audit = new AuditTrail
                {
                    CreatedBy = "desktop",
                    SourceModule = "Core.Identity"
                }
            };
            dbContext.Parties.Add(entity);
        }
        else
        {
            entity.Audit ??= new AuditTrail();
            entity.Audit.ModifiedBy = "desktop";
            entity.Audit.ModifiedOnUtc = DateTime.UtcNow;
        }

        entity.DisplayName = detail.DisplayName.Trim();
        entity.PartyType = detail.PartyType.Trim();
        entity.NationalId = string.IsNullOrWhiteSpace(detail.NationalId) ? null : detail.NationalId.Trim();
        entity.DateOfBirth = detail.DateOfBirth;

        var existingIdentifiers = entity.Identifiers.ToDictionary(i => i.PartyIdentifierId, i => i);
        entity.Identifiers.Clear();

        foreach (var identifier in detail.Identifiers.Where(i => !string.IsNullOrWhiteSpace(i.Value)))
        {
            var scheme = string.IsNullOrWhiteSpace(identifier.Scheme) ? "DEFAULT" : identifier.Scheme.Trim().ToUpperInvariant();
            var value = identifier.Value.Trim();

            if (identifier.PartyIdentifierId.HasValue && existingIdentifiers.TryGetValue(identifier.PartyIdentifierId.Value, out var existing))
            {
                existing.Scheme = scheme;
                existing.Value = value;
                existingIdentifiers.Remove(existing.PartyIdentifierId);
                entity.Identifiers.Add(existing);
            }
            else
            {
                entity.Identifiers.Add(new PartyIdentifier
                {
                    PartyIdentifierId = identifier.PartyIdentifierId ?? _idGenerator.Create(),
                    PartyId = entity.PartyId,
                    Scheme = scheme,
                    Value = value,
                    Audit = new AuditTrail
                    {
                        CreatedBy = isUpdate ? "desktop" : "desktop",
                        SourceModule = "Core.Identity"
                    }
                });
            }
        }

        if (existingIdentifiers.Count > 0)
        {
            dbContext.PartyIdentifiers.RemoveRange(existingIdentifiers.Values);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(entity.PartyId, cancellationToken) ?? Map(entity);
    }

    private static PartyDetailDto Map(Party entity)
    {
        var identifiers = entity.Identifiers
            .OrderBy(i => i.Scheme)
            .Select(i => new PartyIdentifierDto(i.PartyIdentifierId, i.Scheme, i.Value))
            .ToList();

        return new PartyDetailDto(
            entity.PartyId,
            entity.DisplayName,
            entity.PartyType,
            entity.DateOfBirth,
            entity.NationalId,
            identifiers);
    }
}
