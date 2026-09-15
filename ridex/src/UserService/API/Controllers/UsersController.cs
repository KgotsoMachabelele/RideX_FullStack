using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Commands;

namespace UserService.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(Guid), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return Created($"/api/users/{id}", new { id });
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand cmd, CancellationToken ct)
        => Ok(await mediator.Send(cmd, ct));

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId    = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email     = User.FindFirstValue(ClaimTypes.Email);
        var firstName = User.FindFirstValue("firstName");
        var role      = User.FindFirstValue(ClaimTypes.Role);
        return Ok(new { userId, email, firstName, role });
    }
}
