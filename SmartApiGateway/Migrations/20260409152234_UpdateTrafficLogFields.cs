using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartApiGateway.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTrafficLogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "TrafficLogs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "Method",
                table: "TrafficLogs",
                newName: "RequestedUrl");

            migrationBuilder.RenameColumn(
                name: "LatencyMs",
                table: "TrafficLogs",
                newName: "ResponseTimeMs");

            migrationBuilder.RenameColumn(
                name: "Endpoint",
                table: "TrafficLogs",
                newName: "HttpMethod");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ResponseTimeMs",
                table: "TrafficLogs",
                newName: "LatencyMs");

            migrationBuilder.RenameColumn(
                name: "RequestedUrl",
                table: "TrafficLogs",
                newName: "Method");

            migrationBuilder.RenameColumn(
                name: "HttpMethod",
                table: "TrafficLogs",
                newName: "Endpoint");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "TrafficLogs",
                newName: "Timestamp");
        }
    }
}
