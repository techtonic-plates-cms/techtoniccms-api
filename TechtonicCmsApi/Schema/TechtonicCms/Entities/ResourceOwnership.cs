using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("resource_ownerships")]
[Index(nameof(ResourceType), nameof(ResourceId), nameof(OwnerId))]
[Index(nameof(OwnerId), nameof(ResourceType))]
[Index(nameof(ExpiresAt))]
public class ResourceOwnership
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required BaseResource ResourceType { get; set; }
    public required Guid ResourceId { get; set; }
    public required Guid OwnerId { get; set; }

    [StringLength(50)]
    public required string OwnershipType { get; set; } = null!;

    [ForeignKey(nameof(AssignedByUser))]
    public Guid? AssignedBy { get; set; }

    public required DateTime AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public User Owner { get; set; } = null!;
    public User? AssignedByUser { get; set; }
}
