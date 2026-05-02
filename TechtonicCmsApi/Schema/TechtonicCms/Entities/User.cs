using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotChocolate;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("users")]
[Index(nameof(Name), IsUnique = true)]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    [StringLength(255)]
    [IsProjected(true)]
    public required string Name { get; set; } = null!;

    [StringLength(255)]
    [GraphQLIgnore]
    public required string PasswordHash { get; set; } = null!;

    public required DateTime CreationTime { get; set; }
    public required DateTime LastLoginTime { get; set; }
    public required DateTime LastEditTime { get; set; }
    public required UserStatus Status { get; set; }

    [GraphQLIgnore]
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    [GraphQLIgnore]
    public ICollection<UserPolicy> AssignedPolicies { get; set; } = new List<UserPolicy>();
    [GraphQLIgnore]
    public ICollection<UserPolicy> PoliciesAssignedBy { get; set; } = new List<UserPolicy>();
    [GraphQLIgnore]
    public ICollection<ResourceOwnership> OwnedResources { get; set; } = new List<ResourceOwnership>();
    [GraphQLIgnore]
    public ICollection<ResourceOwnership> ResourcesAssignedBy { get; set; } = new List<ResourceOwnership>();
    [GraphQLIgnore]
    public ICollection<AbacPolicy> CreatedPolicies { get; set; } = new List<AbacPolicy>();
    [GraphQLIgnore]
    public ICollection<RolePolicy> RolePoliciesAssignedBy { get; set; } = new List<RolePolicy>();
    [GraphQLIgnore]
    public ICollection<Collection> CreatedCollections { get; set; } = new List<Collection>();
    [GraphQLIgnore]
    public ICollection<Field> CreatedFields { get; set; } = new List<Field>();
    [GraphQLIgnore]
    public ICollection<Entry> CreatedEntries { get; set; } = new List<Entry>();
    [GraphQLIgnore]
    public ICollection<Asset> UploadedAssets { get; set; } = new List<Asset>();
    [GraphQLIgnore]
    public ICollection<AbacEvaluationCache> EvaluationCaches { get; set; } = new List<AbacEvaluationCache>();
    [GraphQLIgnore]
    public ICollection<AbacAudit> AuditLogs { get; set; } = new List<AbacAudit>();
    [GraphQLIgnore]
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}
