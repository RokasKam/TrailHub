using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GearLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GearLists",
                schema: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Items = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearLists", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GearLists_OwnerId",
                schema: "trips",
                table: "GearLists",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GearLists",
                schema: "trips");
        }
    }
}
