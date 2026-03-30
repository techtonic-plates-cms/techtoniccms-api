using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    public ICollection<EntryRelation> FromRelations { get; set; } = new List<EntryRelation>();
    public ICollection<EntryRelation> ToRelations { get; set; } = new List<EntryRelation>();
    public ICollection<EntryTypstText> TypstTexts { get; set; } = new List<EntryTypstText>();
    public ICollection<EntryText> Texts { get; set; } = new List<EntryText>();
    public ICollection<EntryBoolean> Booleans { get; set; } = new List<EntryBoolean>();
    public ICollection<EntryNumber> Numbers { get; set; } = new List<EntryNumber>();
    public ICollection<EntryDateTime> DateTimes { get; set; } = new List<EntryDateTime>();
    public ICollection<EntryRichText> RichTexts { get; set; } = new List<EntryRichText>();
    public ICollection<EntryJsonData> JsonData { get; set; } = new List<EntryJsonData>();
    public ICollection<EntryAsset> Assets { get; set; } = new List<EntryAsset>();
}
