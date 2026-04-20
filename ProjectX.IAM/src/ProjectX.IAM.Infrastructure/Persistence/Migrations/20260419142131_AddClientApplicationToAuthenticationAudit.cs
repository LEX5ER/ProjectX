using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectX.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientApplicationToAuthenticationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientApplication",
                table: "AuthenticationAuditEntries",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientApplication",
                table: "AuthenticationAuditEntries");
        }
    }
}
