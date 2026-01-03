using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncLedgerAndState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncChangeLedgerEntries",
                columns: table => new
                {
                    SyncChangeLedgerEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PreviousValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    OriginSiteId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OriginDeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ObservedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VectorClockJson = table.Column<string>(type: "TEXT", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncChangeLedgerEntries", x => x.SyncChangeLedgerEntryId);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    SyncStateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VectorClockJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.SyncStateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncChangeLedgerEntries_AggregateType_AggregateId_ObservedOnUtc",
                table: "SyncChangeLedgerEntries",
                columns: new[] { "AggregateType", "AggregateId", "ObservedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_AggregateType_AggregateId",
                table: "SyncStates",
                columns: new[] { "AggregateType", "AggregateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncChangeLedgerEntries");

            migrationBuilder.DropTable(
                name: "SyncStates");
        }
    }
}
