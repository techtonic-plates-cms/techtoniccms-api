using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_assets")]
[PrimaryKey(nameof(EntryId), nameof(FieldId), nameof(AssetId))]
public class EntryAsset
{
    public required Guid EntryId { get; set; }
    public required Guid FieldId { get; set; }
    public required Guid AssetId { get; set; }
    public required int SortOrder { get; set; }
    public required DateTime CreatedAt { get; set; }

    [StringLength(64)]
    public string? SearchHash { get; set; }

    public Entry Entry { get; set; } = null!;
    public Field Field { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
