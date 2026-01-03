using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class _202601021730_M7PredictiveTelemetryInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryAnomalyAlerts",
                columns: table => new
                {
                    InventoryAnomalyAlertId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SignalType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    VelocityPerDay = table.Column<double>(type: "REAL", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DetectedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAnomalyAlerts", x => x.InventoryAnomalyAlertId);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLedgerEntries",
                columns: table => new
                {
                    InventoryLedgerEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OnOrder = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLedgerEntries", x => x.InventoryLedgerEntryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAnomalyAlerts_Sku_Location_DetectedOnUtc",
                table: "InventoryAnomalyAlerts",
                columns: new[] { "Sku", "Location", "DetectedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLedgerEntries_Sku_Location_OccurredOnUtc",
                table: "InventoryLedgerEntries",
                columns: new[] { "Sku", "Location", "OccurredOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryAnomalyAlerts");

            migrationBuilder.DropTable(
                name: "InventoryLedgerEntries");
        }
    }
}
