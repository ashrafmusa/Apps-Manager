using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations;

[DbContext(typeof(ExcellCoreContext))]
[Migration("202512270650_IdentityImpactAndActionLog")]
public partial class IdentityImpactAndActionLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgreementImpactedParties",
            columns: table => new
            {
                AgreementImpactedPartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                AgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                Relationship = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgreementImpactedParties", x => x.AgreementImpactedPartyId);
                table.ForeignKey(
                    name: "FK_AgreementImpactedParties_Agreements_AgreementId",
                    column: x => x.AgreementId,
                    principalTable: "Agreements",
                    principalColumn: "AgreementId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AgreementImpactedParties_Parties_PartyId",
                    column: x => x.PartyId,
                    principalTable: "Parties",
                    principalColumn: "PartyId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ActionLogs",
            columns: table => new
            {
                ActionLogId = table.Column<Guid>(type: "TEXT", nullable: false),
                AgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                AgreementApprovalId = table.Column<Guid>(type: "TEXT", nullable: true),
                ActionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                PerformedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ActionedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActionLogs", x => x.ActionLogId);
                table.ForeignKey(
                    name: "FK_ActionLogs_AgreementApprovals_AgreementApprovalId",
                    column: x => x.AgreementApprovalId,
                    principalTable: "AgreementApprovals",
                    principalColumn: "AgreementApprovalId",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ActionLogs_Agreements_AgreementId",
                    column: x => x.AgreementId,
                    principalTable: "Agreements",
                    principalColumn: "AgreementId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ActionLogs_AgreementApprovalId",
            table: "ActionLogs",
            column: "AgreementApprovalId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionLogs_AgreementId",
            table: "ActionLogs",
            column: "AgreementId");

        migrationBuilder.CreateIndex(
            name: "IX_AgreementImpactedParties_AgreementId_PartyId",
            table: "AgreementImpactedParties",
            columns: new[] { "AgreementId", "PartyId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AgreementImpactedParties_PartyId",
            table: "AgreementImpactedParties",
            column: "PartyId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ActionLogs");

        migrationBuilder.DropTable(
            name: "AgreementImpactedParties");
    }
}