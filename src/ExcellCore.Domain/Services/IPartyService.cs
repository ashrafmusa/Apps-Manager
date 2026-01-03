using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IPartyService
{
    Task<IReadOnlyList<PartySummaryDto>> SearchAsync(string? searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartyLookupResultDto>> LookupAsync(string? searchTerm, CancellationToken cancellationToken = default);
    Task<PartyDetailDto?> GetAsync(Guid partyId, CancellationToken cancellationToken = default);
    Task<PartyDetailDto> SaveAsync(PartyDetailDto detail, CancellationToken cancellationToken = default);
}
