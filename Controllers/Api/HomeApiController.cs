using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers.Api;


[Route("api/crash-capture")]
[ApiController]
public class HomeApiController : ControllerBase
{
    private readonly ICrashCaptureService _capture;

    public HomeApiController(ICrashCaptureService capture)
    {
        _capture = capture;
    }

    
    [HttpPost("submit")]
    [Authorize(Policy = Privileges.Crashes.Create)]
    public async Task<IActionResult> Submit([FromForm] string formJson)
    {
        var result = await _capture.SubmitAsync(formJson);

        return result.Outcome switch
        {
            CrashSubmitOutcome.EmptyForm =>
                BadRequest(new { message = "No form data received." }),

            CrashSubmitOutcome.ValidationFailed =>
                BadRequest(new { errors = result.ValidationErrors, formJson = result.FormJsonForRedisplay }),

            CrashSubmitOutcome.SaveFailed =>
                StatusCode(StatusCodes.Status500InternalServerError, new { message = result.ErrorMessage }),

            _ => Created($"/api/crashes/{result.CrashId}", new
            {
                crashId = result.CrashId,
                crNo = result.CrNo,
                message = result.SuccessMessage,
                existsAsSummary = result.ExistsAsSummary
            })
        };
    }
}
