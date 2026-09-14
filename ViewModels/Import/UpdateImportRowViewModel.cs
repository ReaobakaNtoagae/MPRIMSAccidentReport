using System.ComponentModel.DataAnnotations;

namespace CrashReport.ViewModels.Import;

// MVC binds this object from the edit form. It is not a database entity.
public sealed class UpdateImportRowViewModel
{
    public int BatchId { get; set; }
    public long StagingSummaryId { get; set; }
    [Required, StringLength(50)] public string? Station { get; set; }
    [StringLength(50)] public string? ArNumber { get; set; }
    [StringLength(50)] public string? CasNumber { get; set; }
    [Required] public string? CrashDate { get; set; }
    [Required] public string? CrashTime { get; set; }
    [StringLength(20)] public string? Route { get; set; }
    [Required, StringLength(150)] public string? Location { get; set; }
    [StringLength(30)] public string? CrashType { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}
