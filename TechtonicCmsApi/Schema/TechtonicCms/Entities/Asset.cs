using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("assets")]
public class Asset
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    [StringLength(255)]
    public required string Filename { get; set; } = null!;

    [StringLength(100)]
    public required string MimeType { get; set; } = null!;

    public required int FileSize { get; set; }

    [StringLength(1024)]
    public required string Path { get; set; } = null!;

    [ForeignKey(nameof(UploadedByUser))]
    public required Guid UploadedBy { get; set; }

    public required DateTime UploadedAt { get; set; }

    [StringLength(500)]
    public string? Alt { get; set; }

    [Column(TypeName = "text")]
    public string? Caption { get; set; }

    public required bool IsPublic { get; set; }

    public User UploadedByUser { get; set; } = null!;
    public ICollection<Entry> Entries { get; set; } = new List<Entry>();
}
