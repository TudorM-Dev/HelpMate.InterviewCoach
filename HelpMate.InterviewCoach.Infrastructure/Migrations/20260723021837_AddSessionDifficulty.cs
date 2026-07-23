using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpMate.InterviewCoach.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDifficulty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Sessions");
        }
    }
}
