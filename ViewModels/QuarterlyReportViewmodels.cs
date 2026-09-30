namespace CrashReport.ViewModels
{
    public class QuarterlyReportRequest
    {
        public int Quarter { get; set; }

        public int Year { get; set; }

        // false = calendar quarters (Jan-Mar/Apr-Jun/Jul-Sep/Oct-Dec, Year = the
        // calendar year); true = fiscal quarters (year starts 1 April, Year = the
        // fiscal year's starting calendar year). See QuarterlyReportDataService.
        public bool IsFiscalYear { get; set; }

        public string? ProvinceCode { get; set; }

        public string ReportDate { get; set; } = string.Empty;
        public string RefNumber { get; set; } = "16/9/4";
        public string EnquiryName { get; set; } = "M C Mdhluli";

        public string EnquiryTel { get; set; } = "082 802 6966";

        public string ToName { get; set; } = "MR P NGOMANE (MPL)";

        public string ToTitle { get; set; } = "MEMBER OF THE EXECUTIVE COUNCIL";

        public string FromName { get; set; } = "MR W MTHOMBOTHI";
        public string FromTitle { get; set; } = "HEAD OF DEPARTMENT";


    }
    }
