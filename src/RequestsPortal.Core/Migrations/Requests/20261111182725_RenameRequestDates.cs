using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsPortal.Core.Migrations.Requests
{
    /// <inheritdoc />
    public partial class RenameRequestDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "Requests",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Requests",
                newName: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Requests",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Requests",
                newName: "CreatedAtUtc");
        }
    }
}
