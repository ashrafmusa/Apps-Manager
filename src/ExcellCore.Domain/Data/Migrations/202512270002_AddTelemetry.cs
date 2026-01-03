using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations;

[DbContext(typeof(ExcellCoreContext))]
[Migration("202512270002_AddTelemetry")]
public partial class AddTelemetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TelemetryEvents",
            columns: table => new
            {
                TelemetryEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                EventType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CommandText = table.Column<string>(type: "TEXT", nullable: false),
                DurationMilliseconds = table.Column<double>(type: "REAL", nullable: false),
                OccurredOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TelemetryEvents", x => x.TelemetryEventId);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TelemetryEvents");
    }
}
