using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsPortal.Core.Migrations.Provider
{
    /// <inheritdoc />
    public partial class InitialProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderOperations",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvocationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiptId = table.Column<string>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderOperations", x => x.RequestId);
                    table.CheckConstraint("CK_ProviderOperations_InvocationCount", "InvocationCount >= 0");
                    table.CheckConstraint("CK_ProviderOperations_Receipt", "(ReceiptId IS NULL AND CompletedAt IS NULL) OR (ReceiptId IS NOT NULL AND CompletedAt IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderOperations_ReceiptId",
                table: "ProviderOperations",
                column: "ReceiptId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderOperations");
        }
    }
}
