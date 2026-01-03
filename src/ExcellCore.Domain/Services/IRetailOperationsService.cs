using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IRetailOperationsService
{
    Task<RetailDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<RetailSuspendedTicketDto> SuspendAsync(SuspendTransactionRequest request, CancellationToken cancellationToken = default);

    Task<RetailSuspendedTicketDto?> ResumeAsync(ResumeTransactionRequest request, CancellationToken cancellationToken = default);

    Task<RetailReturnDto> RecordReturnAsync(RecordReturnRequest request, CancellationToken cancellationToken = default);
}
