using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IAdtService
{
    Task<BedBoardDto> GetBedBoardAsync(CancellationToken cancellationToken = default);
    Task<AdtResult> AdmitAsync(AdmitRequest request, CancellationToken cancellationToken = default);
    Task<AdtResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);
    Task DischargeAsync(DischargeRequest request, CancellationToken cancellationToken = default);
}
