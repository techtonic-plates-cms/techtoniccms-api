using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("abac_evaluation_cache")]
[Index(nameof(UserId), nameof(ResourceType), nameof(ResourceId), nameof(ActionType))]
[Index(nameof(UserId), nameof(FieldId), nameof(ActionType))]
[Index(nameof(ExpiresAt))]
[Index(nameof(ContextChecksum))]
[Index(nameof(Decision))]
public class AbacEvaluationCache
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required Guid UserId { get; set; }
    public required BaseResource ResourceType { get; set; }
    public required Guid ResourceId { get; set; }
    public required PermissionAction ActionType { get; set; }
    public Guid? FieldId { get; set; }
    public required PermissionEffect Decision { get; set; }
    public required Guid[] MatchingPolicyIds { get; set; } = [];
    public required int EvaluationTimeMs { get; set; }
    public required DateTime ComputedAt { get; set; }
    public required DateTime ExpiresAt { get; set; }

    [StringLength(64)]
    public required string ContextChecksum { get; set; } = null!;

    [Column(TypeName = "text")]
    public required string PolicyVersions { get; set; } = null!;

    public User User { get; set; } = null!;
    public Field? Field { get; set; }
}
