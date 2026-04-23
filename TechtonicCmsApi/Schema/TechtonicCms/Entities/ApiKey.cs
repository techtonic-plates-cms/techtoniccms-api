using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("api_keys")]
[Index(nameof(KeyHash), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(IsActive))]
public class ApiKey
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    [ForeignKey(nameof(User))]
    public required Guid UserId { get; set; }

    [StringLength(255)]
    public required string Name { get; set; } = null!;

    [StringLength(64)]
    public required string KeyHash { get; set; } = null!;

    [StringLength(16)]
    public required string KeyPrefix { get; set; } = null!;

    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public required bool IsActive { get; set; }
    public DateTime? LastUsedAt { get; set; }

    [GraphQLIgnore]
    public User User { get; set; } = null!;
}
