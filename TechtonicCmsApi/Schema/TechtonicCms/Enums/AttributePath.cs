using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

[EnumType]
public enum AttributePath
{
    SubjectId,
    SubjectRole,
    SubjectStatus,
    SubjectCreatedAt,
    ResourceCollectionId,
    ResourceCollectionSlug,
    ResourceCollectionCreatedBy,
    ResourceCollectionIsLocalized,
    ResourceEntryId,
    ResourceEntryStatus,
    ResourceEntryCreatedBy,
    ResourceEntryCollectionId,
    ResourceEntryLocale,
    ResourceEntryPublishedAt,
    ResourceFieldId,
    ResourceFieldName,
    ResourceFieldDataType,
    ResourceFieldSensitivityLevel,
    ResourceFieldIsPii,
    ResourceFieldIsPublic,
    ResourceFieldCollectionId,
    ResourceAssetId,
    ResourceAssetUploadedBy,
    ResourceAssetMimeType,
    ResourceAssetFileSize,
    EnvironmentCurrentTime,
    EnvironmentIpAddress,
    EnvironmentUserAgent,
    ActionType
}
