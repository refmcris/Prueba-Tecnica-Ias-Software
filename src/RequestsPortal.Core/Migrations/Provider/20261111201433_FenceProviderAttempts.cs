using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RequestsPortal.Core.Migrations.Provider
{
    /// <inheritdoc />
    public partial class FenceProviderAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HighestAttempt",
                table: "ProviderOperations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProviderOperations_HighestAttempt",
                table: "ProviderOperations",
                sql: "HighestAttempt >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProviderOperations_HighestAttempt",
                table: "ProviderOperations");

            migrationBuilder.DropColumn(
                name: "HighestAttempt",
                table: "ProviderOperations");
        }
    }
}
