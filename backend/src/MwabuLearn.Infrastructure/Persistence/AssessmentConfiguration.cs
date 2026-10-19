using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Identity;
using MwabuLearn.Infrastructure.Identity;
using MwabuLearn.Domain.Entities.Organisations;
namespace MwabuLearn.Infrastructure.Persistence;
public partial class MwabuDbContext
{
    public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();
    public DbSet<AssessmentAttempt> AssessmentAttempts => Set<AssessmentAttempt>();
}
public sealed class QuestionBankConfiguration : IEntityTypeConfiguration<QuestionBank>
{
    public void Configure(EntityTypeBuilder<QuestionBank> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Title).HasMaxLength(200).IsRequired(); b.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
        b.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId);
        b.HasOne<ContentAsset>().WithMany().HasForeignKey(x => x.AssetId);
        b.HasIndex(x => new { x.OrganisationId, x.AssetId }).IsUnique();
        b.Property(x => x.IsActive).IsConcurrencyToken();
    }
}
public sealed class AssessmentAttemptConfiguration : IEntityTypeConfiguration<AssessmentAttempt>
{
    public void Configure(EntityTypeBuilder<AssessmentAttempt> b)
    {
        b.ToTable("AssessmentAttempts", t => t.HasCheckConstraint("CK_AssessmentAttempts_Score", "\"Score\" >= 0 AND \"Score\" <= \"Total\" AND \"Total\" BETWEEN 1 AND 10"));
        b.HasKey(x => x.Id); b.Property(x => x.SubmissionHash).HasMaxLength(64).IsRequired(); b.Property(x => x.CorrectJson).HasMaxLength(100).IsRequired();
        b.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.LearnerUserId);
        b.HasOne<QuestionBank>().WithMany().HasForeignKey(x => x.QuestionBankId);
        b.HasIndex(x => new { x.OrganisationId, x.LearnerUserId, x.SubmissionKey }).IsUnique();
        b.HasIndex(x => x.ExpiresAt);
    }
}
