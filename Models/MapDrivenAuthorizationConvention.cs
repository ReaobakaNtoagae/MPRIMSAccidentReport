using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace CrashReport.Security;

/// <summary>
/// Registered once in Program.cs, alongside the existing global "must be logged in"
/// filter. For every action in the app, at startup:
///
///   1. If the action or its controller already carries an explicit [Authorize] or
///      [AllowAnonymous] attribute, it's left completely alone — that attribute is
///      the source of truth, same as before this convention existed.
///   2. Otherwise, it's looked up in ActionPrivilegeMap (an exact action override,
///      then a controller-wide default, then the explicit AuthenticatedOnly
///      allow-list) and the matching policy is added as an AuthorizeFilter.
///   3. If it's in none of those, the app throws at startup and refuses to run.
///
/// The net effect: every action in the solution ends up covered by *something* —
/// its own attribute, or this map — and a new action added later without either
/// is caught immediately (a startup crash, not a silent gap discovered later)
/// rather than quietly inheriting only "logged in, any role, full access."
///
/// This deliberately does NOT touch actions that already have their own
/// [Authorize(Policy = ...)] — it only fills the gaps. That keeps this a small,
/// additive change instead of a rewrite of every controller that's already
/// correct, and avoids two policies silently stacking (AND-combining) on the same
/// action if the map ever disagreed with an existing attribute.
/// </summary>
public class MapDrivenAuthorizationConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        var controllerHasOwnAuthorization =
            controller.Attributes.OfType<IAuthorizeData>().Any() ||
            controller.Attributes.OfType<IAllowAnonymous>().Any();

        foreach (var action in controller.Actions)
        {
            if (controllerHasOwnAuthorization ||
                action.Attributes.OfType<IAuthorizeData>().Any() ||
                action.Attributes.OfType<IAllowAnonymous>().Any())
            {
                continue; // already decided by an explicit attribute — not this convention's business
            }

            var key = $"{controller.ControllerName}.{action.ActionName}";

            if (ActionPrivilegeMap.ActionOverrides.TryGetValue(key, out var actionPolicy))
            {
                action.Filters.Add(new AuthorizeFilter(actionPolicy));
            }
            else if (ActionPrivilegeMap.ControllerDefaults.TryGetValue(controller.ControllerName, out var controllerPolicy))
            {
                action.Filters.Add(new AuthorizeFilter(controllerPolicy));
            }
            else if (ActionPrivilegeMap.AuthenticatedOnly.Contains(key))
            {
                // Intentionally a no-op — the global RequireAuthenticatedUser()
                // filter in Program.cs already covers this action.
            }
            else
            {
                throw new InvalidOperationException(
                    $"{key} has no [Authorize]/[AllowAnonymous] attribute and isn't in " +
                    $"ActionPrivilegeMap. Add an attribute to the action, or add \"{key}\" " +
                    "to ActionPrivilegeMap (a specific privilege, a controller default, or " +
                    "AuthenticatedOnly if it's deliberately open to any logged-in user) " +
                    "before this endpoint can exist.");
            }
        }
    }
}
