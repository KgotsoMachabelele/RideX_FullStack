using FluentValidation;
using MediatR;
using UserService.Domain.Entities;
using UserService.Domain.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Application.Commands;

// ── Command ──────────────────────────────────────────────────────────────────
public record RegisterUserCommand(
    string Email, string Password,
    string FirstName, string LastName,
    string PhoneNumber) : IRequest<Guid>;

// ── Validator ─────────────────────────────────────────────────────────────────
public class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator(IUserRepository repo)
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress()
            .MustAsync(async (e, ct) => !await repo.ExistsByEmailAsync(e, ct))
            .WithMessage("Email is already registered.");
        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Must contain a digit.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+?[1-9]\d{9,14}$");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────
public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, Guid>
{
    private readonly IUserRepository _repo;
    private readonly IPasswordHasher _hasher;

    public RegisterUserHandler(IUserRepository repo, IPasswordHasher hasher)
    { _repo = repo; _hasher = hasher; }

    public async Task<Guid> Handle(RegisterUserCommand cmd, CancellationToken ct)
    {
        var hash = _hasher.Hash(cmd.Password);
        var user = User.Create(cmd.Email, hash, cmd.FirstName, cmd.LastName, cmd.PhoneNumber);
        await _repo.AddAsync(user, ct);
        return user.Id;
    }
}
