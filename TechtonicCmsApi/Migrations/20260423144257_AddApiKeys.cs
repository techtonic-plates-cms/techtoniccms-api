using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechtonicCmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,resource_api_key_user_id,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .Annotation("Npgsql:Enum:base_resource", "users,collections,entries,assets,api_keys")
                .Annotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .Annotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .Annotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .Annotation("Npgsql:Enum:logical_operator", "and,or")
                .Annotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .Annotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,ban,unban,activate,deactivate,upload,download,manage_schema")
                .Annotation("Npgsql:Enum:permission_effect", "allow,deny")
                .Annotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .Annotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .Annotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array")
                .OldAnnotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .OldAnnotation("Npgsql:Enum:base_resource", "users,collections,entries,assets")
                .OldAnnotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .OldAnnotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .OldAnnotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .OldAnnotation("Npgsql:Enum:logical_operator", "and,or")
                .OldAnnotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .OldAnnotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,ban,unban,activate,deactivate,upload,download,manage_schema")
                .OldAnnotation("Npgsql:Enum:permission_effect", "allow,deny")
                .OldAnnotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .OldAnnotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .OldAnnotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array");

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_keys_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_IsActive",
                table: "api_keys",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_KeyHash",
                table: "api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId",
                table: "api_keys",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .Annotation("Npgsql:Enum:base_resource", "users,collections,entries,assets")
                .Annotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .Annotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .Annotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .Annotation("Npgsql:Enum:logical_operator", "and,or")
                .Annotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .Annotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,ban,unban,activate,deactivate,upload,download,manage_schema")
                .Annotation("Npgsql:Enum:permission_effect", "allow,deny")
                .Annotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .Annotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .Annotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array")
                .OldAnnotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,resource_api_key_user_id,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .OldAnnotation("Npgsql:Enum:base_resource", "users,collections,entries,assets,api_keys")
                .OldAnnotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .OldAnnotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .OldAnnotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .OldAnnotation("Npgsql:Enum:logical_operator", "and,or")
                .OldAnnotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .OldAnnotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,ban,unban,activate,deactivate,upload,download,manage_schema")
                .OldAnnotation("Npgsql:Enum:permission_effect", "allow,deny")
                .OldAnnotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .OldAnnotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .OldAnnotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array");
        }
    }
}
