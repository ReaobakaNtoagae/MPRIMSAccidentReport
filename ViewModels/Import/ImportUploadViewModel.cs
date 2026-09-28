using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CrashReport.ViewModels.Import
{
    public sealed class ImportUploadViewModel
    {
        [Required, MaxLength(50)] public string Region { get; set; } = string.Empty;
        [Range(1, 12)] public int ReportingMonth { get; set; }

        [Range(2000, 2100)] public int ReportingYear { get; set; }

        [Required] public IFormFile Workbook { get; set; } = null!;

        [MaxLength(1000)] public string? Notes { get; set; }
    }
}
