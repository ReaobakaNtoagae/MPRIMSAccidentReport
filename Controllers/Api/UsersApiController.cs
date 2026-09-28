using CrashReport.Models;
using CrashReport.Models.Dtos;
using CrashReport.Security;
using CrashReport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrashReport.Controllers.Api;


[Route("api/users")]
[ApiController]
[Authorize(Policy = Privileges.Admin.Users)]
public class UsersApiController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly IUserAdminService _userAdmin;

    public UsersApiController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        IUserAdminService userAdmin)
    {
        _users = users;
        _roles = roles;
        _userAdmin = userAdmin;
    }

    // GET api/users
    [HttpGet]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _users.Users.ToListAsync();
        var currentUserId = _users.GetUserId(User);

        var data = new List<object>();
        foreach (var u in users)
        {
            var roles = await _users.GetRolesAsync(u);
            data.Add(new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.UserName,
                u.District,
                u.IsActive,
                u.CreatedAt,
                Roles = roles,
                IsSelf = currentUserId == u.Id
            });
        }
        return Ok(data);
    }

    // GET api/users/roles — role name list for dropdowns, sourced from AspNetRoles.
    [HttpGet("roles")]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> GetRoles()
    {
        var names = await _roles.Roles
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync();

        return Ok(names);
    }

    // GET api/users/{id}
    [HttpGet("{id}")]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        var currentRoles = await _users.GetRolesAsync(user);
        var currentUserId = _users.GetUserId(User);

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.District,
            user.IsActive,
            Role = currentRoles.FirstOrDefault(),
            IsSelf = currentUserId == user.Id
        });
    }

    // POST api/users
    [HttpPost]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var result = await _userAdmin.CreateUserAsync(req.FullName, req.Email, req.District, req.Role, req.Password);

        return result.Outcome switch
        {
            UserAdminOutcome.Success => Created($"/api/users/{result.User!.Id}", new
            {
                result.User.Id,
                result.User.FullName,
                result.User.Email,
                result.User.District,
                result.User.IsActive
            }),
            UserAdminOutcome.EmailTaken => Conflict(new { message = $"A user with email {req.Email} already exists." }),
            UserAdminOutcome.RoleInvalid => BadRequest(new { message = $"Role '{req.Role}' does not exist." }),
            UserAdminOutcome.IdentityError => BadRequest(new { errors = result.Errors }),
            _ => BadRequest()
        };
    }

    // PUT api/users/{id}
    [HttpPut("{id}")]
    [Authorize(Policy = Privileges.Admin.Users)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest req)
    {
        var currentUserId = _users.GetUserId(User);
        var result = await _userAdmin.UpdateUserAsync(
            id, req.FullName, req.Email, req.District, req.Role, req.IsActive, req.NewPassword, currentUserId);

        return result.Outcome switch
        {
            UserAdminOutcome.Success => Ok(new
            {
                result.User!.Id,
                result.User.FullName,
                result.User.Email,
                result.User.District,
                result.User.IsActive
            }),
            UserAdminOutcome.NotFound => NotFound(),
            UserAdminOutcome.EmailTaken => Conflict(new { message = $"A user with email {req.Email} already exists." }),
            UserAdminOutcome.RoleInvalid => BadRequest(new { message = $"Role '{req.Role}' does not exist." }),
            UserAdminOutcome.CannotDeactivateSelf => BadRequest(new { message = "You cannot deactivate your own account." }),
            UserAdminOutcome.IdentityError => BadRequest(new { errors = result.Errors }),
            _ => BadRequest()
        };
    }
}
