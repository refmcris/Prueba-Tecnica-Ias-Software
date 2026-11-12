using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsPortal.Core.Migrations.Requests
{
    /// <inheritdoc />
    public partial class InitialRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalReference = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false, collation: "BINARY"),
                    CustomerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "BINARY"),
                    ServiceCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.CheckConstraint("CK_Requests_Status", "Status IN ('Pending', 'Processing', 'Completed', 'Failed')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CustomerId_ExternalReference",
                table: "Requests",
                columns: new[] { "CustomerId", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status",
                table: "Requests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Requests");
        }
    }
}
