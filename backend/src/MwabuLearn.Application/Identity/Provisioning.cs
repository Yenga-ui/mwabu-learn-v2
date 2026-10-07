namespace MwabuLearn.Application.Identity;
public sealed record ProvisionedUser(UserResponse User, MembershipResponse Membership);
public interface IOrganisationProvisioning
{
    Task<ProvisionedUser> CreateAsync(Guid organisationId, CreateUserRequest request, CancellationToken ct);
}
