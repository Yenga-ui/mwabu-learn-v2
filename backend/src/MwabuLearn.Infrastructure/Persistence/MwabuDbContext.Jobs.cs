using Microsoft.EntityFrameworkCore;
using MwabuLearn.Domain.Entities.Content;
using MwabuLearn.Domain.Entities.Operations;
namespace MwabuLearn.Infrastructure.Persistence;

public partial class MwabuDbContext
{
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    private void PrepareJobs()
    {
        foreach (var entry in ChangeTracker.Entries<ContentAsset>().Where(x =>
            x.Entity.IsPendingDeletion && (x.State == EntityState.Added || x.State == EntityState.Modified && x.Property(a => a.IsPendingDeletion).IsModified)).ToArray())
        {
            var asset = entry.Entity;
            if (BackgroundJobs.Local.Any(x => x.EntityId == asset.Id && x.Type == "asset.delete")) continue;
            BackgroundJobs.Add(new BackgroundJob { Type = "asset.delete", EntityId = asset.Id, DeduplicationKey = $"asset-delete:{asset.Id:N}" });
        }
    }
}
