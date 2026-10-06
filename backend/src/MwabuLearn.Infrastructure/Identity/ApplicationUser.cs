using Microsoft.AspNetCore.Identity;

namespace MwabuLearn.Infrastructure.Identity;

// Credential infrastructure stays outside Domain. Business memberships reference its GUID via an EF foreign key.
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() => Id = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int AccessTokenVersion { get; set; }
}
