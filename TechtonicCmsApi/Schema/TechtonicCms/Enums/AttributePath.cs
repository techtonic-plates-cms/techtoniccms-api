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
    ResourceAssetId,
    ResourceAssetUploadedBy,
    ResourceAssetMimeType,
    ResourceAssetFileSize,
    ResourceUserId,
    ResourceUserStatus,
    EnvironmentCurrentTime,
    EnvironmentIpAddress,
    EnvironmentUserAgent,
    ActionType
}
