using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("abac_audit")]
[Index(nameof(UserId), nameof(Timestamp))]
[Index(nameof(ResourceType), nameof(ResourceId), nameof(Timestamp))]
[Index(nameof(Decision), nameof(Timestamp))]
[Index(nameof(Timestamp))]
[Index(nameof(EvaluationTimeMs))]
public class AbacAudit
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required Guid UserId { get; set; }
    public required PermissionAction RequestedAction { get; set; }
    public required BaseResource ResourceType { get; set; }
    public required Guid ResourceId { get; set; }

    public required PermissionEffect Decision { get; set; }
    public required Guid[] EvaluatedPolicyIds { get; set; } = [];
    public required Guid[] MatchingPolicyIds { get; set; } = [];

    [Column(TypeName = "text")]
    public required string DecisionReason { get; set; } = null!;

    public required int EvaluationTimeMs { get; set; }

    [Column(TypeName = "text")]
    public required string RequestContext { get; set; } = null!;

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    [StringLength(255)]
    public string? SessionId { get; set; }

    public required DateTime Timestamp { get; set; }

    public User User { get; set; } = null!;
}
