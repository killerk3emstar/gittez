using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gittez.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IssuesFetchedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "issues_fetched_at",
                table: "repo_cache",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "issues_fetched_at",
                table: "repo_cache");
        }
    }
}
