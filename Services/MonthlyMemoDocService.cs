using CrashReport.ViewModels;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace CrashReport.Services;


public class MonthlyMemoDocService
{
    
    private const int PW = 9638;

   
    private const string NAVY = "003366";
    private const string THEAD = "1F3864";
    private const string LGRAY = "F2F2F2";
    private const string WHITE = "FFFFFF";
    private const string BLACK = "000000";

    private static class MC
    {
        public const string CRASH = "2E5FA3";
        public const string FATAL = "000000";
        public const string SERIOUS = "C00000";
        public const string SLIGHT = "00B050";
        public const string DEFAULT = "000000";
    }

   
    private const int CW = 580;
    private const int CH = 300;
    
    private const int ChartBoxW = 520;
    private const int ChartBoxH = 260;

    private static readonly string[] AllDays =
        { "MONDAYS", "TUESDAYS", "WEDNESDAYS", "THURSDAYS", "FRIDAYS", "SATURDAYS", "SUNDAYS" };

    public Task<byte[]> GenerateAsync(MonthlyMemoViewModel vm)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            var embedder = new ImageEmbedder(main);
            main.Document = new Document(BuildBody(vm, embedder));
            ApplyPageLayout(main);
            main.Document.Save();
            doc.Save();
        }
        return Task.FromResult(ms.ToArray());
    }

    
    private Body BuildBody(MonthlyMemoViewModel vm, ImageEmbedder embedder)
    {
        var body = new Body();

        var c = vm.Provincial.Current ?? new PeriodStatsBlock();
        var pr = vm.Provincial.Prior ?? new PeriodStatsBlock();
        var cy = vm.CurrentYear;
        var py = vm.PriorYear;
        var hist = vm.FiveYearHistory ?? new List<YearHistory>();

       

        var metrics = new[] { "Crashes", "Fatalities", "Serious", "Slight" };

        double[] rawData = { c.Crashes, c.Fatalities, c.Serious, c.Slight };
        var filteredData = new List<double>();
        var filteredLabels = new List<string>();
        for (var i = 0; i < rawData.Length; i++)
        {
            if (rawData[i] > 0) { filteredData.Add(rawData[i]); filteredLabels.Add(metrics[i]); }
        }

        var pieCurrentYear = MonthlyMemoChartService.CreatePieChart(new PieChartConfig
        {
            Title = cy.ToString(),
            Data = filteredData.ToArray(),
            Labels = filteredLabels.ToArray(),
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });
        var piePreviousYear = MonthlyMemoChartService.CreatePieChart(new PieChartConfig
        {
            Title = py.ToString(),
            Data = new double[] { pr.Crashes, pr.Fatalities, pr.Serious, pr.Slight },
            Labels = metrics,
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });

        var fig2a = MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
        {
            Title = "Total Crashes: " + vm.MonthName,
            Labels = hist.Select(h => h.Year.ToString()).ToArray(),
            Datasets = new List<ChartDataset> { new() { Label = "CRASHES", Data = hist.Select(h => (double)h.Crashes).ToArray() } },
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });
        var fig2b = MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
        {
            Title = "Fatalities: " + vm.MonthName,
            Labels = hist.Select(h => h.Year.ToString()).ToArray(),
            Datasets = new List<ChartDataset> { new() { Label = "FATALITIES", Data = hist.Select(h => (double)h.Fatalities).ToArray() } },
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });

        var fig3 = MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
        {
            Title = "Fatal Victims by Category",
            Labels = new[] { py.ToString(), cy.ToString() },
            Datasets = new List<ChartDataset>
            {
                new() { Label = "DRIVERS", Data = new double[] { pr.FatalDrivers, c.FatalDrivers } },
                new() { Label = "PASSENGERS", Data = new double[] { pr.FatalPassengers, c.FatalPassengers } },
                new() { Label = "PEDESTRIANS", Data = new double[] { pr.FatalPedestrians, c.FatalPedestrians } },
                new() { Label = "CYCLISTS", Data = new double[] { pr.FatalCyclists, c.FatalCyclists } },
            },
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });

        var fig4 = MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
        {
            Title = "Serious Injuries by Category",
            Labels = new[] { py.ToString(), cy.ToString() },
            Datasets = new List<ChartDataset>
            {
                new() { Label = "DRIVERS", Data = new double[] { pr.SeriousDrivers, c.SeriousDrivers } },
                new() { Label = "PASSENGERS", Data = new double[] { pr.SeriousPassengers, c.SeriousPassengers } },
                new() { Label = "PEDESTRIANS", Data = new double[] { pr.SeriousPedestrians, c.SeriousPedestrians } },
                new() { Label = "CYCLISTS", Data = new double[] { pr.SeriousCyclists, c.SeriousCyclists } },
            },
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });

        var fig5 = MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
        {
            Title = "Slight Injuries by Category",
            Labels = new[] { py.ToString(), cy.ToString() },
            Datasets = new List<ChartDataset>
            {
                new() { Label = "DRIVERS", Data = new double[] { pr.SlightDrivers, c.SlightDrivers } },
                new() { Label = "PASSENGERS", Data = new double[] { pr.SlightPassengers, c.SlightPassengers } },
                new() { Label = "PEDESTRIANS", Data = new double[] { pr.SlightPedestrians, c.SlightPedestrians } },
                new() { Label = "CYCLISTS", Data = new double[] { pr.SlightCyclists, c.SlightCyclists } },
            },
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });

        var pRoutes = (vm.ProvincialRoutes ?? new List<RouteStats>()).Take(6).ToList();
        byte[]? fig6 = pRoutes.Count > 0
            ? MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
            {
                Title = "Provincial Problematic Routes",
                Labels = new[] { $"CRASHES\n{py}", $"CRASHES\n{cy}", $"FAT\n{py}", $"FAT\n{cy}" },
                Datasets = pRoutes.Select(r => new ChartDataset
                {
                    Label = r.Route,
                    Data = new double[] { r.CrashesPrev, r.CrashesCurr, r.FatalPrev, r.FatalCurr }
                }).ToList(),
                LegendPosition = "right",
                Palette = MonthlyMemoChartService.RoutePalette,
                Width = CW,
                Height = CH
            })
            : null;

        var defaultSlots = new List<TimeSlotStats>
        {
            new() { Slot = "06H00 - 14H00" },
            new() { Slot = "14H00 - 22H00" },
            new() { Slot = "22H00 - 06H00" },
        };
        var tSlots = (vm.TimeSlots != null && vm.TimeSlots.Count > 0) ? vm.TimeSlots : defaultSlots;
        byte[]? fig7 = tSlots.Count > 0
            ? MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
            {
                Title = "Crashes by Time of Day",
                Labels = tSlots.Select(t => t.Slot).ToArray(),
                Datasets = new List<ChartDataset>
                {
                    new() { Label = py.ToString(), Data = tSlots.Select(t => (double)t.CrashesPrev).ToArray() },
                    new() { Label = cy.ToString(), Data = tSlots.Select(t => (double)t.CrashesCurr).ToArray() },
                },
                LegendPosition = "right",
                Width = CW,
                Height = CH
            })
            : null;

        var dowProv = (vm.DaysOfWeek != null && vm.DaysOfWeek.TryGetValue("Provincial", out var dp)) ? dp : new List<DayStats>();
        byte[]? fig8 = dowProv.Count > 0
            ? MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
            {
                Title = "Crashes by Day of Week (Province)",
                Labels = dowProv.Select(d => d.Day.Length >= 3 ? d.Day[..3] : d.Day).ToArray(),
                Datasets = new List<ChartDataset>
                {
                    new() { Label = py.ToString(), Data = dowProv.Select(d => (double)d.CrashesPrev).ToArray() },
                    new() { Label = cy.ToString(), Data = dowProv.Select(d => (double)d.CrashesCurr).ToArray() },
                },
                LegendPosition = "right",
                Width = CW,
                Height = CH
            })
            : null;

        var ageData = c.FatalAgeGroups ?? new Dictionary<string, int>();
        var ageBar = MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
        {
            Title = "Age Distribution of Fatalities",
            Labels = ageData.Keys.ToArray(),
            Datasets = new List<ChartDataset> { new() { Label = "Fatalities", Data = ageData.Values.Select(v => (double)v).ToArray() } },
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });

        var genderData = c.FatalGender ?? new Dictionary<string, Dictionary<string, int>>();
        var genderRoles = new[] { "Drivers", "Passengers", "Pedestrians", "Cyclists" };
        double[] GenderValues(string key) => genderRoles
            .Select(r => genderData.TryGetValue(r, out var rd) && rd.TryGetValue(key, out var v) ? (double)v : 0)
            .ToArray();
        var genderBar = MonthlyMemoChartService.CreateGroupedBarChart(new GroupedBarChartConfig
        {
            Title = "Fatalities by Gender and Role",
            Labels = genderRoles,
            Datasets = new List<ChartDataset>
            {
                new() { Label = "Male", Data = GenderValues("Male") },
                new() { Label = "Female", Data = GenderValues("Female") },
            },
            LegendPosition = "right",
            Width = CW,
            Height = CH
        });

        // ── Header ────────────────────────────────────────────────
        body.AppendChild(P(R($"Ref: {Or(vm.RefNumber, "16/9/4")}", size: "18"), align: JustificationValues.Right, after: "0"));
        body.AppendChild(P(R($"Enq: {Or(vm.EnquiryName, "M C Mdhluli")}", size: "18"), align: JustificationValues.Right, after: "0"));
        body.AppendChild(P(R($"Tel: {Or(vm.EnquiryTel, "082 802 6966")}", size: "18"), align: JustificationValues.Right, after: "160"));
        body.AppendChild(P(R("MEMORANDUM", bold: true, size: "28", color: NAVY), align: JustificationValues.Center, after: "200"));

        var headerLines = new (string Label, string Value)[]
        {
            ("TO", Or(vm.ToName, "MR P NGOMANE (MPL)")),
            ("", Or(vm.ToTitle, "MEMBER OF THE EXECUTIVE COUNCIL")),
            ("FROM", Or(vm.FromName, "MR W MTHOMBOTHI")),
            ("", Or(vm.FromTitle, "HEAD OF DEPARTMENT")),
            ("DATE", vm.ReportDate ?? ""),
            ("SUBJECT", "REPORT ON CRASHES, FATALITIES, SERIOUS AND SLIGHT INJURIES: " + vm.PeriodFrom + " TO " + vm.PeriodTo +
                        " AS COMPARED WITH THE SAME PERIOD THE PREVIOUS YEAR"),
        };
        foreach (var (lbl, val) in headerLines)
        {
            body.AppendChild(string.IsNullOrEmpty(lbl)
                ? P(R(val, bold: true, size: "20"), indentLeft: "1440", after: "40")
                : P(new[]
                  {
                      R(lbl.PadRight(10), bold: true, size: "20"),
                      R(":   ", bold: true, size: "20"),
                      R(val, bold: true, size: "20"),
                  }, after: "40"));
        }
        body.AppendChild(Blank(160));


        body.AppendChild(Heading("PURPOSE"));
        body.AppendChild(P(R($"To inform the Member of the Executive Council, of crashes and fatalities recorded in the province for the period {vm.PeriodFrom} to {vm.PeriodTo}, as compared with the same period the previous year.", size: "20"), after: "120"));
        body.AppendChild(Blank());

        // ── DISCUSSION ────────────────────────────────────────────
        int crashesN = c.Crashes, fatalsN = c.Fatalities, seriousN = c.Serious, slightN = c.Slight;
        body.AppendChild(Heading("DISCUSSION"));
        body.AppendChild(P(R($"During {vm.MonthYear} {W(crashesN)} ({crashesN}) road crashes took place, resulting in {W(fatalsN)} ({fatalsN}) fatalities, {W(seriousN)} ({seriousN}) serious injuries and {W(slightN)} ({slightN}) slight injuries.", size: "20"), after: "120"));
        body.AppendChild(Blank());

        // ── PROVINCIAL CRASHES: MONTH TO MONTH COMPARISON ────────
        body.AppendChild(Heading("PROVINCIAL CRASHES: MONTH TO MONTH COMPARISON:"));
        body.AppendChild(P(R($"{vm.PeriodFrom} – {vm.PeriodTo}", bold: true, size: "20"), after: "80"));
        body.AppendChild(P(R("The following represent the number of crashes, fatalities and injuries recorded.", size: "20"), after: "80"));
        body.AppendChild(CompTable(new List<(string, int?, int?)>
        {
            ("CRASHES", pr.Crashes, c.Crashes),
            ("FATALITIES", pr.Fatalities, c.Fatalities),
            ("SERIOUS INJURIES", pr.Serious, c.Serious),
            ("SLIGHT INJURIES", pr.Slight, c.Slight),
        }, py, cy));
        body.AppendChild(Blank(80));
        body.AppendChild(P(R("FIGURE 1", bold: true, size: "18"), align: JustificationValues.Center, after: "40"));
        body.AppendChild(embedder.ChartPara(pieCurrentYear, ChartBoxW, ChartBoxH));
        body.AppendChild(embedder.ChartPara(piePreviousYear, ChartBoxW, ChartBoxH));
        body.AppendChild(FigNarrative("Figure 1 represents " + ChangedList(new[]
        {
            ("Crashes", pr.Crashes, c.Crashes),
            ("Fatalities", pr.Fatalities, c.Fatalities),
            ("Serious injuries", pr.Serious, c.Serious),
            ("Slight injuries", pr.Slight, c.Slight),
        }) + " as compared with the same period the previous year."));
        body.AppendChild(Blank());

        
        body.AppendChild(P(R("AVERAGE PER DAY", bold: true, size: "20"), after: "80"));
        {
            var days = vm.DaysInPeriod > 0 ? vm.DaysInPeriod : 30;
            int a0 = PW / 4, a1 = PW / 4, a2 = PW / 4, a3 = PW - (PW / 4) * 3;
            var t = NewSimpleTable(new[] { a0, a1, a2, a3 });
            var hdr = new TableRow();
            hdr.AppendChild(HdrCell("CRASHES", a0));
            hdr.AppendChild(HdrCell("FATALITIES", a1));
            hdr.AppendChild(HdrCell("SERIOUS INJURIES", a2));
            hdr.AppendChild(HdrCell("SLIGHT INJURIES", a3));
            t.Append(hdr);
            var row = new TableRow();
            row.AppendChild(DataCell(((double)crashesN / days).ToString("F1", CultureInfo.InvariantCulture), a0, bold: true, color: MC.CRASH));
            row.AppendChild(DataCell(((double)fatalsN / days).ToString("F1", CultureInfo.InvariantCulture), a1, bold: true, color: MC.FATAL));
            row.AppendChild(DataCell(((double)seriousN / days).ToString("F1", CultureInfo.InvariantCulture), a2, bold: true, color: MC.SERIOUS));
            row.AppendChild(DataCell(((double)slightN / days).ToString("F1", CultureInfo.InvariantCulture), a3, bold: true, color: MC.SLIGHT));
            t.Append(row);
            body.AppendChild(t);
            body.AppendChild(Blank());
        }

       
        if (hist.Count > 0)
        {
            var avgC = hist.Average(h => h.Crashes);
            var avgF = hist.Average(h => h.Fatalities);
            body.AppendChild(P(R($"FIGURE 2: CRASHES AND FATALITIES: {vm.MonthName}", bold: true, size: "20"), after: "80"));
            body.AppendChild(FiveYearTable(hist));
            body.AppendChild(Blank(80));
            body.AppendChild(P(R($"FIGURE 2A: TOTAL CRASHES – {vm.MonthName}", bold: true, size: "18"), align: JustificationValues.Center, after: "40"));
            body.AppendChild(embedder.ChartPara(fig2a, ChartBoxW, ChartBoxH));
            body.AppendChild(Blank(40));
            body.AppendChild(P(R($"FIGURE 2B: FATALITIES – {vm.MonthName}", bold: true, size: "18"), align: JustificationValues.Center, after: "40"));
            body.AppendChild(embedder.ChartPara(fig2b, ChartBoxW, ChartBoxH));
            var monthLabel = string.IsNullOrEmpty(vm.MonthName) ? "this period" : vm.MonthName;
            var monthLabel2 = string.IsNullOrEmpty(vm.MonthName) ? "month" : vm.MonthName;
            body.AppendChild(FigNarrative(
                $"Figure 2A and 2B reflect a 5-year trend for {monthLabel}. " +
                $"The average is {avgC.ToString("F1", CultureInfo.InvariantCulture)} crashes and {avgF.ToString("F1", CultureInfo.InvariantCulture)} fatalities per {monthLabel2} over the past five years. " +
                $"Crashes have {Chg(hist.First().Crashes, hist.Last().Crashes)} and fatalities have {Chg(hist.First().Fatalities, hist.Last().Fatalities)} over this period."));
            body.AppendChild(Blank());
        }

       
        var hasAge = ageData.Count > 0;
        var hasGender = genderData.Count > 0;
        if (hasAge || hasGender)
        {
            body.AppendChild(Heading("PROVINCIAL FATALITIES: AGE GROUP AND GENDER"));
            if (hasAge)
            {
                body.AppendChild(P(R("VICTIMS", bold: true, size: "20"), after: "60"));
                body.AppendChild(AgeGroupTable(ageData));
                body.AppendChild(Blank(80));
                body.AppendChild(embedder.ChartPara(ageBar, ChartBoxW, ChartBoxH));
                body.AppendChild(Blank(40));
            }
            if (hasGender)
            {
                body.AppendChild(P(R("VICTIMS GENDER", bold: true, size: "20"), after: "60"));
                body.AppendChild(GenderTable(genderData));
                body.AppendChild(Blank(80));
                body.AppendChild(embedder.ChartPara(genderBar, ChartBoxW, ChartBoxH));
                body.AppendChild(Blank(40));
                body.AppendChild(FigNarrative("The above table and graph indicates the distribution of fatalities by age group and gender. " +
                    "The majority of fatalities occurred in the 19–35 age group, with males accounting for the highest number."));
            }
            body.AppendChild(Blank());
        }

        
        body.AppendChild(P(R("CATEGORIES OF VICTIMS", bold: true, size: "20"), after: "80"));
        body.AppendChild(CompTable(new List<(string, int?, int?)>
        {
            ("DRIVERS", pr.FatalDrivers, c.FatalDrivers),
            ("PASSENGERS", pr.FatalPassengers, c.FatalPassengers),
            ("PEDESTRIANS", pr.FatalPedestrians, c.FatalPedestrians),
            ("CYCLISTS", pr.FatalCyclists, c.FatalCyclists),
        }, py, cy));
        body.AppendChild(Blank(80));
        body.AppendChild(P(R("FIGURE 3: GRAPHIC REPRESENTATION OF THE ABOVE", bold: true, size: "18"), align: JustificationValues.Center, after: "40"));
        body.AppendChild(embedder.ChartPara(fig3, ChartBoxW, ChartBoxH));
        body.AppendChild(FigNarrative("Figure 3 represents " + ChangedList(new[]
        {
            ("Driver fatalities", pr.FatalDrivers, c.FatalDrivers),
            ("Passenger fatalities", pr.FatalPassengers, c.FatalPassengers),
            ("Pedestrian fatalities", pr.FatalPedestrians, c.FatalPedestrians),
            ("Cyclist fatalities", pr.FatalCyclists, c.FatalCyclists),
        }) + " as compared with the same period the previous year."));
        body.AppendChild(Blank());

        
        body.AppendChild(P(R("SERIOUS INJURIES", bold: true, size: "20"), after: "80"));
        body.AppendChild(CompTable(new List<(string, int?, int?)>
        {
            ("DRIVERS", pr.SeriousDrivers, c.SeriousDrivers),
            ("PASSENGERS", pr.SeriousPassengers, c.SeriousPassengers),
            ("PEDESTRIANS", pr.SeriousPedestrians, c.SeriousPedestrians),
            ("CYCLISTS", pr.SeriousCyclists, c.SeriousCyclists),
        }, py, cy));
        body.AppendChild(Blank(80));
        body.AppendChild(P(R("FIGURE 4: GRAPHIC REPRESENTATION OF THE ABOVE", bold: true, size: "18"), align: JustificationValues.Center, after: "40"));
        body.AppendChild(embedder.ChartPara(fig4, ChartBoxW, ChartBoxH));
        body.AppendChild(FigNarrative("Figure 4 shows serious injuries for " + ChangedList(new[]
        {
            ("drivers", pr.SeriousDrivers, c.SeriousDrivers),
            ("passengers", pr.SeriousPassengers, c.SeriousPassengers),
            ("pedestrians", pr.SeriousPedestrians, c.SeriousPedestrians),
            ("cyclists", pr.SeriousCyclists, c.SeriousCyclists),
        }) + " as compared with the same period the previous year."));
        body.AppendChild(Blank());

        
        body.AppendChild(P(R("SLIGHT INJURIES", bold: true, size: "20"), after: "80"));
        body.AppendChild(CompTable(new List<(string, int?, int?)>
        {
            ("DRIVERS", pr.SlightDrivers, c.SlightDrivers),
            ("PASSENGERS", pr.SlightPassengers, c.SlightPassengers),
            ("PEDESTRIANS", pr.SlightPedestrians, c.SlightPedestrians),
            ("CYCLISTS", pr.SlightCyclists, c.SlightCyclists),
        }, py, cy));
        body.AppendChild(Blank(80));
        body.AppendChild(P(R("FIGURE 5: GRAPHIC REPRESENTATION OF THE ABOVE", bold: true, size: "18"), align: JustificationValues.Center, after: "40"));
        body.AppendChild(embedder.ChartPara(fig5, ChartBoxW, ChartBoxH));
        body.AppendChild(FigNarrative("Figure 5 shows slight injuries for " + ChangedList(new[]
        {
            ("drivers", pr.SlightDrivers, c.SlightDrivers),
            ("passengers", pr.SlightPassengers, c.SlightPassengers),
            ("pedestrians", pr.SlightPedestrians, c.SlightPedestrians),
            ("cyclists", pr.SlightCyclists, c.SlightCyclists),
        }) + " as compared with the same period the previous year."));
        body.AppendChild(Blank());

        
        var districts = vm.Districts ?? new List<DistrictMemoStats>();
        body.AppendChild(P(R($"COMPARISON BY DISTRICT: {vm.PeriodFrom} – {vm.PeriodTo} AS COMPARED WITH THE SAME PERIOD THE PREVIOUS YEAR.", bold: true, size: "20"), after: "80"));
        body.AppendChild(DistTable(districts, py, cy, new (string, Func<DistrictMemoStats, int?>, Func<DistrictMemoStats, int?>)[]
        {
            ("CRASHES", d => d.Prior?.Crashes, d => d.Current?.Crashes),
            ("FATAL", d => d.Prior?.Fatalities, d => d.Current?.Fatalities),
            ("SERIOUS", d => d.Prior?.Serious, d => d.Current?.Serious),
            ("SLIGHT", d => d.Prior?.Slight, d => d.Current?.Slight),
        }));
        body.AppendChild(Blank());

        
        body.AppendChild(P(R("CATEGORIES OF VICTIMS PER DISTRICT", bold: true, size: "20"), after: "80"));
        body.AppendChild(DistTable(districts, py, cy, new (string, Func<DistrictMemoStats, int?>, Func<DistrictMemoStats, int?>)[]
        {
            ("DRIVER", d => d.Prior?.FatalDrivers, d => d.Current?.FatalDrivers),
            ("PASSENGER", d => d.Prior?.FatalPassengers, d => d.Current?.FatalPassengers),
            ("PEDESTRIANS", d => d.Prior?.FatalPedestrians, d => d.Current?.FatalPedestrians),
            ("CYCLISTS", d => d.Prior?.FatalCyclists, d => d.Current?.FatalCyclists),
        }));
        body.AppendChild(Blank(80));
        body.AppendChild(P(R("SERIOUS INJURIES", bold: true, size: "20"), after: "80"));
        body.AppendChild(DistTable(districts, py, cy, new (string, Func<DistrictMemoStats, int?>, Func<DistrictMemoStats, int?>)[]
        {
            ("DRIVER", d => d.Prior?.SeriousDrivers, d => d.Current?.SeriousDrivers),
            ("PASSENGER", d => d.Prior?.SeriousPassengers, d => d.Current?.SeriousPassengers),
            ("PEDESTRIANS", d => d.Prior?.SeriousPedestrians, d => d.Current?.SeriousPedestrians),
            ("CYCLISTS", d => d.Prior?.SeriousCyclists, d => d.Current?.SeriousCyclists),
        }));
        body.AppendChild(Blank(80));
        body.AppendChild(P(R("SLIGHT INJURIES", bold: true, size: "20"), after: "80"));
        body.AppendChild(DistTable(districts, py, cy, new (string, Func<DistrictMemoStats, int?>, Func<DistrictMemoStats, int?>)[]
        {
            ("DRIVER", d => d.Prior?.SlightDrivers, d => d.Current?.SlightDrivers),
            ("PASSENGER", d => d.Prior?.SlightPassengers, d => d.Current?.SlightPassengers),
            ("PEDESTRIANS", d => d.Prior?.SlightPedestrians, d => d.Current?.SlightPedestrians),
            ("CYCLISTS", d => d.Prior?.SlightCyclists, d => d.Current?.SlightCyclists),
        }));
        body.AppendChild(Blank());

        
        if (vm.ProvincialRoutes != null && vm.ProvincialRoutes.Count > 0)
        {
            var rf = c.Fatalities != 0 ? c.Fatalities : 1;
            var rft = pRoutes.Sum(r => r.FatalCurr);
            body.AppendChild(Heading("PROVINCIAL PROBLEMATIC ROUTES"));
            body.AppendChild(P(R($"The following priority routes were identified in the province. The problematic routes show a {((double)rft / rf * 100).ToString("F1", CultureInfo.InvariantCulture)}% contribution of fatalities during this period.", size: "20"), after: "80"));
            body.AppendChild(RouteTable(vm.ProvincialRoutes, py, cy));
            body.AppendChild(Blank(80));
            if (fig6 != null)
            {
                body.AppendChild(P(R("FIGURE 6: GRAPHIC REPRESENTATION OF THE ABOVE", bold: true, size: "18"), align: JustificationValues.Center, after: "40"));
                body.AppendChild(embedder.ChartPara(fig6, ChartBoxW, ChartBoxH));
                body.AppendChild(Blank());
            }
        }

        
        body.AppendChild(Heading("REGIONAL PROBLEMATIC ROUTES"));
        foreach (var d in districts)
        {
            if (d.Routes == null || d.Routes.Count == 0) continue;
            var df = (d.Current?.Fatalities ?? 0) != 0 ? d.Current!.Fatalities : 1;
            var dft = d.Routes.Sum(r => r.FatalCurr);
            body.AppendChild(P(R($"{d.Name} DISTRICT", bold: true, size: "20"), after: "80"));
            body.AppendChild(RouteTable(d.Routes, py, cy));
            body.AppendChild(P(R($"The problematic routes for {d.Name} shows a {Math.Round((double)dft / df * 100, 0, MidpointRounding.AwayFromZero)}% contribution of fatalities during this period.", size: "20"), after: "80"));
            body.AppendChild(Blank());
        }

        
        if (vm.CrashTypes != null && vm.CrashTypes.Count > 0)
        {
            body.AppendChild(Heading("PROVINCIAL CRASHES TYPES"));
            body.AppendChild(TypeOrCategoryTable("TYPES",
                vm.CrashTypes.Select(ct => (ct.Type, ct.CrashesPrev, ct.CrashesCurr, ct.FatalPrev, ct.FatalCurr)), py, cy));

            var sortedByFatalities = vm.CrashTypes.OrderByDescending(ct => ct.FatalCurr).ToList();
            body.AppendChild(P(R("Priority crashes that make up the highest number of fatalities are as follows:", size: "20"), after: "60"));
            
            foreach (var ct in sortedByFatalities)
            {
                body.AppendChild(P(new[] { R("•  ", bold: true, size: "20"), R(ct.Type, bold: true, size: "21") },
                    indentLeft: "360", after: "80"));
            }
            body.AppendChild(Blank());
        }

        
        if (vm.VehicleCategories != null && vm.VehicleCategories.Count > 0)
        {
            body.AppendChild(Heading("PROVINCIAL VEHICLE CATEGORIES"));
            body.AppendChild(P(R("A majority of vehicles involved in crashes are Sedans, LDV and Taxi's.", size: "20"), after: "80"));
            body.AppendChild(TypeOrCategoryTable("VEHICLE CATEGORIES",
                vm.VehicleCategories.Select(vc => (vc.Category, vc.CrashesPrev, vc.CrashesCurr, vc.FatalPrev, vc.FatalCurr)), py, cy));
            body.AppendChild(Blank());
        }

       
        {
            body.AppendChild(Heading("PROVINCIAL PREVALENT TIMES"));
            body.AppendChild(P(R("Fatalities are mostly prevalent between 14h00 to 06h00. The table that follows indicates the times and number of crashes in the Province and all Districts respectively.", size: "20"), after: "80"));
            body.AppendChild(TimeTable(tSlots, py, cy));
            body.AppendChild(Blank(80));

            var fatal1406 = tSlots.Where(t => t.Slot != "06H00 - 14H00").Sum(t => t.FatalCurr);
            var fatalTotal = tSlots.Sum(t => t.FatalCurr);
            var contribPct = fatalTotal > 0 ? Math.Round((double)fatal1406 / fatalTotal * 100, 0, MidpointRounding.AwayFromZero) : 0;
            body.AppendChild(P(R($"The prevalent times between 14h00 until 06h00 shows a {contribPct} % contribution of fatalities during this period.", size: "20"), after: "80"));

            if (fig7 != null)
            {
                var peakSlot = tSlots.Aggregate((a, b) => b.FatalCurr > a.FatalCurr ? b : a);
                body.AppendChild(embedder.ChartPara(fig7, ChartBoxW, ChartBoxH));
                body.AppendChild(FigNarrative(
                    "Figure 7 illustrates the prevalent times of crashes in the province. " +
                    $"The highest number of fatalities occurred between {peakSlot.Slot} with {peakSlot.FatalCurr} fatal{(peakSlot.FatalCurr != 1 ? "ities" : "ity")} recorded."));
                body.AppendChild(Blank());
            }
        }

        
        if (dowProv.Count > 0)
        {
            body.AppendChild(Heading("PROVINCIAL DAYS OF THE WEEK"));
            body.AppendChild(P(R("There is a relative spread of crashes throughout the Province, with an increase being observed from Monday, Friday to Sunday.", size: "20"), after: "80"));
            body.AppendChild(P(R("PROVINCIAL", bold: true, size: "20"), after: "60"));
            body.AppendChild(DaysTable(dowProv, py, cy));
            body.AppendChild(Blank(80));

            if (fig8 != null)
            {
                var peakDay = dowProv.Aggregate((a, b) => b.CrashesCurr > a.CrashesCurr ? b : a);
                var peakFatalDay = dowProv.Aggregate((a, b) => b.FatalCurr > a.FatalCurr ? b : a);
                var text = "Figure 8 represents the distribution of crashes per day of the week. " +
                           $"{peakDay.Day} recorded the highest number of crashes ({peakDay.CrashesCurr})";
                if (peakFatalDay.Day != peakDay.Day)
                    text += $" while {peakFatalDay.Day} recorded the highest fatalities ({peakFatalDay.FatalCurr})";
                text += ".";
                body.AppendChild(embedder.ChartPara(fig8, ChartBoxW, ChartBoxH));
                body.AppendChild(FigNarrative(text));
                body.AppendChild(Blank());
            }

            foreach (var dist in districts)
            {
                var dd = (vm.DaysOfWeek != null && vm.DaysOfWeek.TryGetValue(dist.Key, out var v)) ? v : new List<DayStats>();
                if (dd.Count == 0) continue;
                body.AppendChild(P(R($"{dist.Name} DISTRICT", bold: true, size: "20"), after: "60"));
                body.AppendChild(DaysTable(dd, py, cy));
                body.AppendChild(Blank());
            }
        }

        
        var cV = Variation(pr.Crashes, c.Crashes);
        var fV = Variation(pr.Fatalities, c.Fatalities);
        var sV = Variation(pr.Serious, c.Serious);
        var slV = Variation(pr.Slight, c.Slight);
        body.AppendChild(Heading("CONCLUSION"));
        var conclusionBullets = new[]
        {
            $"Number of crashes {(c.Crashes > pr.Crashes ? "increased" : "decreased")} by {FmtPct(cV)}" +
            $" percent, fatalities {(c.Fatalities > pr.Fatalities ? "increased" : "decreased")} by {FmtPct(fV)}" +
            $" percent, serious injuries {(c.Serious > pr.Serious ? "increased" : "decreased")} by {FmtPct(sV)}" +
            $" percent and slight injuries {(c.Slight > pr.Slight ? "increased" : "decreased")} by {FmtPct(slV)} percent.",
            "Law Enforcement to increase its deployment of traffic officers over weekends and into late shifts.",
            "Attention should be given to Sedans, LDV's and Taxi's, as they contributed highest number of crashes and injuries recorded.",
        };
        foreach (var t in conclusionBullets)
        {
            body.AppendChild(P(new[] { R("•  ", bold: true), R(t, size: "20") }, indentLeft: "360", after: "80"));
        }
        body.AppendChild(Blank(120));

        body.AppendChild(Heading("RECOMMENDATIONS"));
        body.AppendChild(P(R("It is recommended that the MEC takes note of the contents of the report and give guidance where he deems necessary.", size: "20"), after: "300"));
        body.AppendChild(Blank(300));

        // ── SIGNATURE ────────────────────────────────────────────────
        body.AppendChild(SigLine());
        body.AppendChild(P(R(Or(vm.FromName, "MR W MTHOMBOTHI"), bold: true, size: "20"), after: "0"));
        body.AppendChild(P(R(Or(vm.FromTitle, "HEAD OF DEPARTMENT"), bold: true, size: "20"), after: "160"));
        body.AppendChild(P(R("NOTED / NOT NOTED", bold: true, size: "20"), after: "60"));
        body.AppendChild(P(R("COMMENTS:", bold: true, size: "20"), after: "200"));
        body.AppendChild(SigLine());
        body.AppendChild(P(R(Or(vm.ToName, "MR P NGOMANE (MPL)"), bold: true, size: "20"), after: "0"));
        body.AppendChild(P(R(Or(vm.ToTitle, "MEMBER OF THE EXECUTIVE COUNCIL"), bold: true, size: "20"), after: "0"));

        body.AppendChild(new SectionProperties());
        return body;
    }

  

    private static Table CompTable(List<(string Label, int? Prev, int? Curr)> rows, int py, int cy)
    {
        int c0 = F(PW * 0.35), c1 = F(PW * 0.20), c2 = F(PW * 0.20);
        int c3 = PW - F(PW * 0.35) - F(PW * 0.40);
        var table = NewSimpleTable(new[] { c0, c1, c2, c3 });

        var hdr = new TableRow();
        hdr.AppendChild(HdrCell("", c0));
        hdr.AppendChild(HdrCell(py.ToString(), c1));
        hdr.AppendChild(HdrCell(cy.ToString(), c2));
        hdr.AppendChild(HdrCell("DIFFERENCE", c3));
        table.Append(hdr);

        foreach (var (label, prev, curr) in rows)
        {
            var mc = MetricColor(label);
            var variation = Variation(prev, curr);
            var p = prev ?? 0;
            var cu = curr ?? 0;
            var tr = new TableRow();
            tr.AppendChild(LabelCell(label, c0, mc));
            tr.AppendChild(DataCell(p, c1, fill: "F0F4FF", color: mc));
            tr.AppendChild(DataCell(cu, c2, bold: true, color: mc));
            tr.AppendChild(DataCell(variation, c3, fill: cu > p ? "FFE8E8" : "E8FFE8", bold: true, color: mc));
            table.Append(tr);
        }
        return table;
    }

    private static Table DistTable(List<DistrictMemoStats> dists, int py, int cy,
        IEnumerable<(string Label, Func<DistrictMemoStats, int?> GetPrev, Func<DistrictMemoStats, int?> GetCurr)> rows)
    {
        const int lW = 1400;
        var dCount = dists.Count > 0 ? dists.Count : 4;
        var dW = (PW - lW) / dCount;
        var cW = dW / 2;

        var gridWidths = new List<int> { lW };
        foreach (var _ in dists) { gridWidths.Add(cW); gridWidths.Add(cW); }
        var table = NewSimpleTable(gridWidths.ToArray());

        var hdrRow1 = new TableRow();
        hdrRow1.AppendChild(HdrCell("", lW));
        foreach (var d in dists) hdrRow1.AppendChild(HdrCell(d.Name, dW, 2));
        table.Append(hdrRow1);

        var hdrRow2 = new TableRow();
        hdrRow2.AppendChild(HdrCell("", lW));
        foreach (var _ in dists)
        {
            hdrRow2.AppendChild(HdrCell(py.ToString(), cW));
            hdrRow2.AppendChild(HdrCell(cy.ToString(), cW));
        }
        table.Append(hdrRow2);

        foreach (var (label, getPrev, getCurr) in rows)
        {
            var mc = MetricColor(label);
            var tr = new TableRow();
            tr.AppendChild(LabelCell(label, lW, mc));
            foreach (var d in dists)
            {
                tr.AppendChild(DataCell(getPrev(d) ?? 0, cW, fill: "F0F4FF", color: mc));
                tr.AppendChild(DataCell(getCurr(d) ?? 0, cW, bold: true, color: mc));
            }
            table.Append(tr);
        }
        return table;
    }

    private static Table RouteTable(List<RouteStats> routes, int py, int cy)
    {
        int c0 = F(PW * 0.35), c1 = F(PW * 0.16), c2 = F(PW * 0.16), c3 = F(PW * 0.16);
        int c4 = PW - c0 - F(PW * 0.16) * 3;
        var table = NewSimpleTable(new[] { c0, c1, c2, c3, c4 });

        var hdr = new TableRow();
        hdr.AppendChild(HdrCell("", c0));
        hdr.AppendChild(HdrCell($"CRASHES {py}", c1));
        hdr.AppendChild(HdrCell($"CRASHES {cy}", c2));
        hdr.AppendChild(HdrCell($"FATALITIES {py}", c3));
        hdr.AppendChild(HdrCell($"FATALITIES {cy}", c4));
        table.Append(hdr);

        foreach (var r in routes)
        {
            var tr = new TableRow();
            tr.AppendChild(LabelCell(r.Route, c0, BLACK));
            tr.AppendChild(DataCell(r.CrashesPrev, c1, color: MC.CRASH));
            tr.AppendChild(DataCell(r.CrashesCurr, c2, bold: true, color: MC.CRASH));
            tr.AppendChild(DataCell(r.FatalPrev, c3, color: MC.FATAL));
            tr.AppendChild(DataCell(r.FatalCurr, c4, bold: true, color: MC.FATAL));
            table.Append(tr);
        }
        return table;
    }

    private static Table FiveYearTable(List<YearHistory> hist)
    {
        int lW = F(PW * 0.18);
        var yearsCount = hist.Count;
        int cW = F((PW - lW - F(PW * 0.15)) / (double)yearsCount);
        int aW = PW - lW - cW * yearsCount;

        var widths = new List<int> { lW };
        widths.AddRange(hist.Select(_ => cW));
        widths.Add(aW);
        var table = NewSimpleTable(widths.ToArray());

        var hdr = new TableRow();
        hdr.AppendChild(HdrCell("", lW));
        foreach (var h in hist) hdr.AppendChild(HdrCell(h.Year.ToString(), cW));
        hdr.AppendChild(HdrCell("AVG/MTH", aW));
        table.Append(hdr);

        foreach (var key in new[] { "Crashes", "Fatalities" })
        {
            var mc = key == "Crashes" ? MC.CRASH : MC.FATAL;
            var avg = hist.Average(h => key == "Crashes" ? h.Crashes : h.Fatalities);
            var tr = new TableRow();
            tr.AppendChild(LabelCell(key.ToUpperInvariant(), lW, mc));
            foreach (var h in hist)
            {
                var val = key == "Crashes" ? h.Crashes : h.Fatalities;
                tr.AppendChild(DataCell(val, cW, color: mc));
            }
            tr.AppendChild(DataCell(avg.ToString("F1", CultureInfo.InvariantCulture), aW, bold: true, color: mc));
            table.Append(tr);
        }
        return table;
    }

    private static Table AgeGroupTable(Dictionary<string, int> ageData)
    {
        var groups = new[] { "0-7", "8-12", "13-18", "19-35", "36+" };
        int labelW = F(PW * 0.15);
        int dataW = F((PW - labelW) / (double)groups.Length);
        var widths = new List<int> { labelW };
        widths.AddRange(groups.Select(_ => dataW));
        var table = NewSimpleTable(widths.ToArray());

        var hdr = new TableRow();
        hdr.AppendChild(HdrCell("VICTIMS", labelW));
        foreach (var g in groups) hdr.AppendChild(HdrCell(g, dataW));
        table.Append(hdr);

        var tr = new TableRow();
        tr.AppendChild(LabelCell("TOTAL", labelW, MC.FATAL));
        foreach (var g in groups)
            tr.AppendChild(DataCell(ageData.TryGetValue(g, out var v) ? v : 0, dataW, bold: true, color: MC.FATAL));
        table.Append(tr);
        return table;
    }

    private static Table GenderTable(Dictionary<string, Dictionary<string, int>> genderData)
    {
        var roles = new[] { "Drivers", "Passengers", "Pedestrians", "Cyclists" };
        int labelW = F(PW * 0.35);
        int dataW = F((PW - labelW) / 2.0);
        var table = NewSimpleTable(new[] { labelW, dataW, dataW });

        var hdr = new TableRow();
        hdr.AppendChild(HdrCell("VICTIMS GENDER", labelW));
        hdr.AppendChild(HdrCell("M", dataW));
        hdr.AppendChild(HdrCell("F", dataW));
        table.Append(hdr);

        int totalM = 0, totalF = 0;
        foreach (var role in roles)
        {
            genderData.TryGetValue(role, out var rd);
            int m = rd != null && rd.TryGetValue("Male", out var mv) ? mv : 0;
            int f = rd != null && rd.TryGetValue("Female", out var fv) ? fv : 0;
            totalM += m; totalF += f;
            var tr = new TableRow();
            tr.AppendChild(LabelCell(role.ToUpperInvariant(), labelW, MC.FATAL));
            tr.AppendChild(DataCell(m, dataW, bold: true, color: MC.FATAL));
            tr.AppendChild(DataCell(f, dataW, bold: true, color: MC.FATAL));
            table.Append(tr);
        }
        var totalRow = new TableRow();
        totalRow.AppendChild(LabelCell("TOTAL", labelW, MC.FATAL));
        totalRow.AppendChild(DataCell(totalM, dataW, bold: true, color: MC.FATAL));
        totalRow.AppendChild(DataCell(totalF, dataW, bold: true, color: MC.FATAL));
        table.Append(totalRow);
        return table;
    }

    private static Table TypeOrCategoryTable(string firstHeader,
        IEnumerable<(string Name, int CrashesPrev, int CrashesCurr, int FatalPrev, int FatalCurr)> items, int py, int cy)
    {
        int c0 = F(PW * 0.32), c1 = F(PW * 0.17), c2 = F(PW * 0.17), c3 = F(PW * 0.17);
        int c4 = PW - c0 - F(PW * 0.17) * 3;
        var table = NewSimpleTable(new[] { c0, c1, c2, c3, c4 });

        var hdr = new TableRow();
        hdr.AppendChild(HdrCell(firstHeader, c0));
        hdr.AppendChild(HdrCell($"CRASHES {py}", c1));
        hdr.AppendChild(HdrCell($"CRASHES {cy}", c2));
        hdr.AppendChild(HdrCell($"FATALITIES {py}", c3));
        hdr.AppendChild(HdrCell($"FATALITIES {cy}", c4));
        table.Append(hdr);

        foreach (var it in items)
        {
            var tr = new TableRow();
            tr.AppendChild(LabelCell(it.Name, c0, BLACK));
            tr.AppendChild(DataCell(it.CrashesPrev, c1, color: MC.CRASH));
            tr.AppendChild(DataCell(it.CrashesCurr, c2, bold: true, color: MC.CRASH));
            tr.AppendChild(DataCell(it.FatalPrev, c3, color: MC.FATAL));
            tr.AppendChild(DataCell(it.FatalCurr, c4, bold: true, color: MC.FATAL));
            table.Append(tr);
        }
        return table;
    }

    private static Table TimeTable(List<TimeSlotStats> tSlots, int py, int cy)
    {
        int tW = F(PW * 0.30);
        int cW2 = F((PW - tW) / 4.0);
        int c40 = tW, c41 = cW2, c42 = cW2, c43 = cW2, c44 = PW - tW - cW2 * 3;
        var table = NewSimpleTable(new[] { c40, c41, c42, c43, c44 });

        var hdr1 = new TableRow();
        hdr1.AppendChild(HdrCell("TIME", c40));
        hdr1.AppendChild(HdrCell("CRASHES", c41 + c42, 2));
        hdr1.AppendChild(HdrCell("FATALITIES", c43 + c44, 2));
        table.Append(hdr1);

        var hdr2 = new TableRow();
        hdr2.AppendChild(HdrCell("", c40));
        hdr2.AppendChild(HdrCell(py.ToString(), c41));
        hdr2.AppendChild(HdrCell(cy.ToString(), c42));
        hdr2.AppendChild(HdrCell(py.ToString(), c43));
        hdr2.AppendChild(HdrCell(cy.ToString(), c44));
        table.Append(hdr2);

        foreach (var t in tSlots)
        {
            var tr = new TableRow();
            tr.AppendChild(LabelCell(t.Slot, c40, BLACK));
            tr.AppendChild(DataCell(t.CrashesPrev, c41, fill: "F0F4FF", color: MC.CRASH));
            tr.AppendChild(DataCell(t.CrashesCurr, c42, bold: true, color: MC.CRASH));
            tr.AppendChild(DataCell(t.FatalPrev, c43, fill: "F0F4FF", color: MC.FATAL));
            tr.AppendChild(DataCell(t.FatalCurr, c44, bold: true, color: MC.FATAL));
            table.Append(tr);
        }
        return table;
    }

    private static Table DaysTable(List<DayStats> dd, int py, int cy)
    {
        int c0 = F(PW * 0.25), c1 = F(PW * 0.19), c2 = F(PW * 0.19), c3 = F(PW * 0.19);
        int c4 = PW - c0 - F(PW * 0.19) * 3;
        var table = NewSimpleTable(new[] { c0, c1, c2, c3, c4 });

        var hdr = new TableRow();
        hdr.AppendChild(HdrCell("DAYS", c0));
        hdr.AppendChild(HdrCell($"CRASHES {py}", c1));
        hdr.AppendChild(HdrCell($"CRASHES {cy}", c2));
        hdr.AppendChild(HdrCell($"FATALITIES {py}", c3));
        hdr.AppendChild(HdrCell($"FATALITIES {cy}", c4));
        table.Append(hdr);

        foreach (var day in AllDays)
        {
            var d = dd.FirstOrDefault(x => x.Day == day);
            var tr = new TableRow();
            tr.AppendChild(LabelCell(day, c0));
            tr.AppendChild(DataCell(d?.CrashesPrev ?? 0, c1));
            tr.AppendChild(DataCell(d?.CrashesCurr ?? 0, c2));
            tr.AppendChild(DataCell(d?.FatalPrev ?? 0, c3));
            tr.AppendChild(DataCell(d?.FatalCurr ?? 0, c4));
            table.Append(tr);
        }
        return table;
    }

    

    private static string MetricColor(string label)
    {
        var u = label.ToUpperInvariant();
        if (u.Contains("CRASH")) return MC.CRASH;
        if (u.Contains("FATAL")) return MC.FATAL;
        if (u.Contains("SERIOUS")) return MC.SERIOUS;
        if (u.Contains("SLIGHT")) return MC.SLIGHT;
        return MC.DEFAULT;
    }

    private static int F(double x) => (int)Math.Floor(x);

    private static string Or(string? v, string fallback) => string.IsNullOrEmpty(v) ? fallback : v;

    private static Run R(string text, string font = "Arial", string size = "20", bool bold = false, string color = BLACK)
    {
       
        var rpr = new RunProperties(new RunFonts { Ascii = font, HighAnsi = font });
        if (bold) rpr.AppendChild(new Bold());
        rpr.AppendChild(new FontSize { Val = size });
        rpr.AppendChild(new Color { Val = color });
        return new Run(rpr, new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph P(IEnumerable<Run> runs, JustificationValues? align = null,
        string before = "0", string after = "120", string? indentLeft = null)
    {
        
        var ppr = new ParagraphProperties(
            new Justification { Val = align ?? JustificationValues.Left },
            new SpacingBetweenLines { Before = before, After = after });
        if (indentLeft != null) ppr.AppendChild(new Indentation { Left = indentLeft });
        var para = new Paragraph(ppr);
        foreach (var r in runs) para.AppendChild(r);
        return para;
    }

    private static Paragraph P(Run run, JustificationValues? align = null,
        string before = "0", string after = "120", string? indentLeft = null)
        => P(new[] { run }, align, before, after, indentLeft);

    private static Paragraph Heading(string text) =>
        P(R(text, bold: true, size: "22", color: NAVY), before: "200", after: "80");

    private static Paragraph Blank(int s = 120) => P(R(""), before: "0", after: s.ToString());

    private static Paragraph FigNarrative(string text) => P(R(text, size: "20"), after: "80");

    private static Paragraph SigLine() =>
        new(new ParagraphProperties(
                new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Color = NAVY }),
                new SpacingBetweenLines { Before = "0", After = "60" }),
            R(""));

    private static TableCellBorders CellBorders() => new(
        new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
        new RightBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" });

    private static TableCellMargin CellMargins() => new(
        new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
        new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
        new LeftMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
        new RightMargin { Width = "120", Type = TableWidthUnitValues.Dxa });

    private static TableCellProperties CellProps(int width, string fill, int span = 1, bool vCenter = false)
    {
        var tcp = new TableCellProperties();
        tcp.AppendChild(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
        if (span > 1) tcp.AppendChild(new GridSpan { Val = span });
        tcp.AppendChild(CellBorders());
        tcp.AppendChild(new Shading { Fill = fill, Val = ShadingPatternValues.Clear });
        tcp.AppendChild(CellMargins());
        if (vCenter) tcp.AppendChild(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        return tcp;
    }

    private static TableCell HdrCell(string text, int width, int span = 1)
    {
        var tc = new TableCell(CellProps(width, THEAD, span, vCenter: true));
        tc.AppendChild(P(R(text, bold: true, size: "18", color: WHITE), align: JustificationValues.Center, after: "0"));
        return tc;
    }

    private static TableCell DataCell(string text, int width, string? fill = null, bool bold = false, string color = BLACK)
    {
        var tc = new TableCell(CellProps(width, fill ?? WHITE));
        tc.AppendChild(P(R(text, bold: bold, size: "18", color: color), align: JustificationValues.Center, after: "0"));
        return tc;
    }

    private static TableCell DataCell(int value, int width, string? fill = null, bool bold = false, string color = BLACK)
        => DataCell(value.ToString(), width, fill, bold, color);

    private static TableCell LabelCell(string text, int width, string? color = null)
    {
        var tc = new TableCell(CellProps(width, LGRAY));
        tc.AppendChild(P(R(text, bold: true, size: "18", color: color ?? MetricColor(text)), after: "0"));
        return tc;
    }

    private static Table NewSimpleTable(int[] colWidths)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = PW.ToString(), Type = TableWidthUnitValues.Dxa },
            new TableLayout { Type = TableLayoutValues.Fixed }));
        table.AppendChild(new TableGrid(colWidths.Select(w => (OpenXmlElement)new GridColumn { Width = w.ToString() }).ToArray()));
        return table;
    }

  
    private static string Variation(int? prev, int? curr)
    {
        var p = prev ?? 0;
        var cu = curr ?? 0;
        if (p == 0) return cu == 0 ? "0" : "N/A";
        var diff = cu - p;
        return (cu >= p ? "+" : "") + diff;
    }

   
    private static string FmtPct(string variationText)
    {
        var m = Regex.Match(variationText, @"^[+-]?\d+(\.\d+)?");
        if (!m.Success) return "NaN";
        var v = double.Parse(m.Value, CultureInfo.InvariantCulture);
        return Math.Abs(v).ToString("F2", CultureInfo.InvariantCulture);
    }

    private static string NumWords(int n)
    {
        string[] ones =
        {
            "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
            "seventeen", "eighteen", "nineteen"
        };
        string[] tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
        if (n == 0) return "zero";
        if (n < 20) return ones[n];
        if (n < 100) return tens[n / 10] + (n % 10 != 0 ? " " + ones[n % 10] : "");
        if (n < 1000) return ones[n / 100] + " hundred" + (n % 100 != 0 ? " and " + NumWords(n % 100) : "");
        return n.ToString();
    }

    private static string W(int n) => NumWords(n).ToUpperInvariant();

    private static string Chg(int prev, int curr) => curr >= prev ? "increased" : "decreased";

    private static string JoinList(List<string> arr)
    {
        if (arr.Count == 0) return "";
        if (arr.Count == 1) return arr[0];
        return string.Join(", ", arr.Take(arr.Count - 1)) + " and " + arr[^1];
    }

    private static string ChangedList(IEnumerable<(string Label, int Prev, int Curr)> items)
    {
        var list = items.ToList();
        var up = list.Where(i => i.Curr >= i.Prev).Select(i => i.Label.ToLowerInvariant()).ToList();
        var down = list.Where(i => i.Curr < i.Prev).Select(i => i.Label.ToLowerInvariant()).ToList();
        var parts = new List<string>();
        if (up.Count > 0) parts.Add(JoinList(up) + " increased");
        if (down.Count > 0) parts.Add(JoinList(down) + " decreased");
        return string.Join(" while ", parts);
    }

    
    private sealed class ImageEmbedder
    {
        private readonly MainDocumentPart _mainPart;
        private uint _nextId = 1;

        public ImageEmbedder(MainDocumentPart mainPart) => _mainPart = mainPart;

        public Paragraph ChartPara(byte[] png, int widthPx = 520, int heightPx = 260)
        {
            var drawing = Embed(png, widthPx, heightPx);
            return new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "80", After = "120" }),
                new Run(drawing));
        }

        private Drawing Embed(byte[] pngBytes, int widthPx, int heightPx)
        {
            var imagePart = _mainPart.AddImagePart(ImagePartType.Png);
            using (var stream = new MemoryStream(pngBytes))
                imagePart.FeedData(stream);
            var relId = _mainPart.GetIdOfPart(imagePart);

            var id = _nextId++;
            long widthEmu = widthPx * 9525L;
            long heightEmu = heightPx * 9525L;

            return new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = id, Name = $"Chart{id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = id, Name = $"Chart{id}.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                {
                    DistanceFromTop = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft = 0U,
                    DistanceFromRight = 0U
                });
        }
    }

    
    private static void ApplyPageLayout(MainDocumentPart main)
    {
        var body = main.Document.Body!;
        var secPr = body.Elements<SectionProperties>().FirstOrDefault() ?? body.AppendChild(new SectionProperties());
        secPr.PrependChild(new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134 });
        secPr.PrependChild(new PageSize { Width = 11906, Height = 16838 });
    }
}
