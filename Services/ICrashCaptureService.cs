using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.Services;


public interface ICrashCaptureService
{
   
    Task<CrashSubmitResult> SubmitAsync(string formJson);

    
    Task<bool> UpdateCoreFieldsAsync(int id, Crash formValues);

    
    Task<(bool success, string? duplicateError, int crashId)> CreateCoreOnlyAsync(Crash crash);

  
    Task<bool> DeleteCrashAsync(int crashId);
}
