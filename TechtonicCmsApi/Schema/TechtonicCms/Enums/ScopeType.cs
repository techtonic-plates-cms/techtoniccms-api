using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

[EnumType]
public enum ScopeType
{
    Global,
    CollectionSpecific,
    EntrySpecific,
    FieldSpecific
}
