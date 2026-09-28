using System.ComponentModel.DataAnnotations;

namespace CrashReport.Models.Dtos;

/// <summary>
/// Request bodies for ImportApiController. These carry only the editable fields the
/// corresponding Services/Import commands need — BatchId/StagingSummaryId/IssueId
/// come from the route, not the body, since the API expresses them as resource path
/// segments rather than hidden form fields the way the MVC views did.
/// </summary>
public sealed class UpdateImportRowRequest
{
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

public sealed class RowDecisionRequest
{
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class ResolveIssueRequest
{
    [Required] public string Decision { get; set; } = string.Empty;
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class ReferIssueRequest
{
    [Required, StringLength(200)] public string ReferredTo { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Question { get; set; } = string.Empty;
    public DateTime? ResponseDueAt { get; set; }
}

public sealed class DataOwnerResponseRequest
{
    [Required, StringLength(1000)] public string Response { get; set; } = string.Empty;
}
