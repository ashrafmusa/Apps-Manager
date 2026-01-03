using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetailAndCorporateDashboards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorporateContracts",
                columns: table => new
                {
                    CorporateContractId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContractValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RenewalDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Program = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AllocationRatio = table.Column<decimal>(type: "decimal(6,3)", nullable: false),
                    AllocationStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateContracts", x => x.CorporateContractId);
                });

            migrationBuilder.CreateTable(
                name: "RetailTransactions",
                columns: table => new
                {
                    RetailTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoyaltyEnrollment = table.Column<bool>(type: "INTEGER", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailTransactions", x => x.RetailTransactionId);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RetailTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RaisedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_Tickets_RetailTransactions_RetailTransactionId",
                        column: x => x.RetailTransactionId,
                        principalTable: "RetailTransactions",
                        principalColumn: "RetailTransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RetailTransactionId_TicketNumber",
                table: "Tickets",
                columns: new[] { "RetailTransactionId", "TicketNumber" });

            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "AgreementImpactedParties",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "AgreementImpactedParties",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: false,
                oldDefaultValue: "");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "RetailTransactions");

            migrationBuilder.DropTable(
                name: "CorporateContracts");
        }
    }
}
