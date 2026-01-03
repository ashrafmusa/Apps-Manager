using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcellCore.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class _202601031715_M8AdtFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wards",
                columns: table => new
                {
                    WardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.WardId);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_Rooms_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "WardId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Beds",
                columns: table => new
                {
                    BedId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BedNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsIsolation = table.Column<bool>(type: "INTEGER", nullable: false),
                    OccupiedByPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccupiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Audit_CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Audit_ModifiedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Audit_ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Audit_SourceModule = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beds", x => x.BedId);
                    table.ForeignKey(
                        name: "FK_Beds_Parties_OccupiedByPartyId",
                        column: x => x.OccupiedByPartyId,
                        principalTable: "Parties",
                        principalColumn: "PartyId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Beds_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Beds_OccupiedByPartyId",
                table: "Beds",
                column: "OccupiedByPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_RoomId_BedNumber",
                table: "Beds",
                columns: new[] { "RoomId", "BedNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_WardId_RoomNumber",
                table: "Rooms",
                columns: new[] { "WardId", "RoomNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_WardId",
                table: "Rooms",
                column: "WardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Beds");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Wards");
        }
    }
}
