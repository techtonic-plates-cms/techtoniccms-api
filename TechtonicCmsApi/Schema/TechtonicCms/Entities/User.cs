using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    [StringLength(255)]
    public required string Name { get; set; } = null!;

    [StringLength(255)]
    public required string PasswordHash { get; set; } = null!;

    public required DateTime CreationTime { get; set; }
    public required DateTime LastLoginTime { get; set; }
    public required DateTime LastEditTime { get; set; }
    public required UserStatus Status { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserPolicy> AssignedPolicies { get; set; } = new List<UserPolicy>();
    public ICollection<UserPolicy> PoliciesAssignedBy { get; set; } = new List<UserPolicy>();
    public ICollection<ResourceOwnership> OwnedResources { get; set; } = new List<ResourceOwnership>();
    public ICollection<ResourceOwnership> ResourcesAssignedBy { get; set; } = new List<ResourceOwnership>();
    public ICollection<AbacPolicy> CreatedPolicies { get; set; } = new List<AbacPolicy>();
    public ICollection<RolePolicy> RolePoliciesAssignedBy { get; set; } = new List<RolePolicy>();
    public ICollection<Collection> CreatedCollections { get; set; } = new List<Collection>();
    public ICollection<Field> CreatedFields { get; set; } = new List<Field>();
    public ICollection<Entry> CreatedEntries { get; set; } = new List<Entry>();
    public ICollection<Asset> UploadedAssets { get; set; } = new List<Asset>();
    public ICollection<AbacEvaluationCache> EvaluationCaches { get; set; } = new List<AbacEvaluationCache>();
    public ICollection<AbacAudit> AuditLogs { get; set; } = new List<AbacAudit>();
}
