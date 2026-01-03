using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record PartySummaryDto(Guid PartyId, string DisplayName, string PartyType, string? PrimaryIdentifier, DateOnly? DateOfBirth);

public sealed record PartyLookupResultDto(Guid PartyId, string DisplayName, string PartyType, string? PrimaryIdentifier, string RelationshipContext);

public sealed record PartyIdentifierDto(Guid? PartyIdentifierId, string Scheme, string Value);

public sealed record PartyDetailDto(
    Guid? PartyId,
    string DisplayName,
    string PartyType,
    DateOnly? DateOfBirth,
    string? NationalId,
    IReadOnlyList<PartyIdentifierDto> Identifiers);
