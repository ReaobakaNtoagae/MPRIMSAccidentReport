using CrashReport.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CrashReport.Controllers;

// No longer class-level [AllowAnonymous] — ChangePassword below needs to require
// login (it's gated by the global AuthorizeFilter like everything else now), and
// [AllowAnonymous] anywhere in a controller's scope overrides any [Authorize] in
// that same scope regardless of where each is placed, so it has to come off the
// class and go on the individual actions that actually need it instead.
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;

    public AccountController(
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users)
    {
        _signIn = signIn;
        _users = users;
    }


    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signIn.IsSignedIn(User))
            return RedirectToAction("Index", "Home");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] bool rememberMe = false,
        [FromForm] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TempData["LoginError"] = "Email and password are required.";
            return View();
        }

        var user = await _users.FindByEmailAsync(email.Trim());

        if (user == null || !user.IsActive)
        {
            TempData["LoginError"] = "Invalid email or password.";
            return View();
        }

        var result = await _signIn.PasswordSignInAsync(
            user, password, rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {

            var existingClaims = await _users.GetClaimsAsync(user);
            if (!existingClaims.Any(c => c.Type == "FullName"))
                await _users.AddClaimAsync(user, new Claim("FullName", user.FullName));
            else
            {

                var existing = existingClaims.First(c => c.Type == "FullName");
                if (existing.Value != user.FullName)
                {
                    await _users.RemoveClaimAsync(user, existing);
                    await _users.AddClaimAsync(user, new Claim("FullName", user.FullName));
                }
            }

            await _signIn.RefreshSignInAsync(user);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            TempData["LoginError"] =
                "Your account has been locked after too many failed attempts. " +
                "Please try again in 15 minutes.";
            return View();
        }

        TempData["LoginError"] = "Invalid email or password.";
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }


    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // No [AllowAnonymous] here on purpose — this one needs the global
    // AuthorizeFilter to require a signed-in user, since it operates on
    // "the currently logged-in account's own password."
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        [FromForm] string currentPassword,
        [FromForm] string newPassword,
        [FromForm] string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["ChangePasswordError"] = "All fields are required.";
            return View();
        }

        if (newPassword != confirmPassword)
        {
            TempData["ChangePasswordError"] = "New password and confirmation don't match.";
            return View();
        }

        var user = await _users.GetUserAsync(User);
        if (user == null)
            return RedirectToAction(nameof(Login));

        var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            TempData["ChangePasswordError"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return View();
        }

        user.MustChangePassword = false;
        await _users.UpdateAsync(user);

        // ChangePasswordAsync rotates the security stamp, which — depending on
        // SecurityStampValidationInterval — can otherwise sign this session out
        // on its very next request. Refresh now so the user who just changed
        // their password isn't immediately bounced back to the login page.
        await _signIn.RefreshSignInAsync(user);

        return RedirectToAction("Index", "Home");
    }
}