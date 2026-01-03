using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class _202512271645_TelemetryHealthAggregation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelemetryAggregates",
                columns: table => new
                {
                    TelemetryAggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetricKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageDurationMs = table.Column<double>(type: "REAL", nullable: false),
                    MaxDurationMs = table.Column<double>(type: "REAL", nullable: false),
                    P95DurationMs = table.Column<double>(type: "REAL", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CriticalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryAggregates", x => x.TelemetryAggregateId);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryHealthSnapshots",
                columns: table => new
                {
                    TelemetryHealthSnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetricKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CapturedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CriticalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    P95DurationMs = table.Column<double>(type: "REAL", nullable: false),
                    MaxDurationMs = table.Column<double>(type: "REAL", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryHealthSnapshots", x => x.TelemetryHealthSnapshotId);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryThresholds",
                columns: table => new
                {
                    TelemetryThresholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetricKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    WarningThresholdMs = table.Column<double>(type: "REAL", nullable: false),
                    CriticalThresholdMs = table.Column<double>(type: "REAL", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryThresholds", x => x.TelemetryThresholdId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryAggregates_MetricKey_PeriodStartUtc",
                table: "TelemetryAggregates",
                columns: new[] { "MetricKey", "PeriodStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryHealthSnapshots_MetricKey_CapturedOnUtc",
                table: "TelemetryHealthSnapshots",
                columns: new[] { "MetricKey", "CapturedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryThresholds_MetricKey",
                table: "TelemetryThresholds",
                column: "MetricKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelemetryAggregates");

            migrationBuilder.DropTable(
                name: "TelemetryHealthSnapshots");

            migrationBuilder.DropTable(
                name: "TelemetryThresholds");
        }
    }
}
