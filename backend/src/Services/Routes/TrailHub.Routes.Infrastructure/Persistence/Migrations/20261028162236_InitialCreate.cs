using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Routes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "routes");

            migrationBuilder.CreateTable(
                name: "Routes",
                schema: "routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SacScale = table.Column<byte[]>(type: "bytea", nullable: false),
                    WaypointIndices = table.Column<int[]>(type: "integer[]", nullable: false),
                    Snapped = table.Column<bool>(type: "boolean", nullable: false),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: false),
                    AscentMeters = table.Column<double>(type: "double precision", nullable: false),
                    DescentMeters = table.Column<double>(type: "double precision", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Points = table.Column<string>(type: "jsonb", nullable: false),
                    Waypoints = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_OwnerId_UpdatedAtUtc",
                schema: "routes",
                table: "Routes",
                columns: new[] { "OwnerId", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Routes",
                schema: "routes");
        }
    }
}
