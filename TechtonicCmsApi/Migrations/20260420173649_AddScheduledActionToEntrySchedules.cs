using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechtonicCmsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledActionToEntrySchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                .OldAnnotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .OldAnnotation("Npgsql:Enum:base_resource", "users,collections,entries,assets")
                .OldAnnotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .OldAnnotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .OldAnnotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .OldAnnotation("Npgsql:Enum:logical_operator", "and,or")
                .OldAnnotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .OldAnnotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,ban,unban,activate,deactivate,upload,download,manage_schema")
                .OldAnnotation("Npgsql:Enum:permission_effect", "allow,deny")
                .OldAnnotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .OldAnnotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array");

            migrationBuilder.AlterColumn<bool>(
                name: "AlreadyExecuted",
                table: "entry_schedules",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "entry_schedules",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "entry_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "entry_schedules");

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

            migrationBuilder.AlterColumn<bool>(
                name: "AlreadyExecuted",
                table: "entry_schedules",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "entry_schedules",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");
        }
    }
}
