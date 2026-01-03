using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExcellCore.Domain.Services;

public interface IReportingExportService
{
    Task<ReportingExportResultDto> GenerateExportAsync(Guid scheduleId, CancellationToken cancellationToken = default);
}
