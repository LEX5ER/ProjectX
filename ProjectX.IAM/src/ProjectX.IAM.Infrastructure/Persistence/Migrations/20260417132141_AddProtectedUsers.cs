using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectX.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProtected",
                table: "Users");
        }
    }
}
