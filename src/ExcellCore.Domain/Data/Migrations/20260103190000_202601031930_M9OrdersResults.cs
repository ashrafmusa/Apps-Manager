using System;
using ExcellCore.Domain.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ExcellCoreContext))]
    [Migration("20260103190000_202601031930_M9OrdersResults")]
    public partial class _202601031930_M9OrdersResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    LabOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TestCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ExternalSystem = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ExternalOrderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OrderingProvider = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    OrderedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrders", x => x.LabOrderId);
                });

            migrationBuilder.CreateTable(
                name: "OrderSets",
                columns: table => new
                {
                    OrderSetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSets", x => x.OrderSetId);
                });

            migrationBuilder.CreateTable(
                name: "OrderResults",
                columns: table => new
                {
                    OrderResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LabOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResultCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResultValue = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ResultStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Units = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ReferenceRange = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CollectedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderResults", x => x.OrderResultId);
                    table.ForeignKey(
                        name: "FK_OrderResults_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "LabOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_ExternalSystem_ExternalOrderId",
                table: "LabOrders",
                columns: new[] { "ExternalSystem", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_OrderNumber",
                table: "LabOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderResults_LabOrderId_CollectedOnUtc",
                table: "OrderResults",
                columns: new[] { "LabOrderId", "CollectedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderSets_Name_Version",
                table: "OrderSets",
                columns: new[] { "Name", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderResults");

            migrationBuilder.DropTable(
                name: "OrderSets");

            migrationBuilder.DropTable(
                name: "LabOrders");
        }
    }
}
