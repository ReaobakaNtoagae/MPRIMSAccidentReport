using System.ComponentModel.DataAnnotations;

namespace CrashReport.Models.Reports
{
    public sealed class ReportInputModel
    {
        // Data annotations provide server-side validation. Browser validation is helpful,
        // but the server must never trust values submitted by a browser.
        [Required] public PeriodReportType ReportType { get; set; } = PeriodReportType.Monthly;
        [Range(2000, 2100)] public int Year { get; set; } = DateTime.Today.Year;
        [Range(1, 12)] public int? Month { get; set; } = DateTime.Today.Month;
        [Range(1, 4)] public int? Quarter { get; set; } = ((DateTime.Today.Month - 1) / 3) + 1;
        [Required, StringLength(30)] public string ReportDate { get; set; } = DateTime.Today.ToString("dd MMMM yyyy").ToUpperInvariant();
        [Required, StringLength(50)] public string RefNumber { get; set; } = "16/9/4";
        [Required, StringLength(100)] public string EnquiryName { get; set; } = "M C Mdhluli";
        [Required, Phone, StringLength(30)] public string EnquiryTel { get; set; } = "082 802 6966";
        [Required, StringLength(100)] public string ToName { get; set; } = "MR P NGOMANE (MPL)";
        [Required, StringLength(150)] public string ToTitle { get; set; } = "MEMBER OF THE EXECUTIVE COUNCIL";
        [Required, StringLength(100)] public string FromName { get; set; } = "MR W MTHOMBOTHI";
        [Required, StringLength(150)] public string FromTitle { get; set; } = "HEAD OF DEPARTMENT";

        // Keep MVC binding separate from the core request. The core library therefore
        // remains usable from controllers, background jobs or tests.
        public PeriodReportRequest ToRequest() => new()
        {
            ReportType = ReportType,
            Year = Year,
            Month = Month,
            Quarter = Quarter,
            ReportDate = ReportDate,
            RefNumber = RefNumber,
            EnquiryName = EnquiryName,
            EnquiryTel = EnquiryTel,
            ToName = ToName,
            ToTitle = ToTitle,
            FromName = FromName,
            FromTitle = FromTitle
        };
    }

    // The same page shows the form first and, after posting, the calculated preview below it.
    public sealed record ReportsIndexModel(ReportInputModel Input, PeriodReportModel? Preview = null, string? Error = null);
}
