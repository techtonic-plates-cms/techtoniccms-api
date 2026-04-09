using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entries")]
public class Entry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    [ForeignKey(nameof(CreatedByUser))]
    public required Guid CreatedBy { get; set; }

    public required Guid CollectionId { get; set; }

    [StringLength(255)]
    public required string Name { get; set; } = null!;

    [StringLength(255)]
    public string? Slug { get; set; }

    public required EntryStatus Status { get; set; }
    public required Locale Locale { get; set; }
    public required Locale DefaultLocale { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public Collection Collection { get; set; } = null!;

    public required JsonDocument Data { get; set; }

    public void Dispose()
    {
        Data.Dispose();
    }

    /// <summary>
    /// Relations where this entry is the source (this entry references another entry).
    /// </summary>
    public ICollection<EntryRelation> FromRelations { get; set; } = new List<EntryRelation>();

    /// <summary>
    /// Relations where this entry is the target (another entry references this entry).
    /// </summary>
    public ICollection<EntryRelation> ToRelations { get; set; } = new List<EntryRelation>();
}
