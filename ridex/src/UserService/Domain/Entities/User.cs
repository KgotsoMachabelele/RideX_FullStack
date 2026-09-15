namespace UserService.Domain.Entities;

public class User
{
    public Guid   Id           { get; private set; }
    public string Email        { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName    { get; private set; } = string.Empty;
    public string LastName     { get; private set; } = string.Empty;
    public string PhoneNumber  { get; private set; } = string.Empty;
    public string Role         { get; private set; } = "User";
    public string? PaymentToken{ get; private set; }
    public bool   IsActive     { get; private set; } = true;
    public DateTime CreatedAt  { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash,
        string firstName, string lastName, string phone) => new()
    {
        Id           = Guid.NewGuid(),
        Email        = email.ToLowerInvariant(),
        PasswordHash = passwordHash,
        FirstName    = firstName,
        LastName     = lastName,
        PhoneNumber  = phone,
        CreatedAt    = DateTime.UtcNow
    };

    public void UpdatePaymentToken(string token) => PaymentToken = token;
    public void Deactivate() => IsActive = false;
}
