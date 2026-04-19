using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartApiGateway.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingToModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BlockedIps",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "BlockedById",
                table: "BlockedIps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "ApiEndpoints",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlockedIps_BlockedById",
                table: "BlockedIps",
                column: "BlockedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpoints_CreatedById",
                table: "ApiEndpoints",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiEndpoints_AspNetUsers_CreatedById",
                table: "ApiEndpoints",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedIps_AspNetUsers_BlockedById",
                table: "BlockedIps",
                column: "BlockedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiEndpoints_AspNetUsers_CreatedById",
                table: "ApiEndpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_BlockedIps_AspNetUsers_BlockedById",
                table: "BlockedIps");

            migrationBuilder.DropIndex(
                name: "IX_BlockedIps_BlockedById",
                table: "BlockedIps");

            migrationBuilder.DropIndex(
                name: "IX_ApiEndpoints_CreatedById",
                table: "ApiEndpoints");

            migrationBuilder.DropColumn(
                name: "BlockedById",
                table: "BlockedIps");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ApiEndpoints");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BlockedIps",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
