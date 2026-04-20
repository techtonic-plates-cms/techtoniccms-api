using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

[EnumType]
public enum ScheduledAction
{
    Publish,
    Unpublish,
    Archive,
    Restore,
    Delete
}
