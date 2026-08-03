using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models;

[Table("crash_fatalities")]
public class CrashFatality
{
    [Key]
    [Column("fatality_id")]
    public int FatalityId { get; set; }

    [Column("summary_id")]
    public int SummaryId { get; set; }

    [Column("age")]
    public byte Age { get; set; }

    [Column("gender")]
    [MaxLength(1)]
    public string Gender { get; set; } = string.Empty; // "M" or "F"

    [Column("race")]
    [MaxLength(1)]
    public string Race { get; set; } = string.Empty; // "B","C","I","W","O"

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SummaryId))]
    public CrashSummary? Summary { get; set; }
}