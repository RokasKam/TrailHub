using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Groups.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupHikeRsvps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroupHikeRsvps",
                schema: "groups",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HikeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupHikeRsvps", x => new { x.HikeId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GroupHikeRsvps_GroupHikes_HikeId",
                        column: x => x.HikeId,
                        principalSchema: "groups",
                        principalTable: "GroupHikes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupHikeRsvps",
                schema: "groups");
        }
    }
}
