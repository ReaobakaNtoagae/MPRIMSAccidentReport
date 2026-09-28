using CrashReport.Models;

namespace CrashReport.Models.Dtos;

/// <summary>
/// Moved out of CreateSummaryController.cs (where they previously lived as public
/// classes outside the controller class, but still in the CrashReport.Controllers
/// namespace/file) into a real shared location. Before this move,
/// ViewModels/EditSummaryViewmodel.cs had to `using CrashReport.Controllers;` just
/// to reference these two types — a ViewModel depending on a Controller namespace,
/// which is backwards. Both CreateSummaryController (Quick Capture create) and
/// CrashesController.EditSummary (Quick Capture edit) — and now the API
/// controllers and ICrashSummaryValidationService — reference these from here.
/// </summary>
public class VehicleEntryInput
{
    public byte VehicleNumber { get; set; }
    public string VehicleTypeCode { get; set; } = "";
    public string VehicleTypeName { get; set; } = "";
    public string? Make { get; set; }
    public string? Registration { get; set; }
}

public class InjuryEntryInput
{
    public string Severity { get; set; } = "";
    public string? Role { get; set; }
    public byte? VehicleNumber { get; set; } // null for Pedestrian/Cyclist
    public int? Age { get; set; }
    public string? AgeGroupCode { get; set; }
    public string? Gender { get; set; }
    public string? Race { get; set; }
}

/// <summary>
/// The JSON-API request shape for both "create a Quick Capture summary"
/// (CreateSummaryApiController) and "edit one" (CrashesApiController's summary
/// endpoints) -- the MVC actions this mirrors post vehiclesJson/injuriesJson as
/// JSON-encoded form-field strings plus two Request.Form marker keys
/// ("RoleBasedInjuryEditor", "TotalsOnlyEditor"); this flattens that into one
/// ordinary request body instead.
/// </summary>
public class QuickCaptureSummaryRequest
{
    public CrashSummary Summary { get; set; } = null!;
    public List<VehicleEntryInput> Vehicles { get; set; } = new();
    public List<InjuryEntryInput> Injuries { get; set; } = new();

    /// <summary>The new role-based Quick Capture screen sends every severity broken down by road-user role.</summary>
    public bool RoleBasedEditor { get; set; }

    /// <summary>Compatibility marker for the legacy totals-only Quick Capture screen (edit only).</summary>
    public bool TotalsOnlyEditor { get; set; }
}
