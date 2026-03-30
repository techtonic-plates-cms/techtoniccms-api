using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("roles")]
[Index(nameof(Name), IsUnique = true)]
public class Role
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    [StringLength(255)]
    public required string Name { get; set; } = null!;

    [StringLength(1024)]
    public string? Description { get; set; }

    public required DateTime CreationTime { get; set; }
    public required DateTime LastEditTime { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePolicy> RolePolicies { get; set; } = new List<RolePolicy>();
}
