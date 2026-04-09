namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

/// <summary>
/// Subset of <see cref="FieldDataType"/> excluding <c>Relation</c>.
/// Used in <c>SimpleFieldConfigInput</c> so the GraphQL schema cannot express
/// a Relation field through the simple config path.
/// </summary>
public enum SimpleFieldDataType
{
    Text,
    Boolean,
    Number,
    DateTime,
    Asset,
    Object
}
