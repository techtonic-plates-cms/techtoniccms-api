using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("abac_policy_rules")]
[Index(nameof(PolicyId), nameof(Order))]
[Index(nameof(AttributePath))]
[Index(nameof(PolicyId), nameof(IsActive))]
public class AbacPolicyRule
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required Guid PolicyId { get; set; }
    public required AttributePath AttributePath { get; set; }
    public required OperatorType Operator { get; set; }

    [Column(TypeName = "text")]
    public string? ExpectedStringValue { get; set; }

    [Column(TypeName = "double precision")]
    public double? ExpectedNumberValue { get; set; }

    [Column(TypeName = "boolean")]
    public bool? ExpectedBooleanValue { get; set; }

    [Column(TypeName = "uuid")]
    public Guid? ExpectedUuidValue { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime? ExpectedDateTimeValue { get; set; }

    [Column(TypeName = "text[]")]
    public string[]? ExpectedArrayValue { get; set; }

    public AttributePath? ContextReferencePath { get; set; }

    public Enums.ValueType ValueType { get; set; }

    [StringLength(512)]
    public string? Description { get; set; }

    public required bool IsActive { get; set; }
    public required int Order { get; set; }
    public required DateTime CreatedAt { get; set; }

    public AbacPolicy Policy { get; set; } = null!;
}
