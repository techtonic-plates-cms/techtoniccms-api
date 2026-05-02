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
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,resource_api_key_user_id,resource_audit_user_id,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .Annotation("Npgsql:Enum:base_resource", "users,collections,entries,assets,api_keys,policies,roles,audits,wildcard")
                .Annotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .Annotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .Annotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .Annotation("Npgsql:Enum:logical_operator", "and,or")
                .Annotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .Annotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,activate,deactivate,upload,download,manage_schema,wildcard")
                .Annotation("Npgsql:Enum:permission_effect", "allow,deny")
                .Annotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .Annotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .Annotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array")
                .OldAnnotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,resource_api_key_user_id,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .OldAnnotation("Npgsql:Enum:base_resource", "users,collections,entries,assets,api_keys,policies,roles,audits")
                .OldAnnotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .OldAnnotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .OldAnnotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .OldAnnotation("Npgsql:Enum:logical_operator", "and,or")
                .OldAnnotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .OldAnnotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,activate,deactivate,upload,download,manage_schema")
                .OldAnnotation("Npgsql:Enum:permission_effect", "allow,deny")
                .OldAnnotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .OldAnnotation("Npgsql:Enum:user_status", "active,inactive")
                .OldAnnotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array");

            migrationBuilder.CreateIndex(
                name: "IX_users_Name",
                table: "users",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_Name",
                table: "users");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,resource_api_key_user_id,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .Annotation("Npgsql:Enum:base_resource", "users,collections,entries,assets,api_keys,policies,roles,audits")
                .Annotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .Annotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .Annotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .Annotation("Npgsql:Enum:logical_operator", "and,or")
                .Annotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .Annotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,activate,deactivate,upload,download,manage_schema")
                .Annotation("Npgsql:Enum:permission_effect", "allow,deny")
                .Annotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .Annotation("Npgsql:Enum:user_status", "active,inactive")
                .Annotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array")
                .OldAnnotation("Npgsql:Enum:attribute_path", "subject_id,subject_role,subject_status,subject_created_at,resource_collection_id,resource_collection_slug,resource_collection_created_by,resource_collection_is_localized,resource_entry_id,resource_entry_status,resource_entry_created_by,resource_entry_collection_id,resource_entry_locale,resource_entry_published_at,resource_asset_id,resource_asset_uploaded_by,resource_asset_mime_type,resource_asset_file_size,resource_user_id,resource_user_status,resource_api_key_user_id,resource_audit_user_id,environment_current_time,environment_ip_address,environment_user_agent,action_type")
                .OldAnnotation("Npgsql:Enum:base_resource", "users,collections,entries,assets,api_keys,policies,roles,audits,wildcard")
                .OldAnnotation("Npgsql:Enum:entry_status", "draft,published,archived,deleted")
                .OldAnnotation("Npgsql:Enum:field_data_type", "text,boolean,number,date_time,relation,asset,object")
                .OldAnnotation("Npgsql:Enum:locale", "en,es,fr,de,it,pt,ja,ko,zh,ar,ru")
                .OldAnnotation("Npgsql:Enum:logical_operator", "and,or")
                .OldAnnotation("Npgsql:Enum:operator_type", "eq,ne,in,not_in,gt,gte,lt,lte,contains,starts_with,ends_with,is_null,is_not_null,regex,eq_context_ref")
                .OldAnnotation("Npgsql:Enum:permission_action", "create,read,update,delete,publish,unpublish,schedule,archive,restore,activate,deactivate,upload,download,manage_schema,wildcard")
                .OldAnnotation("Npgsql:Enum:permission_effect", "allow,deny")
                .OldAnnotation("Npgsql:Enum:scheduled_action", "publish,unpublish,archive,restore,delete")
                .OldAnnotation("Npgsql:Enum:user_status", "active,inactive,banned")
                .OldAnnotation("Npgsql:Enum:value_type", "string,number,boolean,uuid,datetime,array");
        }
    }
}
