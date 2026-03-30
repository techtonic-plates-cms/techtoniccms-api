using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_numbers")]
[PrimaryKey(nameof(EntryId), nameof(FieldId))]
public class EntryNumber
{
    public required Guid EntryId { get; set; }
    public required Guid FieldId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public int? Value { get; set; }

    [StringLength(64)]
    public string? SearchHash { get; set; }

    public Entry Entry { get; set; } = null!;
    public Field Field { get; set; } = null!;
}
