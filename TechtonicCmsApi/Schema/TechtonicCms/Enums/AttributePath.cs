using HotChocolate.Types;

namespace TechtonicCmsApi.Schema.TechtonicCms.Enums;

/// <summary>
/// Represents the various attributes that can be used in ABAC policies for conditions and constraints.
/// When creating a rule, you can specify one of these attributes as one of the fields that will be used to be evaluated againt one of these same attributes.
/// </summary>
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
    ResourceApiKeyUserId,
    EnvironmentCurrentTime,
    EnvironmentIpAddress,
    EnvironmentUserAgent,
    ActionType
}
