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
    Ban,
    Unban,
    Activate,
    Deactivate,
    Upload,
    Download,
    ManageSchema
}
