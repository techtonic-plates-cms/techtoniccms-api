using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

[EnumType]
public enum PermissionAction
{
    Create,
    Read,
    Update,
    Delete,
    Publish,
    Unpublish,
    Schedule,
    Archive,
    Restore,
    Draft,
    Ban,
    Unban,
    Activate,
    Deactivate,
    Upload,
    Download,
    Transform,
    ConfigureFields,
    ManageSchema
}
