using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ExcellCore.Domain.Entities;
using ExcellCore.Domain.Services;
using ExcellCore.Infrastructure.Services;
using ExcellCore.Module.Extensions.Sync.Models;
using ExcellCore.Module.Extensions.Sync.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExcellCore.Tests;

public sealed class SmokeTests : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly TestSqliteContextFactory _factory;
    private readonly SequentialGuidGenerator _guidGenerator;
    private readonly ClinicalWorkflowService _clinical;
    private readonly RetailOperationsService _retail;
    private readonly ReportingService _reporting;
    private readonly ReportingExportService _export;
    private readonly TelemetryService _telemetry;
    private readonly IDeltaSyncProvider _syncProvider;
    private readonly JsonSyncTransportAdapter _transportAdapter;
    private readonly string _artifactRoot;

    public SmokeTests()
    {
        _factory = new TestSqliteContextFactory();
        _guidGenerator = new SequentialGuidGenerator();
        _clinical = new ClinicalWorkflowService(_factory, _guidGenerator);
        _retail = new RetailOperationsService(_factory, _guidGenerator);
        _reporting = new ReportingService(_factory, _guidGenerator);
        _export = new ReportingExportService(_factory, _guidGenerator);
        _telemetry = new TelemetryService(_factory, _guidGenerator);
        _syncProvider = new DeltaSyncProvider(_factory, new ConflictResolverService(_factory));
        _transportAdapter = new JsonSyncTransportAdapter(_syncProvider, "SmokeSite", "SmokeDevice");
        _artifactRoot = InitializeArtifactRoot();
    }

    [Fact]
    public async Task ClinicalAndRetailJourneys_RunEndToEnd()
    {
        var order = await _clinical.CreateOrderAsync(new CreateMedicationOrderRequest(
            null,
            "Smoke Patient",
            "SmokeMed",
            "10 mg",
            "PO",
            "BID",
            "Dr. Smoke",
            DateTime.UtcNow.AddHours(1),
            null,
            "Active",
            "Notes",
            true));

        await _clinical.RecordDispenseAsync(new RecordDispenseRequest(order.MedicationOrderId, "SMOKE-ITEM", 1, "unit"));
        var completed = await _clinical.CompleteNextAdministrationAsync(order.MedicationOrderId, "RN Smoke");

        var clinicalDashboard = await _clinical.GetDashboardAsync();

        Assert.Contains(clinicalDashboard.Orders, o => o.MedicationOrderId == order.MedicationOrderId);
        Assert.NotNull(completed);
        Assert.Equal("Completed", completed!.Status);

        var retailDashboard = await _retail.GetDashboardAsync();
        var suspendedBefore = retailDashboard.Suspended.Count;
        var returnsBefore = retailDashboard.Returns.Count;

        var suspended = await _retail.SuspendAsync(new SuspendTransactionRequest("In-Store", 50.25m, null, "Smoke suspend", null));
        Assert.Equal("Suspended", suspended.Status);

        var resumed = await _retail.ResumeAsync(new ResumeTransactionRequest(suspended.RetailSuspendedTransactionId));
        Assert.Equal("Resumed", resumed!.Status);

        var recordedReturn = await _retail.RecordReturnAsync(new RecordReturnRequest("SMK-RET-1", "In-Store", 19.99m, "Smoke return"));
        Assert.Equal("Pending", recordedReturn.Status);

        var retailAfter = await _retail.GetDashboardAsync();
        Assert.Equal(suspendedBefore + 1, retailAfter.Suspended.Count);
        Assert.Equal(returnsBefore + 1, retailAfter.Returns.Count);
    }

    [Fact]
    public async Task SyncTransport_RoundTripsWithoutTriage()
    {
        var now = DateTime.UtcNow;
        var aggregateId = Guid.NewGuid();
        var inboundDelta = new SyncDelta(
            "Agreement",
            aggregateId,
            new[] { new SyncFieldChange("Status", "Active", "Draft") },
            new VectorClockStamp(new System.Collections.Generic.Dictionary<string, long> { ["RemoteSite"] = now.Ticks }),
            new SyncOrigin("RemoteSite", "DeviceY", now));

        var inboundEnvelope = new SyncTransportEnvelope("RemoteSite", "DeviceY", now, new[] { inboundDelta });
        var inboundPayload = JsonSerializer.Serialize(inboundEnvelope, SerializerOptions);
        WriteArtifact("sync-inbound-envelope.json", inboundPayload);

        var importResult = await _transportAdapter.ImportAsync(inboundPayload);
        Assert.Equal(1, importResult.AppliedCount);

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var ledgerCount = await context.SyncChangeLedgerEntries.CountAsync();
            Assert.True(ledgerCount > 0);

            var triageEntries = await context.SyncChangeLedgerEntries
                .Where(e => e.FieldName == "__triage__")
                .ToListAsync();

            Assert.True(triageEntries.Count <= 1);
            if (triageEntries.Count == 1)
            {
                Assert.Equal(aggregateId, triageEntries[0].AggregateId);
            }
        }

        var outboundPayload = await _transportAdapter.ExportAsync(now.AddMinutes(-5));
        var outboundEnvelope = JsonSerializer.Deserialize<SyncTransportEnvelope>(outboundPayload, SerializerOptions);

        WriteArtifact("sync-outbound-envelope.json", outboundPayload);

        Assert.NotNull(outboundEnvelope);
        Assert.Equal("SmokeSite", outboundEnvelope!.SourceSiteId);
        Assert.NotEmpty(outboundEnvelope.Deltas);
        Assert.Contains(outboundEnvelope.Deltas, delta => delta.AggregateId == aggregateId);
    }

    [Fact]
    public async Task ReportingExportAndTelemetry_StayHealthy()
    {
        var workspace = await _reporting.GetWorkspaceAsync();
        var schedule = workspace.Schedules.First(s => s.Format.Equals("CSV", StringComparison.OrdinalIgnoreCase));

        var export = await _export.GenerateExportAsync(schedule.ReportingScheduleId);
        var csv = Encoding.UTF8.GetString(export.Content);

        WriteArtifact("reporting-export.csv", csv);

        Assert.Equal("text/csv", export.ContentType);
        Assert.Contains(schedule.Name, csv);
        Assert.Contains("Dashboard", csv);

        await using (var context = await _factory.CreateDbContextAsync())
        {
            await context.TelemetryEvents.AddAsync(new TelemetryEvent
            {
                EventType = "Query",
                DurationMilliseconds = 1800,
                OccurredOnUtc = DateTime.UtcNow,
                Audit = new AuditTrail { CreatedBy = "smoke", SourceModule = "smoke" }
            });

            await context.SaveChangesAsync();
        }

        var telemetry = await _telemetry.AggregateAsync();

        Assert.NotNull(telemetry.Health);
        Assert.False(string.IsNullOrWhiteSpace(telemetry.Health.Status));
        Assert.Equal("Critical", telemetry.Health.Status);
        Assert.True(telemetry.Aggregate.CriticalCount >= 1);

        var telemetryJson = JsonSerializer.Serialize(telemetry, SerializerOptions);
        WriteArtifact("telemetry-health.json", telemetryJson);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static string InitializeArtifactRoot()
    {
        var root = Environment.GetEnvironmentVariable("SMOKE_ARTIFACTS_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Path.GetTempPath(), "excellcore-smoke-artifacts");
        }

        Directory.CreateDirectory(root);
        return root;
    }

    private string WriteArtifact(string fileName, string content)
    {
        var path = Path.Combine(_artifactRoot, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
