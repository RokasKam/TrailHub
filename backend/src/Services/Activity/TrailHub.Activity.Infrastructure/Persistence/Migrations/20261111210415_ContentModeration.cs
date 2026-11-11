using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Activity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HiddenBy",
                schema: "activity",
                table: "HikeComments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentReports",
                schema: "activity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentReports_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalSchema: "activity",
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ActivityId",
                schema: "activity",
                table: "ContentReports",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ContentId_UserId",
                schema: "activity",
                table: "ContentReports",
                columns: new[] { "ContentId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentReports",
                schema: "activity");

            migrationBuilder.DropColumn(
                name: "HiddenBy",
                schema: "activity",
                table: "HikeComments");
        }
    }
}
