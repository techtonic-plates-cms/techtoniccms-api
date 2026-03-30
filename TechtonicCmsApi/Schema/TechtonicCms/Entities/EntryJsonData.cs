using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_json_data")]
[PrimaryKey(nameof(EntryId), nameof(FieldId))]
public class EntryJsonData
{
    public required Guid EntryId { get; set; }
    public required Guid FieldId { get; set; }
    public required DateTime CreatedAt { get; set; }

    [Column(TypeName = "text")]
    public required string Value { get; set; } = null!;

    [StringLength(50)]
    public required string ValueType { get; set; } = null!;

    [StringLength(64)]
    public string? SearchHash { get; set; }

    public Entry Entry { get; set; } = null!;
    public Field Field { get; set; } = null!;
}
