using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("user_roles")]
[Index(nameof(UserId), nameof(RoleId), IsUnique = true)]
public class UserRole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required Guid UserId { get; set; }
    public required Guid RoleId { get; set; }
    public required DateTime AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
