using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CrashReport.Controllers.Api;

[Route("api/account")]
[ApiController]
public class AccountApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IUserClaimsSyncService _claimsSync;

    public AccountApiController(
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users,
        IUserClaimsSyncService claimsSync)
    {
        _signIn = signIn;
        _users = users;
        _claimsSync = claimsSync;
    }


    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email.Trim());
        if (user == null || !user.IsActive)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await _signIn.PasswordSignInAsync(user, req.Password, req.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _claimsSync.SyncFullNameClaimAsync(user);
            await _signIn.RefreshSignInAsync(user);

            var roles = await _users.GetRolesAsync(user);
            return Ok(new
            {
                success = true,
                user.Id,
                user.FullName,
                user.Email,
                mustChangePassword = user.MustChangePassword,
                roles
            });
        }

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "Your account has been locked after too many failed attempts. Please try again in 15 minutes."
            });

        return Unauthorized(new { message = "Invalid email or password." });
    }

    // POST api/account/logout — [AllowAnonymous] to mirror AccountController.Logout
    // exactly (it's reachable even without a live session there too).
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return NoContent();
    }

    // GET api/account/access-denied
    [HttpGet("access-denied")]
    [AllowAnonymous]
    public IActionResult AccessDenied() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to access this resource." });

    
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (req.NewPassword != req.ConfirmPassword)
            return BadRequest(new { message = "New password and confirmation don't match." });

        var user = await _users.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var result = await _users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        user.MustChangePassword = false;
        await _users.UpdateAsync(user);

        
        await _signIn.RefreshSignInAsync(user);

        return Ok(new { success = true });
    }
}
