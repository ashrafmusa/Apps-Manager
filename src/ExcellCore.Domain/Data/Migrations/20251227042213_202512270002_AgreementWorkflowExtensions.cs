using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class _202512270002_AgreementWorkflowExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoRenew",
                table: "Agreements",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastRenewedOn",
                table: "Agreements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RenewalDate",
                table: "Agreements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "Agreements",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Agreements",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateTable(
                name: "AgreementApprovals",
                columns: table => new
                {
                    AgreementApprovalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Approver = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Comments = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    RequestedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DecidedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementApprovals", x => x.AgreementApprovalId);
                    table.ForeignKey(
                        name: "FK_AgreementApprovals_Agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalTable: "Agreements",
                        principalColumn: "AgreementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementApprovals_AgreementId",
                table: "AgreementApprovals",
                column: "AgreementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementApprovals");

            migrationBuilder.DropColumn(
                name: "AutoRenew",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "LastRenewedOn",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RenewalDate",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Agreements");
        }
    }
}
