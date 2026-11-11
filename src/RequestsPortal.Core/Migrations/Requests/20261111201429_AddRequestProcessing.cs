using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsPortal.Core.Migrations.Requests
{
    /// <inheritdoc />
    public partial class AddRequestProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_Status",
                table: "Requests");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "Requests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedFailureCount",
                table: "Requests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseToken",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntil",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "Requests",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ProviderReceiptId",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status_NextAttemptAt",
                table: "Requests",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_Attempts",
                table: "Requests",
                sql: "AttemptCount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Requests_ConfirmedFailures",
                table: "Requests",
                sql: "ConfirmedFailureCount >= 0 AND ConfirmedFailureCount <= AttemptCount");
            migrationBuilder.Sql("UPDATE Requests SET NextAttemptAt = CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_Status_NextAttemptAt",
                table: "Requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_Attempts",
                table: "Requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Requests_ConfirmedFailures",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ConfirmedFailureCount",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "ProviderReceiptId",
                table: "Requests");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status",
                table: "Requests",
                column: "Status");
        }
    }
}
