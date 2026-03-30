using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_relations")]
[PrimaryKey(nameof(FromEntryId), nameof(FieldId), nameof(ToEntryId))]
public class EntryRelation
{
    public required Guid FromEntryId { get; set; }
    public required Guid FieldId { get; set; }
    public required Guid ToEntryId { get; set; }
    public required DateTime CreatedAt { get; set; }

    public Entry FromEntry { get; set; } = null!;
    public Field Field { get; set; } = null!;
    public Entry ToEntry { get; set; } = null!;
}
