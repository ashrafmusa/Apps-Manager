using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface ISlaReportingService
{
    Task<SlaWorkspaceSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
