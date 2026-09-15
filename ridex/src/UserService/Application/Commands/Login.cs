using MediatR;
using UserService.Domain.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Application.Commands;

public record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;
public record LoginResponse(Guid UserId, string Token, string Role, string FirstName, DateTime ExpiresAt);

public class LoginHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IUserRepository _repo;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService     _jwt;

    public LoginHandler(IUserRepository repo, IPasswordHasher hasher, IJwtService jwt)
    { _repo = repo; _hasher = hasher; _jwt = jwt; }

    public async Task<LoginResponse> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await _repo.GetByEmailAsync(cmd.Email, ct)
                   ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!_hasher.Verify(cmd.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var (token, expiry) = _jwt.GenerateToken(user);
        return new LoginResponse(user.Id, token, user.Role, user.FirstName, expiry);
    }
}
