using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_rich_texts")]
[PrimaryKey(nameof(EntryId), nameof(FieldId))]
public class EntryRichText
{
    public required Guid EntryId { get; set; }
    public required Guid FieldId { get; set; }
    public required DateTime CreatedAt { get; set; }

    [Column(TypeName = "text")]
    public required string Raw { get; set; } = null!;

    [Column(TypeName = "text")]
    public required string Rendered { get; set; } = null!;

    [StringLength(20)]
    public required string Format { get; set; } = null!;

    [StringLength(64)]
    public string? SearchHash { get; set; }

    public Entry Entry { get; set; } = null!;
    public Field Field { get; set; } = null!;
}
