namespace CrashReport.Models.Dtos;

public enum CrashSubmitOutcome
{
    /// <summary>No formJson was posted at all.</summary>
    EmptyForm,
    /// <summary>CrNo normalization threw, or ValidateAsync / the duplicate-CrNo check found problems.</summary>
    ValidationFailed,
    /// <summary>The transaction committed.</summary>
    Success,
    /// <summary>An exception was thrown while saving; the transaction was rolled back.</summary>
    SaveFailed,
}

/// <summary>
/// What HomeController.Submit used to encode entirely as TempData + a redirect
/// target, made explicit so both the MVC action (which still sets that same
/// TempData and redirects) and the new JSON API controller (which just returns
/// this as a response body) can share one save path.
/// </summary>
public class CrashSubmitResult
{
    public CrashSubmitOutcome Outcome { get; init; }
    public List<string> ValidationErrors { get; init; } = new();

    /// <summary>
    /// The formJson as it stood at the moment of failure — normalized (CrNo
    /// reformatted) if normalization succeeded before a later validation error,
    /// or the original posted value if normalization itself failed. Only
    /// populated on ValidationFailed, matching TempData["FormJson"]'s previous role
    /// of letting the Create form redisplay exactly what the user typed.
    /// </summary>
    public string? FormJsonForRedisplay { get; init; }

    public string? ErrorMessage { get; init; }
    public int CrashId { get; init; }
    public string? CrNo { get; init; }

    /// <summary>
    /// True if this CrNo also exists as a Quick Add / imported CrashSummary — not
    /// a failure, just a reconciliation note (see the long comment in the service
    /// for why this is allow-and-warn rather than block).
    /// </summary>
    public bool ExistsAsSummary { get; init; }

    public string SuccessMessage => ExistsAsSummary
        ? $"Crash report #{CrashId} saved successfully. Note: CR number '{CrNo}' also exists as a Quick Add / " +
          "imported record — both are saved; reports won't double-count them, but you may want to reconcile the two records."
        : $"Crash report #{CrashId} saved successfully.";
}

/// <summary>
/// Mirrors exactly the field list HomeController.Edit's [Bind(...)] attribute
/// allow-lists — the only fields that action (or its API equivalent) is willing to
/// update. Deliberately narrow: this is a thin top-level edit, not a re-run of the
/// full capture save.
/// </summary>
public class CrashCoreFieldsUpdateRequest
{
    public string? CasNo { get; set; }
    public string? CrNo { get; set; }
    public string? IncidentReportNo { get; set; }
    public string? CapturingNumber { get; set; }
    public DateOnly? CrashDate { get; set; }
    public TimeOnly? CrashTime { get; set; }
    public byte NoOfAppendices { get; set; }
    public byte NoOfVehiclesInvolved { get; set; }
    public string? ProvinceCode { get; set; }
    public short SpeedLimitKmh { get; set; }
    public string? RoadNumber { get; set; }
    public string? KmMarker { get; set; }
    public string? BriefDescription { get; set; }
}
