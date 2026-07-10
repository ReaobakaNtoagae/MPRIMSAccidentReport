namespace CrashReport.ViewModels
{
    public class FiveYearReportRequest
    {
        public int Month { get; set; }
        public int EndYear { get; set; }

        public string ReportDate { get; set; } = string.Empty;

        public string RefNumber { get; set; } = "16/9/4";
        public string EnquiryName { get; set; } = "M C Mdhluli";
        public string EnquiryTel { get; set; } = "082 802 6966";
        public string ToName { get; set; } = "MR P NGOMANE (MPL)";
        public string ToTitle { get; set; } = "MEMBER OF THE EXECUTIVE COUNCIL";
        public string FromName { get; set; } = "MR W MTHOMBOTHI";
        public string FromTitle { get; set; } = "HEAD OF DEPARTMENT";

        public class YearlyStatRow
        {
            public string Label { get; set; } = string.Empty;
            public int[] Years = Array.Empty<int>();
            public int Total => Years.Sum();
            public int Average => Years.Length > 0 ? (int)Math.Round(Years.Average()) : 0;
            
        }

        public class RegionSummary
        {
            public string RegionName { get; set; } = string.Empty;
            public List<YearlyStatRow> Stats { get; set; } = new();
        }

        public class RankedRow
        {
            public string Label { get; set; } = string.Empty;
            public int[] Years { get; set; } = Array.Empty<int>();
            public int Total => Years.Sum();
            public int Average => Years.Length > 0 ? (int)Math.Round(Years.Average()) : 0;
            public double Percent { get; set; }

        }

        public class RankedTable
        {
            public string Title { get; set; } = string.Empty;
            public List<RankedRow> Rows { get; set; } = new();
            public int GrandTotal => Rows.Sum(r => r.Total);
        }

        public class RegionRouteData
        {
            public string RegionName { get; set; } = string.Empty;
            public RankedTable CrashRoutes { get; set; } = new();

            public RankedTable FatalityRoutes { get; set; } = new();
        }
        
        public class DemographicYearRow
        {
            public string Label { get; set; } = string.Empty;
            public Dictionary<int, int> ByYear { get; set;  } = new();

            public int Total => ByYear.Values.Sum();
        }

        public class FiveYearReportViewModel
        {
            public string MonthName { get; set; } = string.Empty;   // "MAY"
            public int StartYear { get; set; }                       // 2021
            public int EndYear { get; set; }                         // 2025
            public string ReportTitle => $"{MonthName} ANALYSIS FOR THE PAST FIVE YEARS";

            // ── Memo header fields ──
            public string ReportDate { get; set; } = string.Empty;
            public string RefNumber { get; set; } = string.Empty;
            public string EnquiryName { get; set; } = string.Empty;
            public string EnquiryTel { get; set; } = string.Empty;
            public string ToName { get; set; } = string.Empty;
            public string ToTitle { get; set; } = string.Empty;
            public string FromName { get; set; } = string.Empty;
            public string FromTitle { get; set; } = string.Empty;


            public List<RegionSummary> RegionSummaries { get; set; } = new();
            public RegionRouteData ProvincialRoutes { get; set; } = new();
            public List<RegionRouteData> RegionRoutes { get; set; } = new();
            public RankedTable CrashTypes { get; set; } = new();
            public RankedTable VehicleCategories { get; set; } = new();

            public RankedTable DaysOfWeekCrashes { get; set; } = new();
            public RankedTable DaysOfWeekFatalities { get; set; } = new();

            public RankedTable WeekendsCrashes { get; set; } = new();
            public RankedTable WeekendsFatalities { get; set; } = new();


            public RankedTable TimeSlotsCrashes { get; set; } = new();
            public RankedTable TimeSlotsFatalities { get; set; } = new();

            public List<int> DemographicYearsAvailable { get; set; } = new();

            public bool DemographicsHasGaps { get; set; }

            public List<DemographicYearRow> AgeGroups { get; set; } = new();
            public List<DemographicYearRow> MaleByRole { get; set; } = new();
            public List<DemographicYearRow> FemaleByRole { get; set; } = new();
            



        }

    }
}
