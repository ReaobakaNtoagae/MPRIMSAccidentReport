using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.ViewModels;

public class EditSummaryViewModel
{
    public CrashSummary Summary { get; set; } = null!;
    public List<VehicleEntryInput> Vehicles { get; set; } = new();
    public List<InjuryEntryInput> Injuries { get; set; } = new();
}