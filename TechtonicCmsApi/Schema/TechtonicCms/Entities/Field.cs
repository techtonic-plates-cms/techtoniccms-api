using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("fields")]
[Index(nameof(CollectionId), nameof(Name), IsUnique = true)]
[Index(nameof(SensitivityLevel))]
[Index(nameof(IsPii))]
public class Field
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required Guid CollectionId { get; set; }

    [StringLength(255)]
    public required string Name { get; set; } = null!;

    [StringLength(255)]
    public string? Label { get; set; }

    [Column(TypeName = "text")]
    public string? Description { get; set; }

    public required bool IsRequired { get; set; }
    public required bool IsUnique { get; set; }
    public required bool IsPublic { get; set; }
    public required bool IsPii { get; set; }
    public required bool IsEncrypted { get; set; }

    [StringLength(20)]
    public required string SensitivityLevel { get; set; } = null!;

    [Column(TypeName = "text")]
    public string? ValidationRules { get; set; }

    [Column(TypeName = "text")]
    public string? DefaultValue { get; set; }

    [StringLength(1024)]
    public string? HelpText { get; set; }

    [ForeignKey(nameof(CreatedByUser))]
    public required Guid CreatedBy { get; set; }

    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }

    public Collection Collection { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;

    public required FieldDataType DataType { get; set; }

    /// <summary>
    /// For Relation fields, the ID of the collection this field references.
    /// </summary>
    public Guid? RelatedCollectionId { get; set; }

    public Collection? RelatedCollection { get; set; }

    /* public ICollection<EntryRelation> EntryRelations { get; set; } = new List<EntryRelation>();
     public ICollection<EntryTypstText> EntryTypstTexts { get; set; } = new List<EntryTypstText>();
     public ICollection<EntryText> EntryTexts { get; set; } = new List<EntryText>();
     public ICollection<EntryBoolean> EntryBooleans { get; set; } = new List<EntryBoolean>();
     public ICollection<EntryNumber> EntryNumbers { get; set; } = new List<EntryNumber>();
     public ICollection<EntryDateTime> EntryDateTimes { get; set; } = new List<EntryDateTime>();
     public ICollection<EntryRichText> EntryRichTexts { get; set; } = new List<EntryRichText>();
     public ICollection<EntryJsonData> EntryJsonData { get; set; } = new List<EntryJsonData>();
     public ICollection<EntryAsset> EntryAssets { get; set; } = new List<EntryAsset>();
     public ICollection<AbacEvaluationCache> EvaluationCaches { get; set; } = new List<AbacEvaluationCache>();
     public ICollection<AbacAudit> AuditLogs { get; set; } = new List<AbacAudit>(); */
}
