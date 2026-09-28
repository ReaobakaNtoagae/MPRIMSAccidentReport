using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.Services;

/// <summary>
/// CrashesController.Grid's filter/sort/page pipeline over MonthlyMemoDataService's
/// merged Row list, extracted so the MVC action and the new CrashesApiController
/// can share it instead of the API controller re-implementing the same nine filter
/// clauses and the column-sort switch.
/// </summary>
public interface ICrashGridService
{
    Task<CrashGridResult> BuildAsync(CrashGridFilter filter);
}
