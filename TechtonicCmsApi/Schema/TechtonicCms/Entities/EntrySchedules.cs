using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TechtonicCmsApi.Schema.TechtonicCms.Entities;

namespace TechtonicCmsApi.Schema.TechtonicCms.Entities;

[Table("entry_schedules")]
public class EntrySchedules
{

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public required Guid Id { get; set; }

    public required Entry Entry { get; set; }

    public required DateTime ScheduledTime { get; set; }

    public required bool AlreadyExecuted { get; set; }
}