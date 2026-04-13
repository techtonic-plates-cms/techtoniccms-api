using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

[EnumType]
public enum FieldDataType
{
    Text,
    Boolean,
    Number,
    DateTime,
    Relation,
    Asset,
    Object
}
