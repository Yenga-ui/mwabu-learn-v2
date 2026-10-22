namespace MwabuLearn.Domain.Entities.Identity;

// Each published policy is immutable; registrations retain the exact accepted version.
public sealed class RegistrationPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Checksum { get; set; } = "";
    // Applies only to authenticated adult-managed requests; never enables learner self-registration.
    public bool AllowLearnerRegistration { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public Guid PublishedBy { get; set; }
}
