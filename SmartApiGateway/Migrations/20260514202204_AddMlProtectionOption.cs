using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartApiGateway.Migrations
{
    /// <inheritdoc />
    public partial class AddMlProtectionOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableMlAnomalyDetection",
                table: "ApiEndpoints",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableMlAnomalyDetection",
                table: "ApiEndpoints");
        }
    }
}
