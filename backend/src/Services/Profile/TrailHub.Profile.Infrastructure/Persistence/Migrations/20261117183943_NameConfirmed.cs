using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Profile.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NameConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NameConfirmed",
                schema: "profile",
                table: "Profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameConfirmed",
                schema: "profile",
                table: "Profiles");
        }
    }
}
