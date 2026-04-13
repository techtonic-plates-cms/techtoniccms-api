using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

[EnumType]
public enum ValueType
{
    String,
    Number,
    Boolean,
    Uuid,
    Datetime,
    Array
}
