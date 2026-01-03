using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations;

[DbContext(typeof(ExcellCoreContext))]
[Migration("202512270001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Parties",
            columns: table => new
            {
                PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                PartyType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                NationalId = table.Column<string>(type: "TEXT", nullable: true),
                DateOfBirth = table.Column<DateOnly>(type: "TEXT", nullable: true),
                Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Parties", x => x.PartyId);
            });

        migrationBuilder.CreateTable(
            name: "Agreements",
            columns: table => new
            {
                AgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                AgreementName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                PayerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CoverageType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Agreements", x => x.AgreementId);
            });

        migrationBuilder.CreateTable(
            name: "PartyIdentifiers",
            columns: table => new
            {
                PartyIdentifierId = table.Column<Guid>(type: "TEXT", nullable: false),
                Scheme = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PartyIdentifiers", x => x.PartyIdentifierId);
                table.ForeignKey(
                    name: "FK_PartyIdentifiers_Parties_PartyId",
                    column: x => x.PartyId,
                    principalTable: "Parties",
                    principalColumn: "PartyId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AgreementRates",
            columns: table => new
            {
                AgreementRateId = table.Column<Guid>(type: "TEXT", nullable: false),
                AgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                ServiceCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                CopayPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgreementRates", x => x.AgreementRateId);
                table.ForeignKey(
                    name: "FK_AgreementRates_Agreements_AgreementId",
                    column: x => x.AgreementId,
                    principalTable: "Agreements",
                    principalColumn: "AgreementId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgreementRates_AgreementId",
            table: "AgreementRates",
            column: "AgreementId");

        migrationBuilder.CreateIndex(
            name: "IX_PartyIdentifiers_PartyId",
            table: "PartyIdentifiers",
            column: "PartyId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AgreementRates");

        migrationBuilder.DropTable(
            name: "PartyIdentifiers");

        migrationBuilder.DropTable(
            name: "Agreements");

        migrationBuilder.DropTable(
            name: "Parties");
    }
}
