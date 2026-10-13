using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Infrastructure.Lessons;
internal static class LessonTransactions
{
    public static Task<T> Run<T>(MwabuDbContext db, Func<Task<T>> action, CancellationToken ct) => db.Database.CurrentTransaction is null
        ? Identity.IdentityValidation.Transaction(db, action, ct) : action();
}
