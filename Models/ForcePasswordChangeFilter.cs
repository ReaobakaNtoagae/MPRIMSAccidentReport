using CrashReport.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CrashReport.Security;

// Registered globally in Program.cs, alongside the existing "must be logged in"
// AuthorizeFilter. For any authenticated request whose action isn't on the
// allow-list below, checks the signed-in user's MustChangePassword flag —
// read fresh from the database every request via UserManager.GetUserAsync,
// never from a claim on the auth cookie, which would stay stale for up to the
// full 8-hour sliding-expiration window after the user actually changes it —
// and redirects to the change-password screen if it's still set.
//
// Applies to every controller, including every [ApiController] under Controllers/Api/
// added alongside the MVC ones. Requests under /api/ get a 403 JSON response instead
// of the MVC redirect below — a fetch() call can't follow a redirect into a rendered
// HTML login/change-password view, so forcing that on an API caller would just look
// like an opaque failure. This doesn't fully close remediation item 6 (consistent
// auth-failure responses across the app generally) but stops this specific filter
// from making it worse for the new API surface.
public class ForcePasswordChangeFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> AllowedWhileMustChange = new(StringComparer.OrdinalIgnoreCase)
    {
        "Account.ChangePassword",
        "Account.Logout",
        // The AccountApi controller (Controllers/Api/AccountApiController.cs) is the
        // JSON equivalent of AccountController — same two actions need the same
        // exemption, or a signed-in user could never reach the endpoint that lets
        // them clear MustChangePassword via the API.
        "AccountApi.ChangePassword",
        "AccountApi.Logout",
    };

    private readonly UserManager<ApplicationUser> _userManager;

    public ForcePasswordChangeFilter(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated == true
            && context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var key = $"{descriptor.ControllerName}.{descriptor.ActionName}";

            if (!AllowedWhileMustChange.Contains(key))
            {
                var dbUser = await _userManager.GetUserAsync(user);
                if (dbUser?.MustChangePassword == true)
                {
                    context.Result = context.HttpContext.Request.Path.StartsWithSegments("/api")
                        ? new ObjectResult(new { message = "Password change required before continuing.", code = "MUST_CHANGE_PASSWORD" })
                            { StatusCode = StatusCodes.Status403Forbidden }
                        : new RedirectToActionResult("ChangePassword", "Account", null);
                    return;
                }
            }
        }

        await next();
    }
}
