using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using HotChocolate;

using TechtonicCmsApi.Schema.TechtonicCms;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

using ValueType = TechtonicCmsApi.Schema.TechtonicCms.Enums.ValueType;

namespace TechtonicCmsApi.Services;

public class AbacService
{
    private readonly TechtonicCmsDbContext _db;

    public AbacService(TechtonicCmsDbContext db)
    {
        _db = db;
    }

    public async Task<bool> CheckPermissionAsync(
        Guid userId,
        BaseResource resourceType,
        PermissionAction action,
        Dictionary<string, object?>? resourceData = null)
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
            return false;

        var policies = await _db.AbacPolicies
            .Where(p => allPolicyIds.Contains(p.Id)
                && p.ResourceType == resourceType
                && p.ActionType == action
                && p.IsActive)
            .ToListAsync();

        var denyPolicies = policies
            .Where(p => p.Effect == PermissionEffect.Deny)
            .OrderByDescending(p => p.Priority)
            .ToList();

        var allowPolicies = policies
            .Where(p => p.Effect == PermissionEffect.Allow)
            .OrderByDescending(p => p.Priority)
            .ToList();

        var context = BuildContext(userId, resourceData);

        foreach (var policy in denyPolicies)
        {
            if (await EvaluatePolicyRulesAsync(policy, context))
                return false;
        }

        foreach (var policy in allowPolicies)
        {
            if (await EvaluatePolicyRulesAsync(policy, context))
                return true;
        }

        return false;
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
        var expected = rule.ExpectedValue;

        return rule.Operator switch
        {
            OperatorType.Eq => EvaluateEquals(actualStr, expected, rule.ValueType),
            OperatorType.Ne => !EvaluateEquals(actualStr, expected, rule.ValueType),
            OperatorType.In => EvaluateIn(actualStr, expected),
            OperatorType.NotIn => !EvaluateIn(actualStr, expected),
            OperatorType.Gt => EvaluateComparison(actualStr, expected, rule.ValueType) > 0,
            OperatorType.Gte => EvaluateComparison(actualStr, expected, rule.ValueType) >= 0,
            OperatorType.Lt => EvaluateComparison(actualStr, expected, rule.ValueType) < 0,
            OperatorType.Lte => EvaluateComparison(actualStr, expected, rule.ValueType) <= 0,
            OperatorType.Contains => actualStr.Contains(expected, StringComparison.OrdinalIgnoreCase),
            OperatorType.StartsWith => actualStr.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            OperatorType.EndsWith => actualStr.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            OperatorType.Regex => Regex.IsMatch(actualStr, expected, RegexOptions.None, TimeSpan.FromSeconds(1)),
            _ => false
        };
    }

    private static Dictionary<string, object?> BuildContext(
        Guid userId,
        Dictionary<string, object?>? resourceData)
    {
        var context = new Dictionary<string, object?>
        {
            ["SubjectId"] = userId.ToString()
        };

        if (resourceData != null)
        {
            foreach (var kvp in resourceData)
                context[kvp.Key] = kvp.Value;
        }

        return context;
    }

    private static bool EvaluateEquals(string actual, string expected, ValueType valueType)
    {
        return valueType switch
        {
            ValueType.Number => double.TryParse(actual, CultureInfo.InvariantCulture, out var aNum)
                && double.TryParse(expected, CultureInfo.InvariantCulture, out var eNum)
                && Math.Abs(aNum - eNum) < double.Epsilon,
            ValueType.Boolean => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ValueType.Uuid => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ValueType.Datetime => DateTime.TryParse(actual, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var aDt)
                && DateTime.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var eDt)
                && aDt == eDt,
            _ => string.Equals(actual, expected, StringComparison.Ordinal)
        };
    }

    private static bool EvaluateIn(string actual, string expectedArray)
    {
        try
        {
            var items = JsonSerializer.Deserialize<string[]>(expectedArray);
            return items != null && items.Contains(actual, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int EvaluateComparison(string actual, string expected, ValueType valueType)
    {
        if (valueType == ValueType.Number)
        {
            if (double.TryParse(actual, CultureInfo.InvariantCulture, out var aNum)
                && double.TryParse(expected, CultureInfo.InvariantCulture, out var eNum))
                return aNum.CompareTo(eNum);
            return 0;
        }

        if (valueType == ValueType.Datetime)
        {
            if (DateTime.TryParse(actual, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var aDt)
                && DateTime.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var eDt))
                return DateTime.Compare(aDt, eDt);
            return 0;
        }

        return string.Compare(actual, expected, StringComparison.Ordinal);
    }
}
