using System.Windows.Controls;

namespace ExcellCore.Module.Extensions.Reporting.Views;

public partial class ReportingModuleView : UserControl
{
    public ReportingModuleView(ReportingWorkspaceView reportingView, SlaWorkspaceView slaView, TelemetryWorkspaceView telemetryView)
    {
        InitializeComponent();
        ReportingContent.Content = reportingView;
        SlaContent.Content = slaView;
        TelemetryContent.Content = telemetryView;
    }
}
