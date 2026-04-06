using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechtonicCmsApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_field_id,resource_field_name,resource_field_data_type,resource_field_sensitivity_level,resource_field_is_pii,resource_field_is_public,resource_field_collection_id,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .Annotation("Npgsql:Enum:base_resource", "users,collections,entries,assets,fields")
                .Annotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .Annotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,text_list,number_list,asset,rich_text,object")
                .Annotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .Annotation("Npgsql:Enum:logical_operator", "and,or")
                .Annotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex")
                .Annotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,draft,ban,unban,activate,deactivate,upload,download,transform,configure_fields,manage_schema")
                .Annotation("Npgsql:Enum:permission_effect", "allow,deny")
                .Annotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .Annotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastEditTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastLoginTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastEditTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "abac_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Effect = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    RuleConnector = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastEvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abac_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_abac_policies_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<int>(type: "integer", nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Alt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Caption = table.Column<string>(type: "text", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assets_users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    DefaultLocale = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SupportedLocales = table.Column<string[]>(type: "text[]", nullable: false, defaultValue: new[] { "en" }),
                    IsLocalized = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collections_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_ownerships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnershipType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "CREATOR"),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_ownerships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resource_ownerships_users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resource_ownerships_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "abac_policy_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributePath = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    ExpectedValue = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abac_policy_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_abac_policy_rules_abac_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "abac_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_policies_abac_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "abac_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_policies_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_policies_users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_policies_abac_policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "abac_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_policies_users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_policies_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Locale = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DefaultLocale = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entries_assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "assets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_entries_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_entries_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsUnique = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsPii = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SensitivityLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PUBLIC"),
                    ValidationRules = table.Column<string>(type: "text", nullable: true),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    HelpText = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DataType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fields_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fields_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "abac_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAction = table.Column<int>(type: "integer", nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedPolicyIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    MatchingPolicyIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    DecisionReason = table.Column<string>(type: "text", nullable: false),
                    EvaluationTimeMs = table.Column<int>(type: "integer", nullable: false),
                    RequestContext = table.Column<string>(type: "text", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abac_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_abac_audit_fields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_abac_audit_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "abac_evaluation_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    MatchingPolicyIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    EvaluationTimeMs = table.Column<int>(type: "integer", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContextChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PolicyVersions = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_abac_evaluation_cache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_abac_evaluation_cache_fields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "fields",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_abac_evaluation_cache_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_abac_audit_Decision_Timestamp",
                table: "abac_audit",
                columns: new[] { "Decision", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_abac_audit_EvaluationTimeMs",
                table: "abac_audit",
                column: "EvaluationTimeMs");

            migrationBuilder.CreateIndex(
                name: "IX_abac_audit_FieldId",
                table: "abac_audit",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_abac_audit_ResourceType_ResourceId_Timestamp",
                table: "abac_audit",
                columns: new[] { "ResourceType", "ResourceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_abac_audit_Timestamp",
                table: "abac_audit",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_abac_audit_UserId_Timestamp",
                table: "abac_audit",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_abac_evaluation_cache_ContextChecksum",
                table: "abac_evaluation_cache",
                column: "ContextChecksum");

            migrationBuilder.CreateIndex(
                name: "IX_abac_evaluation_cache_Decision",
                table: "abac_evaluation_cache",
                column: "Decision");

            migrationBuilder.CreateIndex(
                name: "IX_abac_evaluation_cache_ExpiresAt",
                table: "abac_evaluation_cache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_abac_evaluation_cache_FieldId",
                table: "abac_evaluation_cache",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_abac_evaluation_cache_UserId_FieldId_ActionType",
                table: "abac_evaluation_cache",
                columns: new[] { "UserId", "FieldId", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_abac_evaluation_cache_UserId_ResourceType_ResourceId_Action~",
                table: "abac_evaluation_cache",
                columns: new[] { "UserId", "ResourceType", "ResourceId", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_abac_policies_CreatedBy",
                table: "abac_policies",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_abac_policies_IsActive",
                table: "abac_policies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_abac_policies_Name",
                table: "abac_policies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_abac_policies_Priority",
                table: "abac_policies",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_abac_policies_ResourceType_ActionType",
                table: "abac_policies",
                columns: new[] { "ResourceType", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_abac_policy_rules_AttributePath",
                table: "abac_policy_rules",
                column: "AttributePath");

            migrationBuilder.CreateIndex(
                name: "IX_abac_policy_rules_PolicyId_IsActive",
                table: "abac_policy_rules",
                columns: new[] { "PolicyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_abac_policy_rules_PolicyId_Order",
                table: "abac_policy_rules",
                columns: new[] { "PolicyId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_assets_UploadedBy",
                table: "assets",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_collections_CreatedBy",
                table: "collections",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_collections_Slug",
                table: "collections",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entries_AssetId",
                table: "entries",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_entries_CollectionId",
                table: "entries",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_entries_CreatedBy",
                table: "entries",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_fields_CollectionId_Name",
                table: "fields",
                columns: new[] { "CollectionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fields_CreatedBy",
                table: "fields",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_fields_IsPii",
                table: "fields",
                column: "IsPii");

            migrationBuilder.CreateIndex(
                name: "IX_fields_SensitivityLevel",
                table: "fields",
                column: "SensitivityLevel");

            migrationBuilder.CreateIndex(
                name: "IX_resource_ownerships_AssignedBy",
                table: "resource_ownerships",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_resource_ownerships_ExpiresAt",
                table: "resource_ownerships",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_resource_ownerships_OwnerId_ResourceType",
                table: "resource_ownerships",
                columns: new[] { "OwnerId", "ResourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_resource_ownerships_ResourceType_ResourceId_OwnerId",
                table: "resource_ownerships",
                columns: new[] { "ResourceType", "ResourceId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_policies_AssignedBy",
                table: "role_policies",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_role_policies_ExpiresAt",
                table: "role_policies",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_role_policies_PolicyId",
                table: "role_policies",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_role_policies_RoleId_PolicyId",
                table: "role_policies",
                columns: new[] { "RoleId", "PolicyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_policies_AssignedBy",
                table: "user_policies",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_user_policies_ExpiresAt",
                table: "user_policies",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_policies_PolicyId",
                table: "user_policies",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_policies_UserId_PolicyId",
                table: "user_policies",
                columns: new[] { "UserId", "PolicyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_UserId_RoleId",
                table: "user_roles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            // CMS JSONB extraction functions for querying dynamic field values.
            // Each function extracts a typed value from a JSONB document by key.
            // IMMUTABLE = same inputs always produce same output (required for expression indexes).
            // STRICT = returns NULL automatically if any input is NULL (handles missing keys).
            migrationBuilder.Sql("""
                CREATE FUNCTION cms_extract_text(doc jsonb, key text) RETURNS text
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT doc->>key;
                  $$;

                CREATE FUNCTION cms_extract_number(doc jsonb, key text) RETURNS numeric
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT (doc->>key)::numeric;
                  $$;

                CREATE FUNCTION cms_extract_boolean(doc jsonb, key text) RETURNS boolean
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT (doc->>key)::boolean;
                  $$;

                CREATE FUNCTION cms_extract_datetime(doc jsonb, key text) RETURNS timestamptz
                  LANGUAGE SQL IMMUTABLE STRICT AS $$
                    SELECT (doc->>key)::timestamptz;
                  $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS cms_extract_text(jsonb, text);
                DROP FUNCTION IF EXISTS cms_extract_number(jsonb, text);
                DROP FUNCTION IF EXISTS cms_extract_boolean(jsonb, text);
                DROP FUNCTION IF EXISTS cms_extract_datetime(jsonb, text);
                """);

            migrationBuilder.DropTable(
                name: "abac_audit");

            migrationBuilder.DropTable(
                name: "abac_evaluation_cache");

            migrationBuilder.DropTable(
                name: "abac_policy_rules");

            migrationBuilder.DropTable(
                name: "entries");

            migrationBuilder.DropTable(
                name: "resource_ownerships");

            migrationBuilder.DropTable(
                name: "role_policies");

            migrationBuilder.DropTable(
                name: "user_policies");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "fields");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "abac_policies");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "collections");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
