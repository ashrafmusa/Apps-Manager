using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Services;

public sealed record ReportingDashboardDto(Guid ReportingDashboardId, string Name, string Description, string Domain, bool IsActive);

public sealed record ReportingScheduleDto(Guid ReportingScheduleId, string Name, string Format, TimeSpan Cadence, DateTime NextRunUtc, bool IsEnabled);

public sealed record ReportingWorkspaceSummaryDto(int ActiveDashboards, int ScheduledExports, int ImminentRuns);

public sealed record ReportingWorkspaceSnapshotDto(
    ReportingWorkspaceSummaryDto Summary,
    IReadOnlyList<ReportingDashboardDto> Dashboards,
    IReadOnlyList<ReportingScheduleDto> Schedules);

public sealed record ReportingExportResultDto(
    Guid ReportingScheduleId,
    string FileName,
    string ContentType,
    byte[] Content,
    DateTime GeneratedOnUtc);
