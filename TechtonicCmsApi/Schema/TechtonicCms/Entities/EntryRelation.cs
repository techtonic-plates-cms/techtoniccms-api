using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_relations")]
public class EntryRelation
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// The entry that owns this relation (the "source" side).
    /// </summary>
    public required Guid EntryId { get; set; }

    /// <summary>
    /// The field definition that describes this relation.
    /// </summary>
    public required Guid FieldId { get; set; }

    /// <summary>
    /// The entry being referenced (the "target" side).
    /// </summary>
    public required Guid TargetEntryId { get; set; }

    public Entry Entry { get; set; } = null!;
    public Field Field { get; set; } = null!;
    public Entry TargetEntry { get; set; } = null!;
}
