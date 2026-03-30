using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("collections")]
[Index(nameof(Slug), IsUnique = true)]
public class Collection
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    [ForeignKey(nameof(CreatedByUser))]
    public required Guid CreatedBy { get; set; }

    [StringLength(255)]
    public required string Name { get; set; } = null!;

    [StringLength(255)]
    public required string Slug { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Icon { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    public required Locale DefaultLocale { get; set; }
    public required string[] SupportedLocales { get; set; } = ["en"];
    public required bool IsLocalized { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<Field> Fields { get; set; } = new List<Field>();
    public ICollection<Entry> Entries { get; set; } = new List<Entry>();
}
