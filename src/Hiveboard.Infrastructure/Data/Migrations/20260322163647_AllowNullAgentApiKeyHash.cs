using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hiveboard.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullAgentApiKeyHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ApiKeyHash",
                table: "Agents",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            migrationBuilder.Sql("""
                UPDATE "Agents"
                SET "ApiKeyHash" = NULL
                WHERE "ApiKeyHash" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("""
                    UPDATE "Agents"
                    SET "ApiKeyHash" = substr(replace("Id", '-', '') || replace("Id", '-', ''), 1, 64)
                    WHERE "ApiKeyHash" IS NULL;
                    """);
            }
            else if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    UPDATE "Agents"
                    SET "ApiKeyHash" = substring(replace("Id"::text, '-', '') || replace("Id"::text, '-', '') from 1 for 64)
                    WHERE "ApiKeyHash" IS NULL;
                    """);
            }

            migrationBuilder.AlterColumn<string>(
                name: "ApiKeyHash",
                table: "Agents",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
