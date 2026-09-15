using Microsoft.EntityFrameworkCore;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(u =>
        {
            u.HasKey(x => x.Id);
            u.Property(x => x.Email).HasMaxLength(256).IsRequired();
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.PhoneNumber).HasMaxLength(20);
            u.Property(x => x.Role).HasMaxLength(50).HasDefaultValue("User");
        });
    }
}
