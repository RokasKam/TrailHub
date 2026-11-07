using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Trails.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrailModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAtUtc",
                schema: "trails",
                table: "Trails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenBy",
                schema: "trails",
                table: "Trails",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrailReports",
                schema: "trails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrailId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrailReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrailReports_Trails_TrailId",
                        column: x => x.TrailId,
                        principalSchema: "trails",
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrailReports_TrailId_UserId",
                schema: "trails",
                table: "TrailReports",
                columns: new[] { "TrailId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrailReports",
                schema: "trails");

            migrationBuilder.DropColumn(
                name: "HiddenAtUtc",
                schema: "trails",
                table: "Trails");

            migrationBuilder.DropColumn(
                name: "HiddenBy",
                schema: "trails",
                table: "Trails");
        }
    }
}
