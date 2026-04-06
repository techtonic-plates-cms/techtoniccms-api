using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Data;

public record CmsFieldMapping(
    string PropertyName,
    string JsonKey,
    Type ClrType,
    bool IsBaseField,
    FieldDataType? DataType
);
