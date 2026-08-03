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

   
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signIn.IsSignedIn(User))
            return RedirectToAction("Index", "Home");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

   
    [HttpGet]
    public IActionResult AccessDenied() => View();
}