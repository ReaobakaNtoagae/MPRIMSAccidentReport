using CrashReport.Models;
using CrashReport.Models.Dtos;

namespace CrashReport.Services;


public interface ICrashGridService
{
    Task<CrashGridResult> BuildAsync(CrashGridFilter filter);
}
