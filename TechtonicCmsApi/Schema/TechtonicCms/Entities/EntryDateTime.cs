using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_datetimes")]
[PrimaryKey(nameof(EntryId), nameof(FieldId))]
public class EntryDateTime
{
    public required Guid EntryId { get; set; }
    public required Guid FieldId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? Value { get; set; }

    [StringLength(64)]
    public string? SearchHash { get; set; }

    public Entry Entry { get; set; } = null!;
    public Field Field { get; set; } = null!;
}
