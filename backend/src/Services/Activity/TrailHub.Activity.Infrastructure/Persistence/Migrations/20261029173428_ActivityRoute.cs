using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Activity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivityRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RouteId",
                schema: "activity",
                table: "Activities",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RouteId",
                schema: "activity",
                table: "Activities");
        }
    }
}
