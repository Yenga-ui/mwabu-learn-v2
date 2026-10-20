using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Infrastructure.Identity;
namespace MwabuLearn.Infrastructure.Persistence.Configurations;

public sealed class ContentImportBatchConfiguration : IEntityTypeConfiguration<ContentImportBatch>
{
    public void Configure(EntityTypeBuilder<ContentImportBatch> b)
    {
        b.ToTable("ContentImportBatches"); b.HasKey(x => x.Id);
        b.Property(x => x.SourceCollection).HasMaxLength(100); b.Property(x => x.ArchiveChecksum).HasMaxLength(64);
        b.Property(x => x.FileName).HasMaxLength(255); b.Property(x => x.StorageKey).HasMaxLength(500);
        b.Property(x => x.ReviewEvidence).HasMaxLength(2000);
        b.HasIndex(x => new { x.SourceCollection, x.CurriculumYear, x.ArchiveChecksum }).IsUnique();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId);
    }
}
public sealed class ContentImportRecordConfiguration : IEntityTypeConfiguration<ContentImportRecord>
{
    public void Configure(EntityTypeBuilder<ContentImportRecord> b)
    {
        b.ToTable("ContentImportRecords"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Batch).WithMany(x => x.Records).HasForeignKey(x => x.BatchId);
        b.HasOne(x => x.ContentItem).WithMany().HasForeignKey(x => x.ContentItemId);
        b.Property(x => x.SourcePath).HasMaxLength(1000); b.Property(x => x.Checksum).HasMaxLength(64);
        b.Property(x => x.State).HasMaxLength(30); b.Property(x => x.Format).HasMaxLength(100); b.Property(x => x.Issue).HasMaxLength(2000);
        b.Property(x => x.ReviewEvidence).HasMaxLength(2000); b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerId);
        b.HasIndex(x => new { x.BatchId, x.SourcePath }).IsUnique();
    }
}
public sealed class ImportedLessonTemplateConfiguration : IEntityTypeConfiguration<ImportedLessonTemplate>
{
    public void Configure(EntityTypeBuilder<ImportedLessonTemplate> b)
    {
        b.ToTable("ImportedLessonTemplates"); b.HasKey(x => x.Id);
        b.HasOne(x => x.ContentItem).WithMany().HasForeignKey(x => x.ContentItemId);
        b.HasOne<ContentImportRecord>().WithMany().HasForeignKey(x => x.ImportRecordId);
        b.HasIndex(x => x.ContentItemId).IsUnique();
        b.HasIndex(x => new { x.CurriculumYear, x.OriginalChecksum }).IsUnique();
        b.Property(x => x.OriginalChecksum).HasMaxLength(64); b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.Topic).HasMaxLength(200); b.Property(x => x.SubTopic).HasMaxLength(500); b.Property(x => x.Term).HasMaxLength(100);
        b.HasIndex(x => new { x.CurriculumYear, x.Grade, x.Subject });
    }
}
public sealed class ImportedLessonResourceConfiguration : IEntityTypeConfiguration<ImportedLessonResource>
{
    public void Configure(EntityTypeBuilder<ImportedLessonResource> b)
    {
        b.ToTable("ImportedLessonResources"); b.HasKey(x => x.Id);
        b.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId);
        b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerId);
        b.Property(x => x.Checksum).HasMaxLength(64); b.Property(x => x.Evidence).HasMaxLength(2000);
        b.Property(x => x.SourceDependency).HasMaxLength(500);
        b.HasIndex(x => new { x.TemplateId, x.AssetId }).IsUnique();
    }
}
