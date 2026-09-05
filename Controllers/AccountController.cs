using growcery.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace growcery.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost]
    [AllowAnonymous]
    [Route("register")]
    public async Task<ActionResult<AccountResponse>> Register(RegisterViewModel model)
    {
        var user = new ApplicationUser { UserName = model.UserName };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Created(string.Empty, new AccountResponse("Registration successful.", user.UserName!));
        }

        return BadRequest(new ValidationErrorResponse(result.Errors
            .Select(error => new ValidationError(error.Code, error.Description))));
    }

    [HttpPost]
    [AllowAnonymous]
    [Route("login")]
    public async Task<ActionResult<AccountResponse>> Login(LoginViewModel model)
    {
        var result = await _signInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return Ok(new AccountResponse("Login successful.", model.UserName));
        }

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new ErrorResponse("This account has been locked out. Please try again later."));
        }

        return Unauthorized(new ErrorResponse("Invalid username or password."));
    }

}
