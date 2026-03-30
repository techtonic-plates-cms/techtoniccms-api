using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("user_policies")]
[Index(nameof(UserId), nameof(PolicyId), IsUnique = true)]
[Index(nameof(ExpiresAt))]
public class UserPolicy
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required Guid UserId { get; set; }
    public required Guid PolicyId { get; set; }

    [ForeignKey(nameof(AssignedByUser))]
    public required Guid AssignedBy { get; set; }

    public required DateTime AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    [StringLength(512)]
    public string? Reason { get; set; }

    public User User { get; set; } = null!;
    public AbacPolicy Policy { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
}
