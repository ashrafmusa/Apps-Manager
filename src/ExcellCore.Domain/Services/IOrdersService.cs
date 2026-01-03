using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IOrdersService
{
    Task<LabOrderDto> PlaceLabOrderAsync(PlaceLabOrderRequest request, CancellationToken cancellationToken = default);
    Task<OrderResultDto> IngestResultAsync(IngestResultRequest request, CancellationToken cancellationToken = default);
    Task<OrdersDashboardDto> GetOrdersAsync(CancellationToken cancellationToken = default);
}
