using CrashReport.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CrashReport.Controllers;

[AllowAnonymous]
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

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signIn.IsSignedIn(User))
            return RedirectToAction("Index", "Home");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
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
            // ── Add FullName as a claim so the layout can display it
            //    without a database hit on every request.
            var existingClaims = await _users.GetClaimsAsync(user);
            if (!existingClaims.Any(c => c.Type == "FullName"))
                await _users.AddClaimAsync(user, new Claim("FullName", user.FullName));
            else
            {
                // Keep the claim value in sync if the name was updated
                var existing = existingClaims.First(c => c.Type == "FullName");
                if (existing.Value != user.FullName)
                {
                    await _users.RemoveClaimAsync(user, existing);
                    await _users.AddClaimAsync(user, new Claim("FullName", user.FullName));
                }
            }

            // Re-sign so the new claim is in the cookie immediately
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

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied() => View();
}