namespace MwabuLearn.Application.Education;
public sealed record DevelopmentSeedResult(Guid SchoolId, Guid ProgrammeOrganisationId, Guid ProjectId, IReadOnlyList<string> AccountEmails);
public interface IDevelopmentSeed { Task<DevelopmentSeedResult> RunAsync(CancellationToken ct); }
