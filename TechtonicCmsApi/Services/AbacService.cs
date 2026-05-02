using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using HotChocolate;

using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

using ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType;
using TechtonicCmsApi.Contexts;

namespace TechtonicCmsApi.Services;

public class AbacService
{
    private readonly TechtonicCmsDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Cache TTL for positive (Allow) evaluations.</summary>
    private static readonly TimeSpan CacheTtlAllow = TimeSpan.FromMinutes(5);
    /// <summary>Cache TTL for negative (Deny) evaluations — shorter to allow quick recovery.</summary>
    private static readonly TimeSpan CacheTtlDeny = TimeSpan.FromMinutes(2);

    public AbacService(TechtonicCmsDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> CheckPermissionAsync(
        Guid userId,
        BaseResource resourceType,
        PermissionAction action,
        Dictionary<string, object?>? resourceData = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // ── Try cache lookup first ────────────────────────────────
        var resourceId = ResolveResourceId(resourceType, resourceData);
        var cached = await LookupCacheAsync(userId, resourceType, resourceId, action);
        if (cached != null)
            return cached.Value;

        // ── Full evaluation ───────────────────────────────────────
        var policies = await GetApplicablePoliciesAsync(userId, resourceType, action);

        if (policies.Count == 0)
        {
            stopwatch.Stop();
            await WriteAuditAsync(userId, resourceType, resourceId, action,
                PermissionEffect.Deny, [], [], "No policies assigned to user",
                stopwatch.ElapsedMilliseconds, resourceData);
            return false;
        }

        var denyPolicies = policies
            .Where(p => p.Effect == PermissionEffect.Deny)
            .OrderByDescending(p => p.Priority)
            .ToList();

        var allowPolicies = policies
            .Where(p => p.Effect == PermissionEffect.Allow)
            .OrderByDescending(p => p.Priority)
            .ToList();

        var context = await BuildContextAsync(userId, action, resourceData);
        var evaluatedPolicyIds = policies.Select(p => p.Id).ToArray();
        var matchingPolicyIds = new List<Guid>();

        foreach (var policy in denyPolicies)
        {
            if (await EvaluatePolicyRulesAsync(policy, context))
            {
                matchingPolicyIds.Add(policy.Id);
                stopwatch.Stop();

                var decision = PermissionEffect.Deny;
                await WriteAuditAsync(userId, resourceType, resourceId, action,
                    decision, evaluatedPolicyIds, matchingPolicyIds.ToArray(),
                    $"Denied by policy '{policy.Name}' (priority {policy.Priority})",
                    stopwatch.ElapsedMilliseconds, resourceData);
                await WriteCacheAsync(userId, resourceType, resourceId, action,
                    decision, matchingPolicyIds.ToArray(), stopwatch.ElapsedMilliseconds,
                    policies);

                return false;
            }
        }

        foreach (var policy in allowPolicies)
        {
            if (await EvaluatePolicyRulesAsync(policy, context))
            {
                matchingPolicyIds.Add(policy.Id);
                stopwatch.Stop();

                var decision = PermissionEffect.Allow;
                await WriteAuditAsync(userId, resourceType, resourceId, action,
                    decision, evaluatedPolicyIds, matchingPolicyIds.ToArray(),
                    $"Allowed by policy '{policy.Name}' (priority {policy.Priority})",
                    stopwatch.ElapsedMilliseconds, resourceData);
                await WriteCacheAsync(userId, resourceType, resourceId, action,
                    decision, matchingPolicyIds.ToArray(), stopwatch.ElapsedMilliseconds,
                    policies);

                return true;
            }
        }

        stopwatch.Stop();
        await WriteAuditAsync(userId, resourceType, resourceId, action,
            PermissionEffect.Deny, evaluatedPolicyIds, [],
            "No matching allow policy found", stopwatch.ElapsedMilliseconds, resourceData);

        return false;
    }

    private async Task<List<AbacPolicy>> GetApplicablePoliciesAsync(
        Guid userId,
        BaseResource resourceType,
        PermissionAction action)
    {
        var now = DateTime.UtcNow;

        var roleIds = await _db.UserRoles
            .Where(ur => ur.UserId == userId && (ur.ExpiresAt == null || ur.ExpiresAt > now))
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var policyIdsFromRoles = await _db.RolePolicies
            .Where(rp => roleIds.Contains(rp.RoleId) && (rp.ExpiresAt == null || rp.ExpiresAt > now))
            .Select(rp => rp.PolicyId)
            .ToListAsync();

        var policyIdsDirect = await _db.UserPolicies
            .Where(up => up.UserId == userId && (up.ExpiresAt == null || up.ExpiresAt > now))
            .Select(up => up.PolicyId)
            .ToListAsync();

        var allPolicyIds = policyIdsFromRoles.Concat(policyIdsDirect).ToHashSet();

        if (allPolicyIds.Count == 0)
            return new List<AbacPolicy>();

        return await _db.AbacPolicies
            .Where(p => allPolicyIds.Contains(p.Id)
                && p.IsActive
                && (p.ResourceType == resourceType || p.ResourceType == BaseResource.Wildcard)
                && (p.ActionType == action || p.ActionType == PermissionAction.Wildcard))
            .ToListAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // Audit & Cache Helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a deterministic resource ID from resource type and data.
    /// For field-level checks, uses the field's collection+field composite.
    /// </summary>
    private static Guid ResolveResourceId(
        BaseResource resourceType,
        Dictionary<string, object?>? resourceData)
    {
        // If explicit ResourceFieldId present, derive a deterministic GUID from it
        if (resourceData != null
            && resourceData.TryGetValue("ResourceFieldId", out var fieldIdStr)
            && fieldIdStr is string fidStr
            && Guid.TryParse(fidStr, out var fid))
        {
            return fid;
        }

        // Fallback: deterministic GUID per resource type
        return resourceType switch
        {
            BaseResource.Users        => new Guid("1876dbf2-726d-40a2-ba7e-0e447e61d60e"),
            BaseResource.Collections  => new Guid("44f93036-ba8f-48d2-b6b3-8fc61bf7e7dc"),
            BaseResource.Entries      => new Guid("4709788a-6880-4a2d-9f52-0824f352495a"),
            BaseResource.Assets       => new Guid("1216fcdd-bcef-40f2-ba76-fb90390bb741"),
            BaseResource.ApiKeys      => new Guid("567057f3-3c8d-43b2-863a-07f1ae0a6216"),
            BaseResource.Policies     => new Guid("519a35d4-0f1f-49e6-999c-ebb7d3410467"),
            BaseResource.Roles        => new Guid("8cf36b39-3dff-42a7-bfdc-f0c53772bc0f"),
            BaseResource.Audits       => new Guid("6e145e4c-54f6-45ff-bd18-16260997f34e"),
            _ => Guid.Empty
        };
    }

    /// <summary>
    /// Looks up a cached evaluation. Returns null if no valid cache entry exists.
    /// </summary>
    private async Task<bool?> LookupCacheAsync(
        Guid userId,
        BaseResource resourceType,
        Guid resourceId,
        PermissionAction action)
    {
        IQueryable<AbacEvaluationCache> query = _db.AbacEvaluationCaches
            .Where(c => c.UserId == userId
                && c.ResourceType == resourceType
                && c.ResourceId == resourceId
                && c.ActionType == action
                && c.ExpiresAt > DateTime.UtcNow);

        var cached = await query.FirstOrDefaultAsync();
        if (cached is null)
            return null;

        var currentPolicies = await GetApplicablePoliciesAsync(userId, resourceType, action);
        var currentPolicyVersions = string.Join(",",
            currentPolicies.Select(p => $"{p.Id}:{p.UpdatedAt:O}"));

        if (currentPolicyVersions != cached.PolicyVersions)
            return null;

        return cached.Decision == PermissionEffect.Allow ? true
             : cached.Decision == PermissionEffect.Deny ? false
             : null;
    }

    /// <summary>
    /// Writes an audit record for an ABAC evaluation.
    /// Fire-and-forget style — does not propagate errors to the caller.
    /// </summary>
    private async Task WriteAuditAsync(
        Guid userId,
        BaseResource resourceType,
        Guid resourceId,
        PermissionAction action,
        PermissionEffect decision,
        Guid[] evaluatedPolicyIds,
        Guid[] matchingPolicyIds,
        string decisionReason,
        long evaluationTimeMs,
        Dictionary<string, object?>? resourceData)
    {
        try
        {
            var audit = new AbacAudit
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                RequestedAction = action,
                Decision = decision,
                EvaluatedPolicyIds = evaluatedPolicyIds,
                MatchingPolicyIds = matchingPolicyIds,
                DecisionReason = decisionReason,
                EvaluationTimeMs = (int)evaluationTimeMs,
                RequestContext = JsonSerializer.Serialize(resourceData ?? new()),
                Timestamp = DateTime.UtcNow
            };

            _db.AbacAudits.Add(audit);
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Audit failures must never block permission checks
        }
    }

    /// <summary>
    /// Writes or updates a cache entry for an ABAC evaluation.
    /// </summary>
    private async Task WriteCacheAsync(
        Guid userId,
        BaseResource resourceType,
        Guid resourceId,
        PermissionAction action,
        PermissionEffect decision,
        Guid[] matchingPolicyIds,
        long evaluationTimeMs,
        List<AbacPolicy> evaluatedPolicies)
    {
        try
        {
            // Invalidate any existing cache entry for this exact key
            IQueryable<AbacEvaluationCache> existingQuery = _db.AbacEvaluationCaches
                .Where(c => c.UserId == userId
                    && c.ResourceType == resourceType
                    && c.ResourceId == resourceId
                    && c.ActionType == action);

           

            var existing = await existingQuery.FirstOrDefaultAsync();
            if (existing != null)
                _db.AbacEvaluationCaches.Remove(existing);

            var ttl = decision == PermissionEffect.Allow ? CacheTtlAllow : CacheTtlDeny;
            var now = DateTime.UtcNow;

            var contextChecksum = ComputeContextChecksum(
                userId, resourceType, resourceId, action);
            var policyVersions = string.Join(",",
                evaluatedPolicies.Select(p => $"{p.Id}:{p.UpdatedAt:O}"));

            _db.AbacEvaluationCaches.Add(new AbacEvaluationCache
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                ActionType = action,
                Decision = decision,
                MatchingPolicyIds = matchingPolicyIds,
                EvaluationTimeMs = (int)evaluationTimeMs,
                ComputedAt = now,
                ExpiresAt = now + ttl,
                ContextChecksum = contextChecksum,
                PolicyVersions = policyVersions
            });

            await _db.SaveChangesAsync();
        }
        catch
        {
            // Cache failures must never block permission checks
        }
    }

    /// <summary>
    /// Computes a SHA256 checksum of the evaluation context for cache invalidation.
    /// </summary>
    private static string ComputeContextChecksum(
        Guid userId,
        BaseResource resourceType,
        Guid resourceId,
        PermissionAction action)
    {
        var raw = $"{userId}:{resourceType}:{resourceId}:{action}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
    
    public async Task RequirePermissionAsync(
        Guid userId,
        BaseResource resourceType,
        PermissionAction action,
        Dictionary<string, object?>? resourceData = null)
    {
        var allowed = await CheckPermissionAsync(userId, resourceType, action, resourceData);
        if (!allowed)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Permission denied: {action} on {resourceType}")
                    .SetCode("FORBIDDEN")
                    .Build());
        }
    }

    public Dictionary<string, object?> ExtractResourceData(object entity, BaseResource resource)
    {
        return (entity, resource) switch
        {
            (Schema.TechtonicCms.Entities.ApiKey apiKey, BaseResource.ApiKeys) => new Dictionary<string, object?>
            {
                ["ResourceFieldId"] = apiKey.Id.ToString(),
                ["ResourceApiKeyUserId"] = apiKey.UserId.ToString(),
            },
            (Schema.TechtonicCms.Entities.User user, BaseResource.Users) => new Dictionary<string, object?>
            {
                ["ResourceFieldId"] = user.Id.ToString(),
                ["ResourceUserId"] = user.Id.ToString(),
                ["ResourceUserStatus"] = user.Status.ToString(),
            },
            (Schema.TechtonicCms.Entities.Asset asset, BaseResource.Assets) => new Dictionary<string, object?>
            {
                ["ResourceFieldId"] = asset.Id.ToString(),
                ["ResourceAssetUploadedBy"] = asset.UploadedBy.ToString(),
                ["ResourceAssetMimeType"] = asset.MimeType,
                ["ResourceAssetFileSize"] = asset.FileSize,
            },
            (Schema.TechtonicCms.Entities.Collection collection, BaseResource.Collections) => new Dictionary<string, object?>
            {
                ["ResourceFieldId"] = collection.Id.ToString(),
                ["ResourceCollectionId"] = collection.Id.ToString(),
                ["ResourceCollectionSlug"] = collection.Slug,
                ["ResourceCollectionCreatedBy"] = collection.CreatedBy.ToString(),
                ["ResourceCollectionIsLocalized"] = collection.IsLocalized,
            },
            (Schema.TechtonicCms.Entities.AbacAudit audit, BaseResource.Audits) => new Dictionary<string, object?>
            {
                ["ResourceFieldId"] = audit.Id.ToString(),
                ["ResourceAuditUserId"] = audit.UserId?.ToString(),
            },
            (Schema.TechtonicCms.Entities.Entry entry, BaseResource.Entries) => new Dictionary<string, object?>
            {
                ["ResourceFieldId"] = entry.Id.ToString(),
                ["ResourceEntryId"] = entry.Id.ToString(),
                ["ResourceEntryStatus"] = entry.Status.ToString(),
                ["ResourceEntryCreatedBy"] = entry.CreatedBy.ToString(),
                ["ResourceEntryCollectionId"] = entry.CollectionId.ToString(),
                ["ResourceEntryLocale"] = entry.Locale.ToString(),
                ["ResourceEntryPublishedAt"] = entry.PublishedAt?.ToString("O"),
            },
            _ => new Dictionary<string, object?>()
        };
    }

    /// <summary>
    /// Returns the name of the property that represents ownership for a given resource type.
    /// </summary>
    public static string? GetOwnershipPropertyName(BaseResource resource) => resource switch
    {
        BaseResource.ApiKeys => "UserId",
        BaseResource.Assets => "UploadedBy",
        BaseResource.Collections => "CreatedBy",
        BaseResource.Users => "Id",
        BaseResource.Entries => "CreatedBy",
        _ => null
    };

    /// <summary>
    /// Probes whether the user is restricted to accessing only their own resources.
    /// Returns true if the user would be denied access to a resource owned by someone else.
    /// </summary>
    public async Task<bool> IsRestrictedToOwnResourcesAsync(Guid userId, BaseResource resource, PermissionAction action)
    {
        var probeContext = CreateProbeContext(resource, Guid.NewGuid());
        if (probeContext.Count == 0)
            return false;

        var allowed = await CheckPermissionAsync(userId, resource, action, probeContext);
        return !allowed;
    }

    private static Dictionary<string, object?> CreateProbeContext(BaseResource resource, Guid fakeOwnerId)
    {
        var fakeOwnerStr = fakeOwnerId.ToString();
        return resource switch
        {
            BaseResource.ApiKeys => new Dictionary<string, object?> { ["ResourceApiKeyUserId"] = fakeOwnerStr },
            BaseResource.Assets => new Dictionary<string, object?> { ["ResourceAssetUploadedBy"] = fakeOwnerStr },
            BaseResource.Collections => new Dictionary<string, object?>
            {
                ["ResourceCollectionId"] = fakeOwnerStr,
                ["ResourceCollectionSlug"] = "probe",
                ["ResourceCollectionCreatedBy"] = fakeOwnerStr,
                ["ResourceCollectionIsLocalized"] = false,
            },
            BaseResource.Users => new Dictionary<string, object?>
            {
                ["ResourceUserId"] = fakeOwnerStr,
                ["ResourceUserStatus"] = "Active",
            },
            BaseResource.Entries => new Dictionary<string, object?>
            {
                ["ResourceEntryId"] = fakeOwnerStr,
                ["ResourceEntryStatus"] = "Draft",
                ["ResourceEntryCreatedBy"] = fakeOwnerStr,
                ["ResourceEntryCollectionId"] = fakeOwnerStr,
                ["ResourceEntryLocale"] = "En",
            },
            _ => new Dictionary<string, object?>()
        };
    }

    private async Task<bool> EvaluatePolicyRulesAsync(
        AbacPolicy policy,
        Dictionary<string, object?> context)
    {
        var rules = await _db.AbacPolicyRules
            .Where(r => r.PolicyId == policy.Id && r.IsActive)
            .OrderBy(r => r.Order)
            .ToListAsync();

        if (rules.Count == 0)
            return true;

        if (policy.RuleConnector == LogicalOperator.And)
            return rules.All(rule => EvaluateRule(rule, context));

        return rules.Any(rule => EvaluateRule(rule, context));
    }

    private static bool EvaluateRule(AbacPolicyRule rule, Dictionary<string, object?> context)
    {
        var attributeKey = rule.AttributePath.ToString();
        var actualValue = context.TryGetValue(attributeKey, out var val) ? val : null;

        if (rule.Operator == OperatorType.IsNull)
            return actualValue == null;

        if (rule.Operator == OperatorType.IsNotNull)
            return actualValue != null;

        if (actualValue == null)
            return false;

        var actualStr = actualValue.ToString() ?? "";

        return rule.Operator switch
        {
            OperatorType.Eq => EvaluateEquals(rule, actualValue),
            OperatorType.Ne => !EvaluateEquals(rule, actualValue),
            OperatorType.In => rule.ExpectedArrayValue?.Contains(actualStr, StringComparer.OrdinalIgnoreCase) ?? false,
            OperatorType.NotIn => !(rule.ExpectedArrayValue?.Contains(actualStr, StringComparer.OrdinalIgnoreCase) ?? false),
            OperatorType.Gt => EvaluateComparison(rule, actualValue) > 0,
            OperatorType.Gte => EvaluateComparison(rule, actualValue) >= 0,
            OperatorType.Lt => EvaluateComparison(rule, actualValue) < 0,
            OperatorType.Lte => EvaluateComparison(rule, actualValue) <= 0,
            OperatorType.Contains => actualStr.Contains(rule.ExpectedStringValue ?? "", StringComparison.OrdinalIgnoreCase),
            OperatorType.StartsWith => actualStr.StartsWith(rule.ExpectedStringValue ?? "", StringComparison.OrdinalIgnoreCase),
            OperatorType.EndsWith => actualStr.EndsWith(rule.ExpectedStringValue ?? "", StringComparison.OrdinalIgnoreCase),
            OperatorType.Regex => Regex.IsMatch(actualStr, rule.ExpectedStringValue ?? "", RegexOptions.None, TimeSpan.FromSeconds(1)),
            OperatorType.EqContextRef => rule.ContextReferencePath.HasValue
                && context.TryGetValue(rule.ContextReferencePath.Value.ToString(), out var refVal)
                && string.Equals(actualStr, refVal?.ToString() ?? "", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private async Task<Dictionary<string, object?>> BuildContextAsync(
        Guid userId,
        PermissionAction action,
        Dictionary<string, object?>? resourceData)
    {
        var context = new Dictionary<string, object?>
        {
            ["SubjectId"] = userId.ToString(),
            ["ActionType"] = action.ToString().ToUpperInvariant(),
            ["EnvironmentCurrentTime"] = DateTime.UtcNow.ToString("o")
        };

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is not null)
        {
            context["SubjectStatus"] = user.Status.ToString();
        }

        var roleNames = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && (ur.ExpiresAt == null || ur.ExpiresAt > DateTime.UtcNow))
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync();

        if (roleNames.Count > 0)
        {
            context["SubjectRole"] = string.Join(",", roleNames);
            context["SubjectCreatedAt"] = user?.CreationTime.ToString("o");
        }

        if (_httpContextAccessor.HttpContext is not null)
        {
            context["EnvironmentIpAddress"] = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            context["EnvironmentUserAgent"] = _httpContextAccessor.HttpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "";
        }

        if (resourceData != null)
        {
            foreach (var kvp in resourceData)
                context[kvp.Key] = kvp.Value;
        }

        return context;
    }

    private static bool EvaluateEquals(AbacPolicyRule rule, object? actualValue)
    {
        if (actualValue == null) return false;

        return rule.ValueType switch
        {
            ValueType.String => string.Equals(actualValue.ToString(), rule.ExpectedStringValue, StringComparison.Ordinal),
            ValueType.Number => double.TryParse(actualValue.ToString(), CultureInfo.InvariantCulture, out var aNum)
                && rule.ExpectedNumberValue.HasValue
                && Math.Abs(aNum - rule.ExpectedNumberValue.Value) < double.Epsilon,
            ValueType.Boolean => bool.TryParse(actualValue.ToString(), out var aBool)
                && rule.ExpectedBooleanValue.HasValue
                && aBool == rule.ExpectedBooleanValue.Value,
            ValueType.Uuid => Guid.TryParse(actualValue.ToString(), out var aGuid)
                && rule.ExpectedUuidValue.HasValue
                && aGuid == rule.ExpectedUuidValue.Value,
            ValueType.Datetime => DateTime.TryParse(actualValue.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var aDt)
                && rule.ExpectedDateTimeValue.HasValue
                && aDt == rule.ExpectedDateTimeValue.Value,
            _ => false
        };
    }

    private static int EvaluateComparison(AbacPolicyRule rule, object? actualValue)
    {
        if (actualValue == null) return 0;

        if (rule.ValueType == ValueType.Number && rule.ExpectedNumberValue.HasValue)
        {
            if (double.TryParse(actualValue.ToString(), CultureInfo.InvariantCulture, out var aNum))
                return aNum.CompareTo(rule.ExpectedNumberValue.Value);
            return 0;
        }

        if (rule.ValueType == ValueType.Datetime && rule.ExpectedDateTimeValue.HasValue)
        {
            if (DateTime.TryParse(actualValue.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var aDt))
                return DateTime.Compare(aDt, rule.ExpectedDateTimeValue.Value);
            return 0;
        }

        return string.Compare(actualValue.ToString(), rule.ExpectedStringValue, StringComparison.Ordinal);
    }
}
