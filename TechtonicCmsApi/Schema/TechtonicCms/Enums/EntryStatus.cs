using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

[EnumType]
public enum EntryStatus
{
    Draft,
    Published,
    Archived,
    Deleted
}
