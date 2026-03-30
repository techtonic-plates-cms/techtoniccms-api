using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("abac_policies")]
[Index(nameof(Name), IsUnique = true)]
[Index(nameof(ResourceType), nameof(ActionType))]
[Index(nameof(Priority))]
[Index(nameof(IsActive))]
public class AbacPolicy
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    [StringLength(255)]
    public required string Name { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? Description { get; set; }

    public required PermissionEffect Effect { get; set; }
    public required int Priority { get; set; }
    public required bool IsActive { get; set; }
    public required BaseResource ResourceType { get; set; }
    public required PermissionAction ActionType { get; set; }
    public required LogicalOperator RuleConnector { get; set; }

    [ForeignKey(nameof(CreatedByUser))]
    public required Guid CreatedBy { get; set; }

    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
    public DateTime? LastEvaluatedAt { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<AbacPolicyRule> Rules { get; set; } = new List<AbacPolicyRule>();
    public ICollection<RolePolicy> RolePolicies { get; set; } = new List<RolePolicy>();
    public ICollection<UserPolicy> UserPolicies { get; set; } = new List<UserPolicy>();
}
