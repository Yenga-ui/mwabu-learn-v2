using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MwabuLearn.Application.Directories;
namespace MwabuLearn.Infrastructure.Curricula;

public sealed partial class CurriculumService
{
    private async Task<IDbContextTransaction?> ReadSnapshot(CancellationToken ct) => db.Database.CurrentTransaction is not null ? null :
        await db.Database.BeginTransactionAsync(db.Database.IsNpgsql() ? System.Data.IsolationLevel.RepeatableRead : System.Data.IsolationLevel.Serializable, ct);
}
