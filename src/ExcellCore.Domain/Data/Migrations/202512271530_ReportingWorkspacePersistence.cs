using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReportingWorkspacePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportingDashboards",
                columns: table => new
                {
                    ReportingDashboardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingDashboards", x => x.ReportingDashboardId);
                });

            migrationBuilder.CreateTable(
                name: "ReportingSchedules",
                columns: table => new
                {
                    ReportingScheduleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Cadence = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    NextRunUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportingSchedules", x => x.ReportingScheduleId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportingDashboards");

            migrationBuilder.DropTable(
                name: "ReportingSchedules");
        }
    }
}
