using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartApiGateway.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimiting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableRateLimiting",
                table: "ApiEndpoints",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitPerSecond",
                table: "ApiEndpoints",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableRateLimiting",
                table: "ApiEndpoints");

            migrationBuilder.DropColumn(
                name: "RateLimitPerSecond",
                table: "ApiEndpoints");
        }
    }
}
