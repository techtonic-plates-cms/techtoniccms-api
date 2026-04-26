using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechtonicCmsApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedValue",
                table: "abac_policy_rules");

            migrationBuilder.AddColumn<int>(
                name: "ContextReferencePath",
                table: "abac_policy_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "ExpectedArrayValue",
                table: "abac_policy_rules",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExpectedBooleanValue",
                table: "abac_policy_rules",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDateTimeValue",
                table: "abac_policy_rules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExpectedNumberValue",
                table: "abac_policy_rules",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedStringValue",
                table: "abac_policy_rules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpectedUuidValue",
                table: "abac_policy_rules",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextReferencePath",
                table: "abac_policy_rules");

            migrationBuilder.DropColumn(
                name: "ExpectedArrayValue",
                table: "abac_policy_rules");

            migrationBuilder.DropColumn(
                name: "ExpectedBooleanValue",
                table: "abac_policy_rules");

            migrationBuilder.DropColumn(
                name: "ExpectedDateTimeValue",
                table: "abac_policy_rules");

            migrationBuilder.DropColumn(
                name: "ExpectedNumberValue",
                table: "abac_policy_rules");

            migrationBuilder.DropColumn(
                name: "ExpectedStringValue",
                table: "abac_policy_rules");

            migrationBuilder.DropColumn(
                name: "ExpectedUuidValue",
                table: "abac_policy_rules");

            migrationBuilder.AddColumn<string>(
                name: "ExpectedValue",
                table: "abac_policy_rules",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
