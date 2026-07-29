using System;
using System.Collections.Generic;
using Gittez.Core.Models;
using Gittez.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gittez.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issue_cache",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    repo_full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    html_url = table.Column<string>(type: "text", nullable: false),
                    labels = table.Column<string>(type: "jsonb", nullable: false),
                    comment_count = table.Column<int>(type: "integer", nullable: false),
                    body_length = table.Column<int>(type: "integer", nullable: false),
                    has_assignee = table.Column<bool>(type: "boolean", nullable: false),
                    difficulty = table.Column<short>(type: "smallint", nullable: false),
                    issue_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    issue_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    github_login = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    top_languages = table.Column<List<ProfileLanguage>>(type: "jsonb", nullable: false),
                    median_size_kb = table.Column<int>(type: "integer", nullable: false),
                    interests = table.Column<string>(type: "jsonb", nullable: false),
                    public_repo_count = table.Column<int>(type: "integer", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profiles", x => x.github_login);
                });

            migrationBuilder.CreateTable(
                name: "repo_cache",
                columns: table => new
                {
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    data = table.Column<RepoCandidate>(type: "jsonb", nullable: false),
                    etag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    health_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    health_breakdown = table.Column<List<ScoreComponent>>(type: "jsonb", nullable: true),
                    health_computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repo_cache", x => x.full_name);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "watchlist_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repo_full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlist_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_watchlist_items_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issue_cache_repo",
                table: "issue_cache",
                column: "repo_full_name");

            migrationBuilder.CreateIndex(
                name: "ix_repo_cache_fetched",
                table: "repo_cache",
                column: "fetched_at");

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_items_session_id_repo_full_name",
                table: "watchlist_items",
                columns: new[] { "session_id", "repo_full_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_watchlist_session",
                table: "watchlist_items",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_cache");

            migrationBuilder.DropTable(
                name: "profiles");

            migrationBuilder.DropTable(
                name: "repo_cache");

            migrationBuilder.DropTable(
                name: "watchlist_items");

            migrationBuilder.DropTable(
                name: "sessions");
        }
    }
}
