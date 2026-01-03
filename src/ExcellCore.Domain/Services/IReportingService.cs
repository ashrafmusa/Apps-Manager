using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IReportingService
{
    Task<ReportingWorkspaceSnapshotDto> GetWorkspaceAsync(CancellationToken cancellationToken = default);
}
