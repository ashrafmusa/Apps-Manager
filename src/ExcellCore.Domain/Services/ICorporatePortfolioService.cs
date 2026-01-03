using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface ICorporatePortfolioService
{
    Task<CorporateDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
