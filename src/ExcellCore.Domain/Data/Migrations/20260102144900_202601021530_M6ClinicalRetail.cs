using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class _202601021530_M6ClinicalRetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicationOrders",
                columns: table => new
                {
                    MedicationOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Medication = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Dose = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Route = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OrderingProvider = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OrderedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationOrders", x => x.MedicationOrderId);
                    table.ForeignKey(
                        name: "FK_MedicationOrders_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "PartyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetailReturns",
                columns: table => new
                {
                    RetailReturnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RetailTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TicketNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ReturnedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailReturns", x => x.RetailReturnId);
                });

            migrationBuilder.CreateTable(
                name: "RetailSuspendedTransactions",
                columns: table => new
                {
                    RetailSuspendedTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SuspendedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResumedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailSuspendedTransactions", x => x.RetailSuspendedTransactionId);
                });

            migrationBuilder.CreateTable(
                name: "DispenseEvents",
                columns: table => new
                {
                    DispenseEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MedicationOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InventoryItem = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DispensedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DispensedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispenseEvents", x => x.DispenseEventId);
                    table.ForeignKey(
                        name: "FK_DispenseEvents_MedicationOrders_MedicationOrderId",
                        column: x => x.MedicationOrderId,
                        principalTable: "MedicationOrders",
                        principalColumn: "MedicationOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MedicationAdministrations",
                columns: table => new
                {
                    MedicationAdministrationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MedicationOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AdministeredOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationAdministrations", x => x.MedicationAdministrationId);
                    table.ForeignKey(
                        name: "FK_MedicationAdministrations_MedicationOrders_MedicationOrderId",
                        column: x => x.MedicationOrderId,
                        principalTable: "MedicationOrders",
                        principalColumn: "MedicationOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DispenseEvents_MedicationOrderId_DispensedOnUtc",
                table: "DispenseEvents",
                columns: new[] { "MedicationOrderId", "DispensedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationAdministrations_MedicationOrderId_ScheduledForUtc",
                table: "MedicationAdministrations",
                columns: new[] { "MedicationOrderId", "ScheduledForUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationOrders_OrderNumber",
                table: "MedicationOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicationOrders_PartyId",
                table: "MedicationOrders",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailReturns_RetailTransactionId_TicketNumber",
                table: "RetailReturns",
                columns: new[] { "RetailTransactionId", "TicketNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailSuspendedTransactions_TicketNumber",
                table: "RetailSuspendedTransactions",
                column: "TicketNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispenseEvents");

            migrationBuilder.DropTable(
                name: "MedicationAdministrations");

            migrationBuilder.DropTable(
                name: "RetailReturns");

            migrationBuilder.DropTable(
                name: "RetailSuspendedTransactions");

            migrationBuilder.DropTable(
                name: "MedicationOrders");
        }
    }
}
