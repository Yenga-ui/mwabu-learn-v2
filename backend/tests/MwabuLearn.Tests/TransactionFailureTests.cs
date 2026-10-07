using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Identity;
using Npgsql;

namespace MwabuLearn.Tests;

public sealed class TransactionFailureTests
{
    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public Exception? Error { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Error is null ? ValueTask.FromResult(result) : throw Error;
    }
    [Theory]
    [InlineData(PostgresErrorCodes.SerializationFailure, true)]
    [InlineData(PostgresErrorCodes.DeadlockDetected, true)]
    [InlineData(PostgresErrorCodes.InternalError, false)]
    [InlineData(PostgresErrorCodes.ConnectionFailure, false)]
    public async Task Rotation_translates_only_expected_wrapped_database_concurrency(string state, bool conflict)
    {
        var interceptor = new SaveFailure(); using var env = new IdentityTestEnvironment(interceptor);
        var password = TestSecurityConfiguration.StrongPassword(); var user = await env.CreateUser(password: password);
        var login = await env.Run(sp => sp.GetRequiredService<IAuthenticationService>().LoginAsync(new LoginRequest { Email = user.Email, Password = password }, default));
        interceptor.Error = new InvalidOperationException("Provider wrapper", new DbUpdateException("Save wrapper", new PostgresException("Test failure", "ERROR", "ERROR", state)));
        var thrown = await Record.ExceptionAsync(() => env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(login.RefreshToken!, default)));
        if (conflict) Assert.Equal(IdentityError.Conflict, Assert.IsType<IdentityException>(thrown).Error);
        else Assert.Same(interceptor.Error, thrown); // Unexpected DB failures must still reach the server-error path.
        interceptor.Error = null;
        var rows = await env.Db.RefreshSessions.AsNoTracking().ToListAsync();
        Assert.Null(Assert.Single(rows).RevokedAt); // Aborted rotation did not consume/replace/revoke the token.
        Assert.NotNull((await env.Run(sp => sp.GetRequiredService<ISessionService>().RefreshAsync(login.RefreshToken!, default))).RefreshToken);
    }
}
