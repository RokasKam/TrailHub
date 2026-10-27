using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TrailHub.Trails.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trails");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Trails",
                schema: "trails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Route = table.Column<string>(type: "jsonb", nullable: false),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: false),
                    ElevationGainMeters = table.Column<double>(type: "double precision", nullable: false),
                    ElevationLossMeters = table.Column<double>(type: "double precision", nullable: false),
                    MaxElevationMeters = table.Column<double>(type: "double precision", nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsLoop = table.Column<bool>(type: "boolean", nullable: false),
                    AverageRating = table.Column<double>(type: "double precision", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Path = table.Column<LineString>(type: "geography (linestring, 4326)", nullable: false),
                    StartLatitude = table.Column<double>(type: "double precision", nullable: false),
                    StartLocation = table.Column<Point>(type: "geography (point, 4326)", nullable: false),
                    StartLongitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConditionReports",
                schema: "trails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrailId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConditionReports_Trails_TrailId",
                        column: x => x.TrailId,
                        principalSchema: "trails",
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                schema: "trails",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TrailId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => new { x.UserId, x.TrailId });
                    table.ForeignKey(
                        name: "FK_Favorites_Trails_TrailId",
                        column: x => x.TrailId,
                        principalSchema: "trails",
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointsOfInterest",
                schema: "trails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Elevation = table.Column<double>(type: "double precision", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TrailId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsOfInterest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointsOfInterest_Trails_TrailId",
                        column: x => x.TrailId,
                        principalSchema: "trails",
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "trails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrailId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Trails_TrailId",
                        column: x => x.TrailId,
                        principalSchema: "trails",
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrailPhotos",
                schema: "trails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrailId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrailPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrailPhotos_Trails_TrailId",
                        column: x => x.TrailId,
                        principalSchema: "trails",
                        principalTable: "Trails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConditionReports_TrailId_ReportedAtUtc",
                schema: "trails",
                table: "ConditionReports",
                columns: new[] { "TrailId", "ReportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_TrailId",
                schema: "trails",
                table: "Favorites",
                column: "TrailId");

            migrationBuilder.CreateIndex(
                name: "IX_PointsOfInterest_TrailId",
                schema: "trails",
                table: "PointsOfInterest",
                column: "TrailId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TrailId_UserId",
                schema: "trails",
                table: "Reviews",
                columns: new[] { "TrailId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrailPhotos_TrailId",
                schema: "trails",
                table: "TrailPhotos",
                column: "TrailId");

            migrationBuilder.CreateIndex(
                name: "IX_Trails_Difficulty",
                schema: "trails",
                table: "Trails",
                column: "Difficulty");

            migrationBuilder.CreateIndex(
                name: "IX_Trails_DistanceMeters",
                schema: "trails",
                table: "Trails",
                column: "DistanceMeters");

            migrationBuilder.CreateIndex(
                name: "IX_Trails_Name",
                schema: "trails",
                table: "Trails",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Trails_Path",
                schema: "trails",
                table: "Trails",
                column: "Path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Trails_Region",
                schema: "trails",
                table: "Trails",
                column: "Region")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Trails_StartLocation",
                schema: "trails",
                table: "Trails",
                column: "StartLocation")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConditionReports",
                schema: "trails");

            migrationBuilder.DropTable(
                name: "Favorites",
                schema: "trails");

            migrationBuilder.DropTable(
                name: "PointsOfInterest",
                schema: "trails");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "trails");

            migrationBuilder.DropTable(
                name: "TrailPhotos",
                schema: "trails");

            migrationBuilder.DropTable(
                name: "Trails",
                schema: "trails");
        }
    }
}
